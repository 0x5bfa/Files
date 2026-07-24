// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;

namespace Files.Core.Capabilities.Previews;

public interface IWindowsShellPreviewPolicy
{
	PreviewBlockReason? GetBlockReason(
		CapabilityContext context,
		Guid handlerClsid);
}
