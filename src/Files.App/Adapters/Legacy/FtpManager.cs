// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Net;

namespace Files.App.Adapters.Legacy
{
	/// <summary>
	/// Retains credentials for legacy WinRT FTP storage items.
	/// </summary>
	internal static class FtpManager
	{
		public static Dictionary<string, NetworkCredential> Credentials { get; } = [];

		public static NetworkCredential Anonymous { get; } = new("anonymous", "anonymous");
	}
}
