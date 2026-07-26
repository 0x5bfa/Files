// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Ftp;

/// <summary>
/// Indicates that an FTP source needs new credentials.
/// </summary>
public sealed class FtpAuthenticationRequiredException :
	UnauthorizedAccessException
{
	public FtpAuthenticationRequiredException(
		string connectionId,
		string message,
		Exception? innerException = null)
		: base(message, innerException)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
		ConnectionId = connectionId;
	}

	public string ConnectionId { get; }
}
