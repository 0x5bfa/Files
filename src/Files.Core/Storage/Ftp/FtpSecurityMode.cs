// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Ftp;

/// <summary>
/// Selects the transport security used by an FTP connection.
/// </summary>
public enum FtpSecurityMode
{
	Plain,
	ExplicitTls,
	ImplicitTls,
}
