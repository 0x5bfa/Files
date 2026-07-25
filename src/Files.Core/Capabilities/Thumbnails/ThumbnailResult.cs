// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities.Thumbnails;

public sealed record ThumbnailResult
{
	public ThumbnailResult(
		ReadOnlyMemory<byte> Content,
		string ContentType,
		bool IsFallback)
	{
		if (Content.IsEmpty)
		{
			throw new ArgumentException(
				"Thumbnail content cannot be empty.",
				nameof(Content));
		}

		ArgumentException.ThrowIfNullOrWhiteSpace(ContentType);

		this.Content = Content;
		this.ContentType = ContentType;
		this.IsFallback = IsFallback;
	}

	public ReadOnlyMemory<byte> Content { get; }

	public string ContentType { get; }

	public bool IsFallback { get; }
}
