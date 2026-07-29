// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.ItemFeatures;

namespace Files.Core.ItemFeatures.Previews;

public interface IPreviewStreamAccessPolicy
{
	ValueTask<PreviewBlockReason?> GetBlockReasonAsync(
		PreviewRequest request,
		ItemContext context,
		CancellationToken cancellationToken = default);
}
