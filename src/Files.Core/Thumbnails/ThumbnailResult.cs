// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;

namespace Files.Core.Thumbnails;

/// <summary>
/// Owns the stream returned for a thumbnail request.
/// </summary>
public sealed class ThumbnailResult : IDisposable, IAsyncDisposable
{
	public ThumbnailResult(Stream content, string? contentType = null, bool isFallback = false)
	{
		ArgumentNullException.ThrowIfNull(content);

		Content = content;
		ContentType = contentType;
		IsFallback = isFallback;
	}

	public Stream Content { get; }

	public string? ContentType { get; }

	public bool IsFallback { get; }

	public void Dispose()
	{
		Content.Dispose();
		GC.SuppressFinalize(this);
	}

	public async ValueTask DisposeAsync()
	{
		await Content.DisposeAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}
}
