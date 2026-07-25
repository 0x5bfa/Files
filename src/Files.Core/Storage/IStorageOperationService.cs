// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage;

/// <summary>
/// Selects a storage operation provider and executes a request.
/// </summary>
public interface IStorageOperationService
{
	bool CanHandle(StorageOperationRequest request);

	ValueTask<StorageOperationResult> ExecuteAsync(
		StorageOperationRequest request,
		IProgress<StorageOperationProgress>? progress = null,
		CancellationToken cancellationToken = default);
}
