// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;

namespace Files.Core.Capabilities.Previews;

public interface IPreviewContentTypeResolver
{
	bool TryResolve(
		CapabilityContext context,
		out PreviewContentType contentType);
}
