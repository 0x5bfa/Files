// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;

namespace Files.Core.Capabilities.Previews;

public interface IPreviewStreamAccessPolicy
{
	ValueTask<PreviewBlockReason?> GetBlockReasonAsync(
		PreviewRequest request,
		CapabilityContext context,
		CancellationToken cancellationToken = default);
}
