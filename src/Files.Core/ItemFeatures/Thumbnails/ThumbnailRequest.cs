// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

namespace Files.Core.ItemFeatures.Thumbnails;

public sealed record ThumbnailRequest
{
	public ThumbnailRequest(int requestedSize, ThumbnailMode mode = ThumbnailMode.PreferContent)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedSize);
		if (mode is not ThumbnailMode.Icon
			and not ThumbnailMode.Content
			and not ThumbnailMode.PreferContent)
		{
			throw new ArgumentOutOfRangeException(nameof(mode));
		}

		RequestedSize = requestedSize;
		Mode = mode;
	}

	public int RequestedSize { get; }

	public ThumbnailMode Mode { get; }
}
