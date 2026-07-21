// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;

namespace Files.Core.Capabilities.Previews;

/// <summary>
/// Owns the stream returned for a preview request.
/// </summary>
public sealed class PreviewResult : IDisposable, IAsyncDisposable
{
	public PreviewResult(Stream content, string? contentType = null)
	{
		ArgumentNullException.ThrowIfNull(content);

		Content = content;
		ContentType = contentType;
	}

	public Stream Content { get; }

	public string? ContentType { get; }

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
