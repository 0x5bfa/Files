// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.ItemFeatures;

namespace Files.Core.ItemFeatures.Previews;

public interface IWindowsShellPreviewPolicy
{
	PreviewBlockReason? GetBlockReason(
		ItemContext context,
		Guid handlerClsid);
}
