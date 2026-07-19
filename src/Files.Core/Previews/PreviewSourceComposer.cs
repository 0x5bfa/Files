// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;

namespace Files.Core.Previews;

/// <summary>
/// Builds a priority-ordered preview router from all preview candidates.
/// </summary>
public sealed class PreviewSourceComposer : ICapabilityComposer<IPreviewSource>
{
	public IPreviewSource? Compose(
		CapabilityContext context,
		IReadOnlyList<CapabilityCandidate<IPreviewSource>> candidates)
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
			_ => new RoutedPreviewSource(sources),
		};
	}

	private sealed class RoutedPreviewSource : IPreviewSource
	{
		private readonly IReadOnlyList<IPreviewSource> sources;

		public RoutedPreviewSource(IReadOnlyList<IPreviewSource> sources)
		{
			this.sources = sources;
		}

		public async ValueTask<PreviewResult?> GetPreviewAsync(
			PreviewRequest request,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);

			foreach (var source in sources)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var result = await source
					.GetPreviewAsync(request, cancellationToken)
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
