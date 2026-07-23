// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage;

/// <summary>
/// Reports aggregate progress for a storage operation.
/// </summary>
public sealed record StorageOperationProgress(
	int CompletedItems,
	int TotalItems,
	StorableReference? CurrentItem = null);
