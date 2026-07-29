// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.ItemFeatures;

namespace Files.Core.ItemFeatures.Previews;

public interface IWindowsPreviewHandlerResolver
{
	ValueTask<Guid?> ResolveAsync(
		ItemContext context,
		CancellationToken cancellationToken = default);
}
