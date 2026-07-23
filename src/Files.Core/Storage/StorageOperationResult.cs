// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage;

/// <summary>
/// Contains the outcome of a storage operation.
/// </summary>
public sealed record StorageOperationResult(
	bool Succeeded,
	StorableReference? ResultItem,
	Exception? Error = null);
