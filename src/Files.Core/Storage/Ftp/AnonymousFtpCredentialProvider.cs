// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Ftp;

/// <summary>
/// Supplies conventional anonymous FTP credentials.
/// </summary>
public sealed class AnonymousFtpCredentialProvider : IFtpCredentialProvider
{
	public static AnonymousFtpCredentialProvider Instance { get; } = new();

	private AnonymousFtpCredentialProvider()
	{
	}

	public ValueTask<FtpCredential?> GetCredentialAsync(
		FtpCredentialRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		cancellationToken.ThrowIfCancellationRequested();

		return ValueTask.FromResult<FtpCredential?>(
			new FtpCredential("anonymous", "anonymous@"));
	}
}
