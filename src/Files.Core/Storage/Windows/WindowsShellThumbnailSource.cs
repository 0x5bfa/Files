// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Runtime.Versioning;
using Files.Core.ItemFeatures.Thumbnails;
using Windows.Win32.UI.Shell;

namespace Files.Core.Storage.Windows;

[SupportedOSPlatform("windows6.0.6000")]
internal sealed class WindowsShellThumbnailSource : IThumbnailSource
{
	private readonly WindowsShellItemResolver resolver;
	private readonly WindowsShellThumbnailBackend backend;
	private readonly WindowsItemLocator locator;

	public WindowsShellThumbnailSource(
		WindowsShellItemResolver resolver,
		WindowsShellThumbnailBackend backend,
		WindowsItemLocator locator)
	{
		ArgumentNullException.ThrowIfNull(resolver);
		ArgumentNullException.ThrowIfNull(backend);
		ArgumentNullException.ThrowIfNull(locator);

		this.resolver = resolver;
		this.backend = backend;
		this.locator = locator;
	}

	public async ValueTask<ThumbnailResult?> GetThumbnailAsync(
		ThumbnailRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		var payload = await resolver
			.InvokeConcurrentAsync(
				locator,
				shellItem => shellItem is IShellItemImageFactory imageFactory
					? backend.GetThumbnail(imageFactory, request, cancellationToken)
					: null,
				cancellationToken)
			.ConfigureAwait(false);

		return payload is null
			? null
			: new ThumbnailResult(payload.Content, payload.ContentType, payload.IsFallback);
	}
}
