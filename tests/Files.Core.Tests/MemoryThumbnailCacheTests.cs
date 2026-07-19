// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;
using Files.Core.Thumbnails;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Files.Core.Tests;

[TestClass]
public sealed class MemoryThumbnailCacheTests
{
	[TestMethod]
	public async Task EvictsTheLeastRecentlyUsedEntry()
	{
		var cache = new MemoryThumbnailCache(capacity: 2);
		var first = CreateKey("first", 32);
		var second = CreateKey("second", 32);
		var third = CreateKey("third", 32);

		await cache.SetAsync(first, CreateEntry("first"));
		await cache.SetAsync(second, CreateEntry("second"));
		Assert.IsNotNull(await cache.GetAsync(first));
		await cache.SetAsync(third, CreateEntry("third"));

		Assert.IsNotNull(await cache.GetAsync(first));
		Assert.IsNull(await cache.GetAsync(second));
		Assert.IsNotNull(await cache.GetAsync(third));
	}

	[TestMethod]
	public async Task InvalidateRemovesAllSizesAndModesForAnItem()
	{
		var sourceId = new StorageSourceId("test");
		var reference = new StorableReference(sourceId, "item");
		var cache = new MemoryThumbnailCache();

		await cache.SetAsync(new ThumbnailCacheKey(reference, 32, ThumbnailMode.Icon), CreateEntry("icon"));
		await cache.SetAsync(new ThumbnailCacheKey(reference, 128, ThumbnailMode.Content), CreateEntry("content"));
		await cache.SetAsync(new ThumbnailCacheKey(new StorableReference(sourceId, "other"), 32, ThumbnailMode.Icon), CreateEntry("other"));

		await cache.InvalidateAsync(reference);

		Assert.IsNull(await cache.GetAsync(new ThumbnailCacheKey(reference, 32, ThumbnailMode.Icon)));
		Assert.IsNull(await cache.GetAsync(new ThumbnailCacheKey(reference, 128, ThumbnailMode.Content)));
		Assert.IsNotNull(await cache.GetAsync(new ThumbnailCacheKey(new StorableReference(sourceId, "other"), 32, ThumbnailMode.Icon)));
}

	[TestMethod]
	public async Task CacheEntryReturnsAnIndependentReadOnlyStream()
	{
		var cache = new MemoryThumbnailCache();
		var key = CreateKey("item", 64);
		var bytes = new byte[] { 1, 2, 3 };
		await cache.SetAsync(key, new ThumbnailCacheEntry(bytes, "image/test"));
		bytes[0] = 99;

		var entry = await cache.GetAsync(key);
		Assert.IsNotNull(entry);
		CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, entry.Content.ToArray());
		Assert.AreEqual("image/test", entry.ContentType);
}

	private static ThumbnailCacheKey CreateKey(string itemId, int size)
		=> new(new StorageSourceId("test"), itemId, size, ThumbnailMode.Content);

	private static ThumbnailCacheEntry CreateEntry(string value)
		=> new(System.Text.Encoding.UTF8.GetBytes(value), "text/plain");
}
