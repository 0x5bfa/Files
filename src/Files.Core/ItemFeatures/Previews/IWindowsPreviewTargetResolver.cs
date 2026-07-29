// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;

namespace Files.Core.ItemFeatures.Previews;

public interface IWindowsPreviewTargetResolver
{
	ValueTask<WindowsPreviewTarget> ResolveAsync(
		StorableReference reference,
		CancellationToken cancellationToken = default);
}
