// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Archives;

public interface IArchiveBackend
{
	string Id { get; }

	int Priority { get; }

	bool SupportsEncryptedArchives { get; }

	ValueTask<ArchiveMountResult> TryMountAsync(
		ArchiveMountRequest request,
		CancellationToken cancellationToken = default);
}
