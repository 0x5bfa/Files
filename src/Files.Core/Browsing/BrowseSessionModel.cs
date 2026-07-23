// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;
using Files.Core.Capabilities.Changes;
using Files.Core.Capabilities.Thumbnails;
using Files.Core.Models;
using Files.Core.Storage;
using Files.Core.ViewSettings;
using System.Threading.Channels;

namespace Files.Core.Browsing;

public sealed class BrowseSessionModel : IBrowseSessionModel
{
	private const int ChangeQueueCapacity = 256;

	private readonly IBrowseLocationResolver locationResolver;
	private readonly IViewSettingsStore? viewSettingsStore;
	private readonly IThumbnailCache? thumbnailCache;
	private readonly Dictionary<BrowseLocation, BrowseViewSettings> sessionViewSettings = [];
	private BrowseItemProjection itemProjection;
	private readonly SemaphoreSlim navigationLock = new(1, 1);
	private readonly SemaphoreSlim refreshSignal = new(0, 1);
	private readonly Channel<QueuedFolderChange> changeQueue =
		Channel.CreateBounded<QueuedFolderChange>(
			new BoundedChannelOptions(ChangeQueueCapacity)
			{
				FullMode = BoundedChannelFullMode.Wait,
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false,
			});
	private readonly CancellationTokenSource refreshLifetime = new();
	private readonly object disposalLock = new();
	private BrowseContextState? activeContext;
	private BrowseContextState? preparingContext;
	private Task? disposeTask;
	private readonly Task refreshPumpTask;
	private long generationCounter;
	private long requestedFullRefreshGeneration;
	private int refreshSignalPending;
	private readonly Queue<QueuedFolderChange> deferredChanges = [];
	private BrowseSelectionState selection = BrowseSelectionState.Empty;
	private long itemsVersion;
	private bool isDisposed;

	public BrowseSessionModel(
		IBrowseLocationResolver locationResolver,
		IViewSettingsStore? viewSettingsStore = null,
		IThumbnailCache? thumbnailCache = null)
	{
		ArgumentNullException.ThrowIfNull(locationResolver);
		this.locationResolver = locationResolver;
		this.viewSettingsStore = viewSettingsStore;
		this.thumbnailCache = thumbnailCache;
		itemProjection = new BrowseItemProjection(BrowseViewSettings.Default);
		ViewSettings = BrowseViewSettings.Default;
		refreshPumpTask = RefreshPumpAsync(refreshLifetime.Token);
	}

	public BrowseLocation? Location { get; private set; }

	public IBrowseLocationContext? Context => Volatile.Read(ref activeContext)?.Context;

	public long Generation => Volatile.Read(ref activeContext)?.Generation ?? 0;

	public IReadOnlyList<IStorableModel> Items =>
		Volatile.Read(ref itemProjection).Items;

	public long ItemsVersion => Volatile.Read(ref itemsVersion);

	public BrowseSelectionState Selection => Volatile.Read(ref selection);

	public BrowseViewSettings ViewSettings { get; private set; }

	public bool IsLoading { get; private set; }

	public Exception? Error { get; private set; }

	public event EventHandler? StateChanged;

	public event EventHandler<BrowseItemsChangedEventArgs>? ItemsChanged;

	public event EventHandler? SelectionChanged;

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

				var nextProjection = new BrowseItemProjection(nextViewSettings);
				var nextItemChanges = nextProjection.Reset(nextItems);
				var previousContext = Volatile.Read(ref activeContext);
				var previousItems = Items;
				var nextSelection = Equals(Location, location)
					? NormalizeSelection(selection, nextProjection.Items)
					: BrowseSelectionState.Empty;
				Location = location;
				Volatile.Write(ref activeContext, nextContext);
				Volatile.Write(ref preparingContext, null);
				Volatile.Write(ref itemProjection, nextProjection);
				ViewSettings = nextViewSettings;
				Error = null;
				PublishItemsChanged(nextItemChanges);
				SetSelectionState(nextSelection);
				nextLocationContext = null;
				nextContext = null;
				committed = true;
				SignalRefreshPump();

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
							ref requestedFullRefreshGeneration,
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

