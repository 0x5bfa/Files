// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;

namespace Files.Core.Capabilities.Thumbnails;

/// <summary>
/// Stores materialized thumbnail payloads independently of item model lifetimes.
/// </summary>
public interface IThumbnailCache
{
	ValueTask<ThumbnailCacheEntry?> GetAsync(
		ThumbnailCacheKey key,
		CancellationToken cancellationToken = default);

	ValueTask SetAsync(
		ThumbnailCacheKey key,
		ThumbnailCacheEntry entry,
		CancellationToken cancellationToken = default);

	ValueTask InvalidateAsync(
		StorableReference reference,
		CancellationToken cancellationToken = default);
}
