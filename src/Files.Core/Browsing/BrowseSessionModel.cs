// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;
using Files.Core.Capabilities.Changes;
using Files.Core.Models;
using Files.Core.ViewSettings;

namespace Files.Core.Browsing;

public sealed class BrowseSessionModel : IBrowseSessionModel
{
	private readonly IBrowseLocationResolver locationResolver;
	private readonly IViewSettingsStore? viewSettingsStore;
	private readonly Dictionary<BrowseLocation, BrowseViewSettings> sessionViewSettings = [];
	private readonly SemaphoreSlim navigationLock = new(1, 1);
	private readonly SemaphoreSlim refreshSignal = new(0, 1);
	private readonly CancellationTokenSource refreshLifetime = new();
	private readonly object disposalLock = new();
	private BrowseContextState? activeContext;
	private BrowseContextState? preparingContext;
	private Task? disposeTask;
	private readonly Task refreshPumpTask;
	private long generationCounter;
	private long requestedRefreshGeneration;
	private int refreshSignalPending;
	private bool isDisposed;

	public BrowseSessionModel(
		IBrowseLocationResolver locationResolver,
		IViewSettingsStore? viewSettingsStore = null)
	{
		ArgumentNullException.ThrowIfNull(locationResolver);
		this.locationResolver = locationResolver;
		this.viewSettingsStore = viewSettingsStore;
		Items = Array.Empty<IStorableModel>();
		ViewSettings = BrowseViewSettings.Default;
		refreshPumpTask = RefreshPumpAsync(refreshLifetime.Token);
	}

	public BrowseLocation? Location { get; private set; }

	public IBrowseLocationContext? Context => Volatile.Read(ref activeContext)?.Context;

	public IReadOnlyList<IStorableModel> Items { get; private set; }

	public BrowseViewSettings ViewSettings { get; private set; }

	public bool IsLoading { get; private set; }

	public Exception? Error { get; private set; }

	public event EventHandler? StateChanged;

	public async ValueTask NavigateAsync(BrowseLocation location, CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);
		ArgumentNullException.ThrowIfNull(location);

