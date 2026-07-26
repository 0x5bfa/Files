// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Ftp;

/// <summary>
/// Supplies one credential from application-owned memory.
/// </summary>
public sealed class StaticFtpCredentialProvider : IFtpCredentialProvider
{
	private readonly FtpCredential credential;

	public StaticFtpCredentialProvider(FtpCredential credential)
	{
		ArgumentNullException.ThrowIfNull(credential);
		this.credential = credential;
	}

	public ValueTask<FtpCredential?> GetCredentialAsync(
		FtpCredentialRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult<FtpCredential?>(credential);
	}
}
