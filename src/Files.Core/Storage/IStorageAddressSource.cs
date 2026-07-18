// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage;

/// <summary>
/// Supplies a resolvable address without also claiming to be a storage item.
/// </summary>
public interface IStorageAddressSource
{
	StorageAddress Address { get; }
}
