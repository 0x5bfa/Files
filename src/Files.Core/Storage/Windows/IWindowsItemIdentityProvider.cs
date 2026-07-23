// Copyright (c) Files Community
// Licensed under the MIT License.

using Windows.Win32.UI.Shell;

namespace Files.Core.Storage.Windows;

/// <summary>
/// Creates provider-specific identities independently from Shell addresses.
/// </summary>
internal interface IWindowsItemIdentityProvider
{
	string GetItemId(
		IShellItem shellItem,
		string parsingName,
		string? fileSystemPath);

	bool TryGetParsingName(
		string itemId,
		out string parsingName);

	bool IsFileSystemIdentity(string itemId);
}
