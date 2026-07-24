// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;

namespace Files.Core.Capabilities.Previews;

/// <summary>
/// Shared backend that can produce previews for capability contexts.
/// </summary>
public interface IPreviewProvider
{
	bool CanProvide(CapabilityContext context);

	ValueTask<PreviewResult?> GetPreviewAsync(
		PreviewRequest request,
		CapabilityContext context,
		CancellationToken cancellationToken = default);
}
