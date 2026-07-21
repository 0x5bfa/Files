// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities.Thumbnails;

public sealed record ThumbnailRequest
{
	public ThumbnailRequest(int requestedSize, ThumbnailMode mode = ThumbnailMode.PreferContent)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedSize);

		RequestedSize = requestedSize;
		Mode = mode;
	}

	public int RequestedSize { get; }

	public ThumbnailMode Mode { get; }
}
