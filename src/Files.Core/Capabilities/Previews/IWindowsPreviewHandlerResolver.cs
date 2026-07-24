// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;

namespace Files.Core.Capabilities.Previews;

public interface IWindowsPreviewHandlerResolver
{
	ValueTask<Guid?> ResolveAsync(
		CapabilityContext context,
		CancellationToken cancellationToken = default);
}
