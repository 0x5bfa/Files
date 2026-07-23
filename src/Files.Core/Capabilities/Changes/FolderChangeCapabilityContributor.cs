// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Runtime.Versioning;
using Files.Core.Storage.Windows;

namespace Files.Core.Capabilities.Changes;

/// <summary>
/// Creates Windows Shell folder-change capabilities for Windows folders.
/// </summary>
[SupportedOSPlatform("windows6.0.6000")]
public sealed class FolderChangeCapabilityContributor : ICapabilityContributor<IFolderChangeSource>
{
	public IFolderChangeSource? Create(CapabilityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.Source is WindowsStorageSource source
			&& context.CoreModel is WindowsFolder folder
			? new WindowsFolderChangeSource(source, folder.Locator)
			: null;
	}
}
