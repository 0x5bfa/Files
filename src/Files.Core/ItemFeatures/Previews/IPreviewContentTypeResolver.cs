// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.ItemFeatures;

namespace Files.Core.ItemFeatures.Previews;

public interface IPreviewContentTypeResolver
{
	bool TryResolve(
		ItemContext context,
		out PreviewContentType contentType);
}
