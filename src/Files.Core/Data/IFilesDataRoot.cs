// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Models;
using Files.Core.Storage;

namespace Files.Core.Data;

/// <summary>
/// Root of the storage-backed Files application model graph.
/// </summary>
public interface IFilesDataRoot : IAsyncDisposable
{
	IReadOnlyList<IStorageSource> Sources { get; }

	IStorableModelFactory ModelFactory { get; }

	IAsyncEnumerable<IFolderModel> GetRootsAsync(StorageSourceId sourceId, CancellationToken cancellationToken = default);

	ValueTask<IStorableModel> ResolveAsync(StorableReference reference, CancellationToken cancellationToken = default);
}
