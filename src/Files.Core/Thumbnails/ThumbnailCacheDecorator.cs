// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;
using Files.Core.Capabilities;
using Files.Core.Storage;

namespace Files.Core.Thumbnails;

/// <summary>
/// Wraps a composed thumbnail source with a shared, composition-root-owned cache.
/// </summary>
public sealed class ThumbnailCacheDecorator : ICapabilityDecorator<IThumbnailSource>
{
	private readonly IThumbnailCache cache;

	public ThumbnailCacheDecorator(IThumbnailCache cache)
	{
		ArgumentNullException.ThrowIfNull(cache);
		this.cache = cache;
	}

	public IThumbnailSource Decorate(
		CapabilityContext context,
		IThumbnailSource capability)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(capability);

		return new CachedThumbnailSource(context.Reference, capability, cache);
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

			await using (result.ConfigureAwait(false))
			{
				using var buffer = new MemoryStream();
				await result.Content
					.CopyToAsync(buffer, cancellationToken)
					.ConfigureAwait(false);

				var entry = new ThumbnailCacheEntry(
					buffer.ToArray(),
					result.ContentType,
					result.IsFallback);
				await cache.SetAsync(key, entry, cancellationToken).ConfigureAwait(false);
				return entry.CreateResult();
			}
		}
	}
}
