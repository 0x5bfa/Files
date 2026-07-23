// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Models;
using Files.Core.ViewSettings;

namespace Files.Core.Browsing;

public sealed class BrowseSessionModel : IBrowseSessionModel
{
	private readonly IBrowseLocationResolver locationResolver;
	private readonly IViewSettingsStore? viewSettingsStore;
	private readonly Dictionary<BrowseLocation, BrowseViewSettings> sessionViewSettings = [];
	private readonly SemaphoreSlim navigationLock = new(1, 1);
	private readonly object disposalLock = new();
	private IBrowseLocationContext? context;
	private Task? disposeTask;
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
	}

	public BrowseLocation? Location { get; private set; }

	public IBrowseLocationContext? Context => context;

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
			IsLoading = true;
			Error = null;
			OnStateChanged();

			try
			{
				var nextItems = new List<IStorableModel>();
				IBrowseLocationContext? nextContext = null;
				var committed = false;

				try
				{
					nextContext = await locationResolver
						.OpenAsync(location, cancellationToken)
						.ConfigureAwait(false);
					ArgumentNullException.ThrowIfNull(nextContext);

					var nextViewSettings = viewSettingsStore is null
						? sessionViewSettings.GetValueOrDefault(location, BrowseViewSettings.Default)
						: await viewSettingsStore
							.GetAsync(location, cancellationToken)
							.ConfigureAwait(false)
							?? BrowseViewSettings.Default;

					await foreach (var item in nextContext.GetItemsAsync(cancellationToken).ConfigureAwait(false))
					{
						nextItems.Add(item);
					}

					var previousContext = context;
					var previousItems = Items;
					Location = location;
					context = nextContext;
					Items = nextItems.AsReadOnly();
					ViewSettings = nextViewSettings;
					nextContext = null;
					committed = true;

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
		finally
		{
			navigationLock.Release();
		}
	}

	public ValueTask RefreshAsync(CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);

		return Location is null
			? ValueTask.CompletedTask
			: NavigateAsync(Location, cancellationToken);
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
		await navigationLock.WaitAsync().ConfigureAwait(false);

		try
		{
			var items = Items;
			var activeContext = context;
			Items = Array.Empty<IStorableModel>();
			context = null;
			sessionViewSettings.Clear();

			try
			{
				DisposeItems(items);
			}
			finally
			{
				if (activeContext is not null)
				{
					await activeContext.DisposeAsync().ConfigureAwait(false);
				}
			}
		}
		finally
		{
			navigationLock.Release();
			navigationLock.Dispose();
			GC.SuppressFinalize(this);
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
