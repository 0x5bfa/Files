// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage;

/// <summary>
/// Provider identity and an optional locator for an item within a configured storage source.
/// </summary>
public sealed record StorableReference
{
	public StorableReference(StorageSourceId sourceId, string itemId, StorageAddress? lastKnownAddress = null)
	{
		ArgumentNullException.ThrowIfNull(sourceId);
		ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

		SourceId = sourceId;
		ItemId = itemId;
		LastKnownAddress = lastKnownAddress;
	}

	public StorageSourceId SourceId { get; }

	public string ItemId { get; }

	public StorageAddress? LastKnownAddress { get; }
}
