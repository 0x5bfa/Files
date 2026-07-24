// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities.Previews;

public interface IWindowsPreviewHandlerAssociation
{
	string? QueryPreviewHandler(string normalizedExtension);
}
