// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.ItemFeatures.Previews;

public interface IWindowsPreviewHandlerAssociation
{
	string? QueryPreviewHandler(string normalizedExtension);
}
