// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Ftp;

/// <summary>
/// Supplies credentials without storing secrets in a connection profile.
/// </summary>
public interface IFtpCredentialProvider
{
	ValueTask<FtpCredential?> GetCredentialAsync(
		FtpCredentialRequest request,
		CancellationToken cancellationToken = default);
}
