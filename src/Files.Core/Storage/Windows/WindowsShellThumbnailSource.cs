// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;
using System.Runtime.Versioning;
using Files.Core.Thumbnails;

namespace Files.Core.Storage.Windows;

[SupportedOSPlatform("windows6.0.6000")]
internal sealed class WindowsShellThumbnailSource : IThumbnailSource
{
	private readonly IWindowsShellScheduler scheduler;
	private readonly WindowsShellThumbnailBackend backend;
	private readonly string parsingName;

	public WindowsShellThumbnailSource(
		IWindowsShellScheduler scheduler,
		WindowsShellThumbnailBackend backend,
		string parsingName)
	{
		ArgumentNullException.ThrowIfNull(scheduler);
		ArgumentNullException.ThrowIfNull(backend);
		ArgumentException.ThrowIfNullOrWhiteSpace(parsingName);

		this.scheduler = scheduler;
		this.backend = backend;
		this.parsingName = parsingName;
	}

	public async ValueTask<ThumbnailResult?> GetThumbnailAsync(
		ThumbnailRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		var payload = await scheduler
			.InvokeConcurrentAsync(
				() => backend.GetThumbnail(parsingName, request, cancellationToken),
				cancellationToken)
			.ConfigureAwait(false);

		return payload is null
			? null
			: new ThumbnailResult(
				new MemoryStream(payload.Content, writable: false),
				payload.ContentType,
				payload.IsFallback);
	}
}