				var currentContext = Volatile.Read(ref activeContext);
				if (currentContext is null)
				{
					continue;
				}

				try
				{
					if (await ProcessRequestedFullRefreshAsync(
						currentContext,
						cancellationToken).ConfigureAwait(false))
					{
						continue;
					}

					await ProcessChangesAsync(cancellationToken).ConfigureAwait(false);
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

	private async ValueTask<bool> ProcessRequestedFullRefreshAsync(
		BrowseContextState currentContext,
		CancellationToken cancellationToken)
	{
		var generation = Volatile.Read(ref requestedFullRefreshGeneration);
		if (generation is 0)
		{
			return false;
		}

		if (generation > currentContext.Generation)
		{
			return true;
		}

		if (generation < currentContext.Generation)
		{
			Interlocked.CompareExchange(
				ref requestedFullRefreshGeneration,
				0,
				generation);
			return false;
		}

		if (Interlocked.CompareExchange(
			ref requestedFullRefreshGeneration,
			0,
			generation) != generation)
		{
			return false;
		}

		await RefreshCurrentAsync(generation, cancellationToken).ConfigureAwait(false);
		return true;
	}

	private async ValueTask ProcessChangesAsync(CancellationToken cancellationToken)
	{
		while (TryReadNextChange(out var pendingChange))
		{
			var currentContext = Volatile.Read(ref activeContext);
			if (currentContext is null)
			{
				deferredChanges.Enqueue(pendingChange);
				return;
			}

			if (pendingChange.Generation < currentContext.Generation)
			{
				continue;
			}

			if (pendingChange.Generation > currentContext.Generation)
			{
				if (Volatile.Read(ref preparingContext)?.Generation == pendingChange.Generation)
				{
					deferredChanges.Enqueue(pendingChange);
				}

				continue;
			}

			if (Volatile.Read(ref requestedFullRefreshGeneration) == currentContext.Generation)
			{
				return;
			}

			var result = await ApplyChangeAsync(
				pendingChange,
				cancellationToken).ConfigureAwait(false);
			if (result is IncrementalApplyResult.RequiresFullRefresh)
			{
				RequestFullRefresh(currentContext.Generation);
				return;
			}
		}
	}

	private bool TryReadNextChange(out QueuedFolderChange pendingChange)
	{
		if (deferredChanges.Count is not 0)
		{
			pendingChange = deferredChanges.Dequeue();
			return true;
		}

		return changeQueue.Reader.TryRead(out pendingChange);
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

	private async ValueTask<IncrementalApplyResult> ApplyChangeAsync(
		QueuedFolderChange pendingChange,
		CancellationToken cancellationToken)
	{
		var currentContext = Volatile.Read(ref activeContext);
		if (currentContext is null
			|| currentContext.Generation != pendingChange.Generation)
		{
			return IncrementalApplyResult.Stale;
		}

		try
		{
			if (pendingChange.Change.RequiresRefresh
				|| pendingChange.Change.Kind is FolderChangeKind.DirectoryUpdated
				|| currentContext.Context is not IBrowseLocationItemResolver resolver)
			{
				return IncrementalApplyResult.RequiresFullRefresh;
			}

			return pendingChange.Change.Kind switch
			{
				FolderChangeKind.Created => await ApplyCreatedAsync(
					currentContext,
					resolver,
					pendingChange.Change,
					cancellationToken).ConfigureAwait(false),
				FolderChangeKind.Deleted => await ApplyDeletedAsync(
					currentContext,
					pendingChange.Change,
					cancellationToken).ConfigureAwait(false),
				FolderChangeKind.Renamed => await ApplyRenamedAsync(
					currentContext,
					resolver,
					pendingChange.Change,
					cancellationToken).ConfigureAwait(false),
				FolderChangeKind.Updated => await ApplyUpdatedAsync(
					currentContext,
					resolver,
					pendingChange.Change,
					cancellationToken).ConfigureAwait(false),
				_ => IncrementalApplyResult.RequiresFullRefresh,
			};
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
			return IncrementalApplyResult.RequiresFullRefresh;
		}
	}

	private async ValueTask<IncrementalApplyResult> ApplyCreatedAsync(
		BrowseContextState context,
		IBrowseLocationItemResolver resolver,
		FolderChange change,
		CancellationToken cancellationToken)
	{
		if (!TryGetKey(change.CurrentItem, out var key))
		{
			return IncrementalApplyResult.RequiresFullRefresh;
		}

		await navigationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		ItemLookupResult lookup;
		try
		{
			if (!IsActiveGeneration(context))
			{
				return IncrementalApplyResult.Stale;
			}

			lookup = FindItemIndex(
				Volatile.Read(ref itemProjection),
				key,
				out _);
			if (lookup is ItemLookupResult.Found)
			{
				return IncrementalApplyResult.Applied;
			}

			if (lookup is ItemLookupResult.Ambiguous)
			{
				return IncrementalApplyResult.RequiresFullRefresh;
			}
		}
		finally
		{
			navigationLock.Release();
		}

		var replacement = await resolver
			.ResolveAsync(change.CurrentItem!, cancellationToken)
			.ConfigureAwait(false);
		var retained = false;

		try
		{
			if (!HasKey(replacement, key))
			{
				return IncrementalApplyResult.RequiresFullRefresh;
			}

			await navigationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				if (!IsActiveGeneration(context))
				{
					return IncrementalApplyResult.Stale;
				}

				var projection = Volatile.Read(ref itemProjection);
				lookup = FindItemIndex(projection, key, out _);
				if (lookup is ItemLookupResult.Found)
				{
					return IncrementalApplyResult.Applied;
				}

				if (lookup is ItemLookupResult.Ambiguous)
				{
					return IncrementalApplyResult.RequiresFullRefresh;
				}

				var changes = projection.Add(replacement);
				if (changes.IsEmpty)
				{
					return IncrementalApplyResult.Applied;
				}

				retained = true;
				PublishItemsChanged(changes);
				OnStateChanged();
				return IncrementalApplyResult.Applied;
			}
			finally
			{
				navigationLock.Release();
			}
		}
		finally
		{
			if (!retained)
			{
				replacement.Dispose();
			}
		}
	}

	private async ValueTask<IncrementalApplyResult> ApplyDeletedAsync(
		BrowseContextState context,
		FolderChange change,
		CancellationToken cancellationToken)
	{
		if (!TryGetKey(change.PreviousItem, out var key))
		{
			return IncrementalApplyResult.RequiresFullRefresh;
		}

		await navigationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (!IsActiveGeneration(context))
			{
				return IncrementalApplyResult.Stale;
			}

			var lookup = FindItemIndex(
				Volatile.Read(ref itemProjection),
				key,
				out _);
			if (lookup is not ItemLookupResult.Found)
			{
				return IncrementalApplyResult.RequiresFullRefresh;
			}
		}
		finally
		{
			navigationLock.Release();
		}

		await InvalidateAsync([change.PreviousItem], cancellationToken).ConfigureAwait(false);
		await navigationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (!IsActiveGeneration(context))
			{
				return IncrementalApplyResult.Stale;
			}

			var projection = Volatile.Read(ref itemProjection);
			var lookup = FindItemIndex(projection, key, out var index);
			if (lookup is not ItemLookupResult.Found)
			{
				return IncrementalApplyResult.RequiresFullRefresh;
			}

			var removed = projection.Items[index];
			var changes = projection.Remove(key);
			if (changes.IsEmpty)
			{
				return IncrementalApplyResult.RequiresFullRefresh;
			}

			try
			{
				PublishItemsChanged(changes);
				RemoveSelectionKey(key);
				OnStateChanged();
			}
			finally
			{
				removed.Dispose();
			}

			return IncrementalApplyResult.Applied;
		}
		finally
		{
			navigationLock.Release();
		}
	}

	private async ValueTask<IncrementalApplyResult> ApplyRenamedAsync(
		BrowseContextState context,
		IBrowseLocationItemResolver resolver,
		FolderChange change,
		CancellationToken cancellationToken)
	{
		if (!TryGetKey(change.CurrentItem, out var currentKey))
		{
			return IncrementalApplyResult.RequiresFullRefresh;
		}

		var oldKey = change.PreviousItem is not null
			&& TryGetKey(change.PreviousItem, out var previousKey)
			? previousKey
			: currentKey;
		var previousKeyToReplace = oldKey;
		await navigationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (!IsActiveGeneration(context))
			{
				return IncrementalApplyResult.Stale;
			}

			var lookup = FindItemIndex(
				Volatile.Read(ref itemProjection),
				oldKey,
				out _);
			if (lookup is not ItemLookupResult.Found
				&& oldKey != currentKey)
			{
				previousKeyToReplace = currentKey;
				lookup = FindItemIndex(
					Volatile.Read(ref itemProjection),
					currentKey,
					out _);
			}

			if (lookup is not ItemLookupResult.Found)
			{
				return IncrementalApplyResult.RequiresFullRefresh;
			}
		}
		finally
		{
			navigationLock.Release();
		}

		var replacement = await resolver
			.ResolveAsync(change.CurrentItem!, cancellationToken)
			.ConfigureAwait(false);
		var retained = false;
		var sameInstance = false;

		try
		{
			if (!HasKey(replacement, currentKey))
			{
				return IncrementalApplyResult.RequiresFullRefresh;
			}

			await InvalidateAsync(
				[change.PreviousItem, change.CurrentItem],
				cancellationToken).ConfigureAwait(false);

			await navigationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				if (!IsActiveGeneration(context))
				{
					return IncrementalApplyResult.Stale;
				}

				var projection = Volatile.Read(ref itemProjection);
				var lookup = FindItemIndex(
					projection,
					previousKeyToReplace,
					out var index);

				if (lookup is not ItemLookupResult.Found)
				{
					return IncrementalApplyResult.RequiresFullRefresh;
				}

				var previous = projection.Items[index];
				if (ReferenceEquals(previous, replacement))
				{
					sameInstance = true;
					return IncrementalApplyResult.RequiresFullRefresh;
				}

				var changes = projection.Replace(previousKeyToReplace, replacement);
				retained = true;
				try
				{
					PublishItemsChanged(changes);
					MigrateSelection(previousKeyToReplace, currentKey);
					OnStateChanged();
				}
				finally
				{
					previous.Dispose();
				}

				return IncrementalApplyResult.Applied;
			}
			finally
			{
				navigationLock.Release();
			}
		}
		finally
		{
			if (!retained && !sameInstance)
			{
				replacement.Dispose();
			}
		}
	}

	private async ValueTask<IncrementalApplyResult> ApplyUpdatedAsync(
		BrowseContextState context,
		IBrowseLocationItemResolver resolver,
		FolderChange change,
		CancellationToken cancellationToken)
	{
		if (!TryGetKey(change.CurrentItem, out var key))
		{
			return IncrementalApplyResult.RequiresFullRefresh;
		}

		await navigationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (!IsActiveGeneration(context))
			{
				return IncrementalApplyResult.Stale;
			}

			var lookup = FindItemIndex(
				Volatile.Read(ref itemProjection),
				key,
				out _);
			if (lookup is not ItemLookupResult.Found)
			{
				return IncrementalApplyResult.RequiresFullRefresh;
			}
		}
		finally
		{
			navigationLock.Release();
		}

		var replacement = await resolver
			.ResolveAsync(change.CurrentItem!, cancellationToken)
			.ConfigureAwait(false);
		var retained = false;
		var sameInstance = false;

		try
		{
			if (!HasKey(replacement, key))
			{
				return IncrementalApplyResult.RequiresFullRefresh;
			}

			await InvalidateAsync([change.CurrentItem], cancellationToken).ConfigureAwait(false);
			await navigationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				if (!IsActiveGeneration(context))
				{
					return IncrementalApplyResult.Stale;
				}

				var projection = Volatile.Read(ref itemProjection);
				var lookup = FindItemIndex(projection, key, out var index);
				if (lookup is not ItemLookupResult.Found)
				{
					return IncrementalApplyResult.RequiresFullRefresh;
				}

				var previous = projection.Items[index];
				if (ReferenceEquals(previous, replacement))
				{
					sameInstance = true;
					return IncrementalApplyResult.RequiresFullRefresh;
				}

				var changes = projection.Replace(key, replacement);
				retained = true;
				try
				{
					PublishItemsChanged(changes);
					OnStateChanged();
				}
				finally
				{
					previous.Dispose();
				}

				return IncrementalApplyResult.Applied;
			}
			finally
			{
				navigationLock.Release();
			}
		}
		finally
		{
			if (!retained && !sameInstance)
			{
				replacement.Dispose();
			}
		}
	}

	private async ValueTask InvalidateAsync(
		IEnumerable<StorableReference?> references,
		CancellationToken cancellationToken)
	{
		if (thumbnailCache is null)
		{
			return;
		}

		var seen = new HashSet<StorableKey>();
		foreach (var reference in references)
		{
			if (reference is null || !seen.Add(ToKey(reference)))
			{
				continue;
			}

			await thumbnailCache
				.InvalidateAsync(reference, cancellationToken)
				.ConfigureAwait(false);
		}
	}

	private bool IsActiveGeneration(BrowseContextState context)
	{
		return !Volatile.Read(ref isDisposed)
			&& ReferenceEquals(Volatile.Read(ref activeContext), context);
	}

	private static bool TryGetKey(
		StorableReference? reference,
		out StorableKey key)
	{
		if (reference is null)
		{
			key = default;
			return false;
		}

		key = ToKey(reference);
		return true;
	}

	private static StorableKey ToKey(StorableReference reference)
	{
		return new StorableKey(reference.SourceId, reference.ItemId);
	}

	private static bool HasKey(IStorableModel model, StorableKey key)
	{
		return ToKey(model.Reference) == key;
	}

	private static ItemLookupResult FindItemIndex(
		BrowseItemProjection projection,
		StorableKey key,
		out int index)
	{
		ArgumentNullException.ThrowIfNull(projection);
		return projection.TryGet(key, out _, out index)
			? ItemLookupResult.Found
			: ItemLookupResult.Missing;
	}

	private bool EnqueueChange(
		BrowseContextState context,
		FolderChange change)
	{
		if (Volatile.Read(ref isDisposed) || !IsKnownContext(context))
		{
			return false;
		}

		var pendingChange = new QueuedFolderChange(context.Generation, change);
		if (!changeQueue.Writer.TryWrite(pendingChange))
		{
			return RequestFullRefresh(context.Generation);
		}

		SignalRefreshPump();
		return true;
	}

	private void OnFolderChanged(
		BrowseContextState context,
		FolderChange change)
	{
		EnqueueChange(context, change);
	}

	private void OnFolderChangeFaulted(
		BrowseContextState context,
		FolderChangeErrorEventArgs args)
	{
		if (!RequestFullRefresh(context.Generation))
		{
			return;
		}

		Error = args.Error;
		OnStateChanged();
	}

	private bool RequestFullRefresh(long generation)
	{
		if (Volatile.Read(ref isDisposed))
		{
			return false;
		}

		var activeGeneration = Volatile.Read(ref activeContext)?.Generation;
		var preparingGeneration = Volatile.Read(ref preparingContext)?.Generation;
		if (activeGeneration != generation && preparingGeneration != generation)
		{
			return false;
		}

		while (true)
		{
			var requestedGeneration = Volatile.Read(ref requestedFullRefreshGeneration);
			if (requestedGeneration >= generation)
			{
				break;
			}

			if (Interlocked.CompareExchange(
				ref requestedFullRefreshGeneration,
				generation,
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

			var changes = Volatile
				.Read(ref itemProjection)
				.UpdateSort(settings);
			ViewSettings = settings;
			PublishItemsChanged(changes);
			OnStateChanged();
		}
		finally
		{
			navigationLock.Release();
		}
	}

	public void SetSelection(
		IEnumerable<StorableKey> selectedKeys,
		StorableKey? focusedKey,
		StorableKey? anchorKey)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);
		ArgumentNullException.ThrowIfNull(selectedKeys);

		SetSelectionState(NormalizeSelection(
			new BrowseSelectionState(
				Array.AsReadOnly(selectedKeys.ToArray()),
				focusedKey,
				anchorKey),
			Items));
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
			changeQueue.Writer.TryComplete();
			await navigationLock.WaitAsync().ConfigureAwait(false);

			try
			{
				var items = Items;
				var currentContext = Volatile.Read(ref activeContext);
				Volatile.Write(
					ref itemProjection,
					new BrowseItemProjection(ViewSettings));
				Volatile.Write(ref selection, BrowseSelectionState.Empty);
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

	private void PublishItemsChanged(BrowseItemChangeSet changeSet)
	{
		if (changeSet.IsEmpty)
		{
			return;
		}

		var previousVersion = Interlocked.Read(ref itemsVersion);
		var version = Interlocked.Increment(ref itemsVersion);
		ItemsChanged?.Invoke(
			this,
			new BrowseItemsChangedEventArgs(
				previousVersion,
				version,
				changeSet.Changes));
	}

	private void SetSelectionState(BrowseSelectionState nextSelection)
	{
		ArgumentNullException.ThrowIfNull(nextSelection);

		var currentSelection = Volatile.Read(ref selection);
		if (currentSelection.FocusedKey == nextSelection.FocusedKey
			&& currentSelection.AnchorKey == nextSelection.AnchorKey
			&& currentSelection.SelectedKeys.SequenceEqual(nextSelection.SelectedKeys))
		{
			return;
		}

		Volatile.Write(ref selection, nextSelection);
		SelectionChanged?.Invoke(this, EventArgs.Empty);
	}

	private void RemoveSelectionKey(StorableKey key)
	{
		var currentSelection = Volatile.Read(ref selection);
		if (!currentSelection.SelectedKeys.Contains(key)
			&& currentSelection.FocusedKey != key
			&& currentSelection.AnchorKey != key)
		{
			return;
		}

		SetSelectionState(new BrowseSelectionState(
			Array.AsReadOnly(currentSelection.SelectedKeys
				.Where(selectedKey => selectedKey != key)
				.ToArray()),
			currentSelection.FocusedKey == key
				? null
				: currentSelection.FocusedKey,
			currentSelection.AnchorKey == key
				? null
				: currentSelection.AnchorKey));
	}

	private void MigrateSelection(StorableKey previousKey, StorableKey currentKey)
	{
		if (previousKey == currentKey)
		{
			return;
		}

		var currentSelection = Volatile.Read(ref selection);
		SetSelectionState(new BrowseSelectionState(
			Array.AsReadOnly(currentSelection.SelectedKeys
				.Select(selectedKey => selectedKey == previousKey ? currentKey : selectedKey)
				.Distinct()
				.ToArray()),
			currentSelection.FocusedKey == previousKey
				? currentKey
				: currentSelection.FocusedKey,
			currentSelection.AnchorKey == previousKey
				? currentKey
				: currentSelection.AnchorKey));
	}

	private static BrowseSelectionState NormalizeSelection(
		BrowseSelectionState state,
		IReadOnlyList<IStorableModel> items)
	{
		var existingKeys = items
			.Select(static item => item.Reference.GetKey())
			.ToHashSet();
		return new BrowseSelectionState(
			Array.AsReadOnly(state.SelectedKeys
				.Where(existingKeys.Contains)
				.Distinct()
				.ToArray()),
			state.FocusedKey is { } focusedKey
				&& existingKeys.Contains(focusedKey)
				? focusedKey
				: null,
			state.AnchorKey is { } anchorKey
				&& existingKeys.Contains(anchorKey)
				? anchorKey
				: null);
	}

	private readonly record struct QueuedFolderChange(
		long Generation,
		FolderChange Change);

	private enum IncrementalApplyResult
	{
		Applied,
		Stale,
		RequiresFullRefresh,
	}

	private enum ItemLookupResult
	{
		Missing,
		Found,
		Ambiguous,
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
			owner.OnFolderChanged(this, args.Change);
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
