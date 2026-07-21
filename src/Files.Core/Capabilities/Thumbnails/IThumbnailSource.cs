// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities.Thumbnails;

/// <summary>
/// Supplies thumbnails without also claiming to be a storage item.
/// </summary>
public interface IThumbnailSource
{
	ValueTask<ThumbnailResult?> GetThumbnailAsync(ThumbnailRequest request, CancellationToken cancellationToken = default);
}
