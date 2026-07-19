// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Previews;

/// <summary>
/// Produces UI-neutral preview content for one item.
/// </summary>
public interface IPreviewSource
{
	ValueTask<PreviewResult?> GetPreviewAsync(
		PreviewRequest request,
		CancellationToken cancellationToken = default);
}
