// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;

namespace Files.Core.Capabilities.Previews;

/// <summary>
/// Owns an encoded or textual preview stream.
/// </summary>
public sealed class StreamPreviewResult : PreviewResult
{
	private Stream? content;

	public StreamPreviewResult(
		Stream content,
		string contentType,
		long? contentLength = null,
		string? suggestedFileName = null)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

		this.content = content;
		ContentType = contentType;
		ContentLength = contentLength;
		SuggestedFileName = suggestedFileName;
	}

	public Stream Content =>
		Volatile.Read(ref content)
		?? throw new ObjectDisposedException(nameof(StreamPreviewResult));

	public string ContentType { get; }

	public long? ContentLength { get; }

	public string? SuggestedFileName { get; }

	public override async ValueTask DisposeAsync()
	{
		var stream = Interlocked.Exchange(ref content, null);
		if (stream is not null)
		{
			await stream.DisposeAsync().ConfigureAwait(false);
		}
	}
}
