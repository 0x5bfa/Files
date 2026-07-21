// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;

namespace Files.Core.Capabilities.Thumbnails;

/// <summary>
/// Builds a priority-ordered fallback chain from all thumbnail candidates.
/// </summary>
public sealed class ThumbnailSourceComposer : ICapabilityComposer<IThumbnailSource>
{
	public IThumbnailSource? Compose(
		CapabilityContext context,
		IReadOnlyList<CapabilityCandidate<IThumbnailSource>> candidates)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(candidates);

		var sources = candidates
			.OrderByDescending(static candidate => candidate.Priority)
			.Select(static candidate => candidate.Capability)
			.ToArray();

		return sources.Length switch
		{
			0 => null,
			1 => sources[0],
			_ => new FallbackThumbnailSource(sources),
		};
	}

	private sealed class FallbackThumbnailSource : IThumbnailSource
	{
		private readonly IReadOnlyList<IThumbnailSource> sources;

		public FallbackThumbnailSource(IReadOnlyList<IThumbnailSource> sources)
		{
			this.sources = sources;
		}

		public async ValueTask<ThumbnailResult?> GetThumbnailAsync(
			ThumbnailRequest request,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);

			foreach (var source in sources)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var result = await source
					.GetThumbnailAsync(request, cancellationToken)
					.ConfigureAwait(false);

				if (result is not null)
				{
					return result;
				}
			}

			return null;
		}
	}
}
