// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Archives;

/// <summary>
/// Supplies credentials without coupling Files.Core to a UI framework.
/// </summary>
public interface IArchiveCredentialProvider
{
	ValueTask<ArchiveCredential?> GetCredentialAsync(
		ArchiveCredentialChallenge challenge,
		CancellationToken cancellationToken = default);
}
