// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.ItemFeatures;

namespace Files.Core.ItemFeatures.Previews;

/// <summary>
/// Loads previews for supported items.
/// </summary>
public interface IPreviewLoader
{
	bool CanLoad(ItemContext context);

	ValueTask<PreviewResult?> GetPreviewAsync(
		PreviewRequest request,
		ItemContext context,
		CancellationToken cancellationToken = default);
}
