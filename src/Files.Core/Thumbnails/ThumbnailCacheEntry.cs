// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;

namespace Files.Core.Thumbnails;

/// <summary>
/// Contains an immutable thumbnail payload suitable for a shared cache.
/// </summary>
public sealed class ThumbnailCacheEntry
{
	private readonly byte[] content;

	public ThumbnailCacheEntry(
		byte[] content,
		string? contentType = null,
		bool isFallback = false)
	{
		ArgumentNullException.ThrowIfNull(content);

		this.content = (byte[])content.Clone();
		ContentType = contentType;
		IsFallback = isFallback;
	}

	public ReadOnlyMemory<byte> Content => content;

	public string? ContentType { get; }

	public bool IsFallback { get; }

	internal ThumbnailResult CreateResult()
	{
		return new ThumbnailResult(
			new MemoryStream(content, writable: false),
			ContentType,
			IsFallback);
	}
}
