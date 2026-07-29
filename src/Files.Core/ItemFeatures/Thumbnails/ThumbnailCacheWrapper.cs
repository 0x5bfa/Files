// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.ItemFeatures;
using Files.Core.Storage;

namespace Files.Core.ItemFeatures.Thumbnails;

/// <summary>
/// Wraps a composed thumbnail source with a shared, composition-root-owned cache.
/// </summary>
public sealed class ThumbnailCacheWrapper : IItemFeatureWrapper<IThumbnailSource>
{
	private readonly IThumbnailCache cache;

	public ThumbnailCacheWrapper(IThumbnailCache cache)
	{
		ArgumentNullException.ThrowIfNull(cache);
		this.cache = cache;
	}

	public IThumbnailSource Wrap(
		ItemContext context,
		IThumbnailSource source)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(source);

		return new CachedThumbnailSource(context.Reference, source, cache);
	}

	private sealed class CachedThumbnailSource : IThumbnailSource
	{
		private readonly StorableReference reference;
		private readonly IThumbnailSource innerSource;
		private readonly IThumbnailCache cache;

		public CachedThumbnailSource(
			StorableReference reference,
			IThumbnailSource innerSource,
			IThumbnailCache cache)
		{
			this.reference = reference;
			this.innerSource = innerSource;
			this.cache = cache;
		}

		public async ValueTask<ThumbnailResult?> GetThumbnailAsync(
			ThumbnailRequest request,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);

			var key = new ThumbnailCacheKey(
				reference,
				request.RequestedSize,
				request.Mode);
			var invalidationVersion = await cache
				.GetInvalidationVersionAsync(reference, cancellationToken)
				.ConfigureAwait(false);
			var cached = await cache.GetAsync(key, cancellationToken).ConfigureAwait(false);

			if (cached is not null)
			{
				return cached.CreateResult();
			}

			var result = await innerSource
				.GetThumbnailAsync(request, cancellationToken)
				.ConfigureAwait(false);

			if (result is null)
			{
				return null;
			}

			var entry = new ThumbnailCacheEntry(
				result.Content.ToArray(),
				result.ContentType,
				result.IsFallback);
			await cache
				.TrySetAsync(
					key,
					entry,
					invalidationVersion,
					cancellationToken)
				.ConfigureAwait(false);
			return entry.CreateResult();
		}
	}
}
