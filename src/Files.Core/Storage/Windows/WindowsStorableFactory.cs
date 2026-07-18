// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using Windows.Win32;
using Windows.Win32.System.SystemServices;
using Windows.Win32.UI.Shell;

namespace Files.Core.Storage.Windows;

internal static unsafe class WindowsStorableFactory
{
	public static WindowsStorable Create(string parsingName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(parsingName);

		var result = PInvoke.SHCreateItemFromParsingName(parsingName, null, out IShellItem shellItem);
		result.ThrowOnFailure();

		return Create(shellItem);
	}

	public static WindowsStorable Create(Guid knownFolderId)
	{
		var result = PInvoke.SHGetKnownFolderItem(
			knownFolderId,
			KNOWN_FOLDER_FLAG.KF_FLAG_DEFAULT,
			null,
			out IShellItem shellItem);
		result.ThrowOnFailure();

		return Create(shellItem);
	}

	public static bool TryCreate(string parsingName, [NotNullWhen(true)] out WindowsStorable? storable)
	{
		if (string.IsNullOrWhiteSpace(parsingName))
		{
			storable = null;
			return false;
		}

		var result = PInvoke.SHCreateItemFromParsingName(parsingName, null, out IShellItem shellItem);

		if (result.Failed)
		{
			storable = null;
			return false;
		}

		storable = Create(shellItem);
		return true;
	}

	public static bool TryCreate(Guid knownFolderId, [NotNullWhen(true)] out WindowsStorable? storable)
	{
		var result = PInvoke.SHGetKnownFolderItem(
			knownFolderId,
			KNOWN_FOLDER_FLAG.KF_FLAG_DEFAULT,
			null,
			out IShellItem shellItem);

		if (result.Failed)
		{
			storable = null;
			return false;
		}

		storable = Create(shellItem);
		return true;
	}

	public static WindowsStorable Create(IShellItem shellItem)
	{
		ArgumentNullException.ThrowIfNull(shellItem);

		var result = shellItem.GetAttributes(SFGAO_FLAGS.SFGAO_FOLDER, out var attributes);
		result.ThrowOnFailure();

		return (attributes & SFGAO_FLAGS.SFGAO_FOLDER) != 0
			? new WindowsFolder(shellItem)
			: new WindowsFile(shellItem);
	}
}