		await navigationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			await NavigateCoreAsync(location, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			navigationLock.Release();
		}
	}

	private async ValueTask NavigateCoreAsync(
		BrowseLocation location,
		CancellationToken cancellationToken)
	{
		IsLoading = true;
		Error = null;
		OnStateChanged();

		try
		{
			var nextItems = new List<IStorableModel>();
			IBrowseLocationContext? nextLocationContext = null;
			BrowseContextState? nextContext = null;
			var committed = false;

			try
			{
				nextLocationContext = await locationResolver
					.OpenAsync(location, cancellationToken)
					.ConfigureAwait(false);
				ArgumentNullException.ThrowIfNull(nextLocationContext);

				var changes = nextLocationContext.LocationModel?
					.Get<IFolderChangeSource>();
				var generation = Interlocked.Increment(ref generationCounter);
				nextContext = new BrowseContextState(
					this,
					nextLocationContext,
					changes,
					generation);
				Volatile.Write(ref preparingContext, nextContext);

				var nextViewSettings = viewSettingsStore is null
					? sessionViewSettings.GetValueOrDefault(location, BrowseViewSettings.Default)
					: await viewSettingsStore
						.GetAsync(location, cancellationToken)
						.ConfigureAwait(false)
						?? BrowseViewSettings.Default;

				await nextContext.StartAsync(cancellationToken).ConfigureAwait(false);

				await foreach (var item in nextLocationContext.GetItemsAsync(cancellationToken).ConfigureAwait(false))
				{
					nextItems.Add(item);
				}

				var previousContext = Volatile.Read(ref activeContext);
				var previousItems = Items;
				Location = location;
				Volatile.Write(ref activeContext, nextContext);
				Volatile.Write(ref preparingContext, null);
				Items = nextItems.AsReadOnly();
				ViewSettings = nextViewSettings;
				Error = null;
				nextLocationContext = null;
				nextContext = null;
				committed = true;
				WakeRefreshPumpIfRequested(generation);

				try
				{
					DisposeItems(previousItems);
				}
				finally
				{
					if (previousContext is not null)
					{
						await previousContext.DisposeAsync().ConfigureAwait(false);
					}
				}
			}
			finally
			{
				if (!committed)
				{
					if (nextContext is not null)
					{
						Volatile.Write(ref preparingContext, null);
						Interlocked.CompareExchange(
							ref requestedRefreshGeneration,
							0,
							nextContext.Generation);
					}

					try
					{
						DisposeItems(nextItems);
					}
					finally
					{
						if (nextContext is not null)
						{
							await nextContext.DisposeAsync().ConfigureAwait(false);
						}
						else if (nextLocationContext is not null)
						{
							await nextLocationContext.DisposeAsync().ConfigureAwait(false);
						}
					}
				}
			}
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			Error = exception;
			throw;
		}
		finally
		{
			IsLoading = false;
			OnStateChanged();
		}
	}

	public ValueTask RefreshAsync(CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);

		return Location is null
			? ValueTask.CompletedTask
			: NavigateAsync(Location, cancellationToken);
	}

	private async Task RefreshPumpAsync(CancellationToken cancellationToken)
	{
		try
		{
			while (true)
			{
				await refreshSignal
					.WaitAsync(cancellationToken)
					.ConfigureAwait(false);
				Interlocked.Exchange(ref refreshSignalPending, 0);

				var generation = Volatile.Read(ref requestedRefreshGeneration);
				var currentContext = Volatile.Read(ref activeContext);
				if (generation is 0
					|| currentContext is null
					|| currentContext.Generation != generation
					|| Interlocked.CompareExchange(
						ref requestedRefreshGeneration,
						0,
						generation) != generation)
				{
					continue;
				}

				try
				{
					await RefreshCurrentAsync(generation, cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					return;
				}
				catch
				{
					// NavigateCoreAsync records the refresh failure in Error.
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private async ValueTask RefreshCurrentAsync(
		long generation,
		CancellationToken cancellationToken)
	{
		await navigationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			var currentContext = Volatile.Read(ref activeContext);
			if (currentContext is null || currentContext.Generation != generation)
			{
				return;
			}

			await NavigateCoreAsync(
				currentContext.Context.Location,
				cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			navigationLock.Release();
		}
	}

	private void OnFolderChanged(BrowseContextState context)
	{
		RequestRefresh(context);
	}

	private void OnFolderChangeFaulted(
		BrowseContextState context,
		FolderChangeErrorEventArgs args)
	{
		if (!RequestRefresh(context))
		{
			return;
		}

		Error = args.Error;
		OnStateChanged();
	}

	private bool RequestRefresh(BrowseContextState context)
	{
		if (Volatile.Read(ref isDisposed) || !IsKnownContext(context))
		{
			return false;
		}

		while (true)
		{
			if (!IsKnownContext(context))
			{
				return false;
			}

			var requestedGeneration = Volatile.Read(ref requestedRefreshGeneration);
			if (requestedGeneration >= context.Generation)
			{
				break;
			}

			if (Interlocked.CompareExchange(
				ref requestedRefreshGeneration,
				context.Generation,
				requestedGeneration) == requestedGeneration)
			{
				break;
			}
		}

		SignalRefreshPump();
		return true;
	}

	private bool IsKnownContext(BrowseContextState context)
	{
		return ReferenceEquals(Volatile.Read(ref activeContext), context)
			|| ReferenceEquals(Volatile.Read(ref preparingContext), context);
	}

	private void WakeRefreshPumpIfRequested(long generation)
	{
		if (Volatile.Read(ref requestedRefreshGeneration) == generation)
		{
			SignalRefreshPump();
		}
	}

	private void SignalRefreshPump()
	{
		if (Interlocked.Exchange(ref refreshSignalPending, 1) is not 0)
		{
			return;
		}

		try
		{
			refreshSignal.Release();
		}
		catch (ObjectDisposedException)
		{
		}
	}

	public async ValueTask UpdateViewSettingsAsync(
		BrowseViewSettings settings,
		CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);
		ArgumentNullException.ThrowIfNull(settings);

		await navigationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			if (Location is null)
			{
				throw new InvalidOperationException("View settings require an active browse location.");
			}

			if (viewSettingsStore is not null)
			{
				await viewSettingsStore
					.SetAsync(Location, settings, cancellationToken)
					.ConfigureAwait(false);
			}
			else
			{
				sessionViewSettings[Location] = settings;
			}

			ViewSettings = settings;
			OnStateChanged();
		}
		finally
		{
			navigationLock.Release();
		}
	}

	public void Dispose()
	{
		DisposeAsync().AsTask().GetAwaiter().GetResult();
	}

	public ValueTask DisposeAsync()
	{
		lock (disposalLock)
		{
			isDisposed = true;
			disposeTask ??= DisposeCoreAsync();
			return new ValueTask(disposeTask);
		}
	}

	private async Task DisposeCoreAsync()
	{
		refreshLifetime.Cancel();
		SignalRefreshPump();
		try
		{
			await refreshPumpTask.ConfigureAwait(false);
			await navigationLock.WaitAsync().ConfigureAwait(false);

			try
			{
				var items = Items;
				var currentContext = Volatile.Read(ref activeContext);
				Items = Array.Empty<IStorableModel>();
				Volatile.Write(ref activeContext, null);
				Volatile.Write(ref preparingContext, null);
				sessionViewSettings.Clear();

				try
				{
					DisposeItems(items);
				}
				finally
				{
					if (currentContext is not null)
					{
						await currentContext.DisposeAsync().ConfigureAwait(false);
					}
				}
			}
			finally
			{
				navigationLock.Release();
			}
		}
		finally
		{
			navigationLock.Dispose();
			refreshSignal.Dispose();
			refreshLifetime.Dispose();
			GC.SuppressFinalize(this);
		}
	}

	private sealed class BrowseContextState : IAsyncDisposable
	{
		private readonly BrowseSessionModel owner;
		private readonly IFolderChangeSource? changes;
		private int handlersAttached;

		public BrowseContextState(
			BrowseSessionModel owner,
			IBrowseLocationContext context,
			IFolderChangeSource? changes,
			long generation)
		{
			this.owner = owner;
			Context = context;
			this.changes = changes;
			Generation = generation;
		}

		public IBrowseLocationContext Context { get; }

		public long Generation { get; }

		public async ValueTask StartAsync(CancellationToken cancellationToken)
		{
			if (changes is null)
			{
				return;
			}

			changes.Changed += OnChanged;
			changes.Faulted += OnFaulted;
			Volatile.Write(ref handlersAttached, 1);

			try
			{
				await changes.StartAsync(cancellationToken).ConfigureAwait(false);
			}
			catch
			{
				Detach();
				throw;
			}
		}

		public async ValueTask DisposeAsync()
		{
			Detach();
			await Context.DisposeAsync().ConfigureAwait(false);
		}

		private void Detach()
		{
			if (changes is null
				|| Interlocked.Exchange(ref handlersAttached, 0) is 0)
			{
				return;
			}

			changes.Changed -= OnChanged;
			changes.Faulted -= OnFaulted;
		}

		private void OnChanged(
			object? sender,
			FolderChangeEventArgs args)
		{
			owner.OnFolderChanged(this);
		}

		private void OnFaulted(
			object? sender,
			FolderChangeErrorEventArgs args)
		{
			owner.OnFolderChangeFaulted(this, args);
		}
	}

	private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

	private static void DisposeItems(IEnumerable<IStorableModel> items)
	{
		foreach (var item in items)
		{
			item.Dispose();
		}
	}
}
