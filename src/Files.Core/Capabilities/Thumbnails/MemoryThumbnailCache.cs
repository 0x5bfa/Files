// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;

namespace Files.Core.Capabilities.Thumbnails;

/// <summary>
/// Provides a bounded process-memory LRU cache for thumbnail decorators.
/// </summary>
public sealed class MemoryThumbnailCache : IThumbnailCache
{
	private readonly object syncRoot = new();
	private readonly int capacity;
	private readonly Dictionary<ThumbnailCacheKey, CacheItem> items = [];
	private readonly LinkedList<ThumbnailCacheKey> usage = [];

	public MemoryThumbnailCache(int capacity = 512)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
		this.capacity = capacity;
	}

	public ValueTask<ThumbnailCacheEntry?> GetAsync(
		ThumbnailCacheKey key,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(key);
		cancellationToken.ThrowIfCancellationRequested();

		lock (syncRoot)
		{
			if (!items.TryGetValue(key, out var item))
			{
				return ValueTask.FromResult<ThumbnailCacheEntry?>(null);
			}

			usage.Remove(item.UsageNode);
			usage.AddFirst(item.UsageNode);
			return ValueTask.FromResult<ThumbnailCacheEntry?>(item.Entry);
		}
	}

	public ValueTask SetAsync(
		ThumbnailCacheKey key,
		ThumbnailCacheEntry entry,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(key);
		ArgumentNullException.ThrowIfNull(entry);
		cancellationToken.ThrowIfCancellationRequested();

		lock (syncRoot)
		{
			if (items.TryGetValue(key, out var existing))
			{
				existing.Entry = entry;
				usage.Remove(existing.UsageNode);
				usage.AddFirst(existing.UsageNode);
				return ValueTask.CompletedTask;
			}

			var usageNode = usage.AddFirst(key);
			items.Add(key, new CacheItem(entry, usageNode));

			if (items.Count > capacity && usage.Last is { } leastRecentlyUsed)
			{
				usage.RemoveLast();
				items.Remove(leastRecentlyUsed.Value);
			}
		}

		return ValueTask.CompletedTask;
	}

	public ValueTask InvalidateAsync(
		StorableReference reference,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(reference);
		cancellationToken.ThrowIfCancellationRequested();

		lock (syncRoot)
		{
			var keys = items.Keys
				.Where(key => key.SourceId == reference.SourceId
					&& StringComparer.Ordinal.Equals(key.ItemId, reference.ItemId))
				.ToArray();

			foreach (var key in keys)
			{
				var item = items[key];
				usage.Remove(item.UsageNode);
				items.Remove(key);
			}
		}

		return ValueTask.CompletedTask;
	}

	private sealed class CacheItem
	{
		public CacheItem(
			ThumbnailCacheEntry entry,
			LinkedListNode<ThumbnailCacheKey> usageNode)
		{
			Entry = entry;
			UsageNode = usageNode;
		}

		public ThumbnailCacheEntry Entry { get; set; }

		public LinkedListNode<ThumbnailCacheKey> UsageNode { get; }
	}
}
