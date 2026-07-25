// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Models;

namespace Files.Core.Storage.Archives;

public sealed record ArchiveMountRequest
{
	public ArchiveMountRequest(
		IStorageSource source,
		IStorableModel archiveModel,
		ArchiveCredential? credential = null,
		int credentialAttempt = 0,
		IArchiveCredentialProvider? credentialProvider = null)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(archiveModel);
		ArgumentOutOfRangeException.ThrowIfNegative(credentialAttempt);

		Source = source;
		ArchiveModel = archiveModel;
		Credential = credential;
		CredentialAttempt = credentialAttempt;
		CredentialProvider = credentialProvider;
	}

	public IStorageSource Source { get; }

	public IStorableModel ArchiveModel { get; }

	public StorableReference Archive => ArchiveModel.Reference;

	public ArchiveCredential? Credential { get; }

	public int CredentialAttempt { get; }

	public IArchiveCredentialProvider? CredentialProvider { get; }
}
