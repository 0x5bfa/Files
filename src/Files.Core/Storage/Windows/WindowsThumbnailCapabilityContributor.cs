// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Runtime.Versioning;
using Files.Core.Capabilities;
using Files.Core.Capabilities.Thumbnails;

namespace Files.Core.Storage.Windows;

/// <summary>
/// Creates a thumbnail source for items resolved by the Windows Shell provider.
/// </summary>
[SupportedOSPlatform("windows6.0.6000")]
public sealed class WindowsThumbnailCapabilityContributor : ICapabilityContributor<IThumbnailSource>
{
	private readonly WindowsShellThumbnailBackend backend;

	public WindowsThumbnailCapabilityContributor(WindowsShellThumbnailBackend backend)
	{
		ArgumentNullException.ThrowIfNull(backend);
		this.backend = backend;
	}

	public IThumbnailSource? Create(CapabilityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (context.Source is not WindowsStorageSource source
			|| context.CoreModel is not WindowsStorable storable)
		{
			return null;
		}

		return new WindowsShellThumbnailSource(
			source.ShellItemResolver,
			backend,
			storable.Locator);
	}
}
