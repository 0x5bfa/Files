// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities.Properties;
using Files.Core.Capabilities.Thumbnails;
using Files.Core.Capabilities;
using Files.Core.Models;
using Files.Core.ViewSettings;

namespace Files.Core.Browsing;

/// <summary>
/// Performs cancellable, generation-bound property and thumbnail prefetching.
/// </summary>
public sealed class BrowsePrefetchCoordinator : IBrowsePrefetchCoordinator
{
	private const int DefaultThumbnailSize = 96;

	private readonly IBrowseSessionModel session;
	private readonly object syncRoot = new();
	private readonly int thumbnailSize;
	private readonly HashSet<Task> activeTasks = [];
	private CancellationTokenSource? currentPrefetch;
	private long currentGeneration;
	private bool isDisposed;

	public BrowsePrefetchCoordinator(
		IBrowseSessionModel session,
		int thumbnailSize = DefaultThumbnailSize)
	{
		ArgumentNullException.ThrowIfNull(session);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(thumbnailSize);

		this.session = session;
		this.thumbnailSize = thumbnailSize;
		session.ItemsChanged += OnSessionItemsChanged;
	}

	public void UpdateViewport(
		BrowseViewport viewport,
		BrowseViewSettings settings,
		long browseGeneration)
	{
		ArgumentNullException.ThrowIfNull(viewport);
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentOutOfRangeException.ThrowIfNegative(browseGeneration);

		var nextCancellation = new CancellationTokenSource();
		CancellationTokenSource? previousCancellation;
		lock (syncRoot)
		{
			ObjectDisposedException.ThrowIf(isDisposed, this);
			previousCancellation = currentPrefetch;
			currentPrefetch = nextCancellation;
			currentGeneration = browseGeneration;
			var task = Task.Run(
				() => PrefetchAsync(
					viewport,
					settings,
					browseGeneration,
					nextCancellation.Token),
				CancellationToken.None);
			activeTasks.Add(task);
			_ = task.ContinueWith(
				RemoveCompletedTask,
				CancellationToken.None,
				TaskContinuationOptions.DenyChildAttach,
				TaskScheduler.Default);
		}

		CancelAndDispose(previousCancellation);
	}

	public async ValueTask DisposeAsync()
	{
		CancellationTokenSource? cancellation;
		Task[] tasks;
		lock (syncRoot)
		{
			if (isDisposed)
			{
				return;
			}

			isDisposed = true;
			cancellation = currentPrefetch;
			currentPrefetch = null;
			tasks = activeTasks.ToArray();
		}

		session.ItemsChanged -= OnSessionItemsChanged;
		CancelAndDispose(cancellation);

		if (tasks.Length is not 0)
		{
			await Task.WhenAll(tasks).ConfigureAwait(false);
		}
	}

	private async Task PrefetchAsync(
		BrowseViewport viewport,
		BrowseViewSettings settings,
		long generation,
		CancellationToken cancellationToken)
	{
		try
		{
			var propertyIds = GetPropertyIds(settings);
			var items = session.Items;
			foreach (var index in EnumerateIndices(items.Count, viewport))
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!IsCurrent(generation, cancellationToken))
				{
					return;
				}

				await PrefetchItemAsync(
					items[index],
					propertyIds,
					generation,
					cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch
		{
			// Prefetch is best effort; the foreground consumer can retry.
		}
	}

	private async ValueTask PrefetchItemAsync(
		IStorableModel item,
		IReadOnlyList<string> propertyIds,
		long generation,
		CancellationToken cancellationToken)
	{
		if (propertyIds.Count is not 0
			&& item.Get<IPropertySource>() is { } propertySource)
		{
			try
			{
				await propertySource
					.GetPropertiesAsync(
						new PropertyRequest(propertyIds),
						cancellationToken)
					.ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch
			{
				// Prefetch is best effort; the foreground consumer can retry.
			}
		}

		if (!IsCurrent(generation, cancellationToken))
		{
			return;
		}

		if (item.Get<IThumbnailSource>() is { } thumbnailSource)
		{
			try
			{
				await thumbnailSource
					.GetThumbnailAsync(
						new ThumbnailRequest(
							thumbnailSize,
							ThumbnailMode.PreferContent),
						cancellationToken)
					.ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch
			{
				// Prefetch is best effort; the foreground consumer can retry.
			}
		}
	}

	private bool IsCurrent(long generation, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested
			|| session.Generation != generation)
		{
			return false;
		}

		lock (syncRoot)
		{
			return !isDisposed
				&& currentGeneration == generation
				&& currentPrefetch is not null;
		}
	}

	private void OnSessionItemsChanged(
		object? sender,
		BrowseItemsChangedEventArgs args)
	{
		CancellationTokenSource? cancellation = null;
		lock (syncRoot)
		{
			if (currentPrefetch is not null
				&& currentGeneration != session.Generation)
			{
				cancellation = currentPrefetch;
				currentPrefetch = null;
			}
		}

		CancelAndDispose(cancellation);
	}

	private static IReadOnlyList<string> GetPropertyIds(BrowseViewSettings settings)
	{
		var propertyIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var column in settings.Columns)
		{
			if (column.IsVisible
				&& !string.IsNullOrWhiteSpace(column.PropertyId))
			{
				propertyIds.Add(column.PropertyId);
			}
		}

		if (!string.IsNullOrWhiteSpace(settings.SortPropertyId))
		{
			propertyIds.Add(settings.SortPropertyId);
		}

		return propertyIds.ToArray();
	}

	private static IEnumerable<int> EnumerateIndices(
		int itemCount,
		BrowseViewport viewport)
	{
		var visibleStart = Math.Min(viewport.FirstVisibleIndex, itemCount);
		var visibleEnd = Math.Min(
			itemCount,
			visibleStart + viewport.VisibleCount);
		var lookAheadEnd = Math.Min(
			itemCount,
			visibleEnd + viewport.LookAheadCount);

		for (var index = visibleStart; index < visibleEnd; index++)
		{
			yield return index;
		}

		for (var index = visibleEnd; index < lookAheadEnd; index++)
		{
			yield return index;
		}

		for (var index = 0; index < visibleStart; index++)
		{
			yield return index;
		}

		for (var index = lookAheadEnd; index < itemCount; index++)
		{
			yield return index;
		}
	}

	private static void CancelAndDispose(CancellationTokenSource? cancellation)
	{
		if (cancellation is null)
		{
			return;
		}

		cancellation.Cancel();
		cancellation.Dispose();
	}

	private void RemoveCompletedTask(Task task)
	{
		lock (syncRoot)
		{
			activeTasks.Remove(task);
		}
	}
}
