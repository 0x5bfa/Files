// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities.Previews;

public interface IWindowsShellPreviewSessionFactory
{
	ValueTask<IWindowsShellPreviewSession> CreateAsync(
		WindowsShellPreviewResult result,
		WindowsPreviewHost host,
		CancellationToken cancellationToken = default);
}
