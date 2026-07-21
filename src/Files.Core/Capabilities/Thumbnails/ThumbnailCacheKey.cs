// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;

namespace Files.Core.Capabilities.Thumbnails;

/// <summary>
/// Identifies cached content without treating a last-known address as item identity.
/// </summary>
public sealed record ThumbnailCacheKey
{
	public ThumbnailCacheKey(
		StorageSourceId sourceId,
		string itemId,
		int requestedSize,
		ThumbnailMode mode)
	{
		ArgumentNullException.ThrowIfNull(sourceId);
		ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedSize);

		SourceId = sourceId;
		ItemId = itemId;
		RequestedSize = requestedSize;
		Mode = mode;
	}

	public ThumbnailCacheKey(
		StorableReference reference,
		int requestedSize,
		ThumbnailMode mode)
		: this(
			GetReference(reference).SourceId,
			reference.ItemId,
			requestedSize,
			mode)
	{
	}

	public StorageSourceId SourceId { get; }

	public string ItemId { get; }

	public int RequestedSize { get; }

	public ThumbnailMode Mode { get; }

	private static StorableReference GetReference(StorableReference reference)
	{
		ArgumentNullException.ThrowIfNull(reference);
		return reference;
	}
}
