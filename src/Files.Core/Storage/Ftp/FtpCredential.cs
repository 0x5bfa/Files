// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Ftp;

/// <summary>
/// Contains transient credentials for one FTP connection attempt.
/// </summary>
public sealed class FtpCredential
{
	public FtpCredential(string userName, string password)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(userName);
		ArgumentNullException.ThrowIfNull(password);

		UserName = userName;
		Password = password;
	}

	public string UserName { get; }

	public string Password { get; }

	public override string ToString() => $"{UserName}:***";
}
