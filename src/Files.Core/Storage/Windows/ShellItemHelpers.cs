// Copyright (c) Files Community
// Licensed under the MIT License.

using Windows.Win32;
using Windows.Win32.System.SystemServices;
using Windows.Win32.UI.Shell;

namespace Files.Core.Storage.Windows;

internal static unsafe class ShellItemHelpers
{
	public static WindowsStorableSnapshot CreateSnapshot(
		IShellItem shellItem,
		IWindowsItemIdentityProvider identityProvider)
	{
		ArgumentNullException.ThrowIfNull(shellItem);
		ArgumentNullException.ThrowIfNull(identityProvider);

		var result = shellItem.GetAttributes(
			SFGAO_FLAGS.SFGAO_FOLDER | SFGAO_FLAGS.SFGAO_FILESYSTEM,
			out var attributes);
		result.ThrowOnFailure();

		var parsingName = GetRequiredDisplayName(
			shellItem,
			SIGDN.SIGDN_DESKTOPABSOLUTEPARSING);
		var name = TryGetDisplayName(shellItem, SIGDN.SIGDN_PARENTRELATIVEFORUI)
			?? TryGetDisplayName(shellItem, SIGDN.SIGDN_NORMALDISPLAY)
			?? parsingName;
		var fileSystemPath = (attributes & SFGAO_FLAGS.SFGAO_FILESYSTEM) != 0
			? TryGetDisplayName(shellItem, SIGDN.SIGDN_FILESYSPATH)
			: null;
		var itemId = identityProvider.GetItemId(
			shellItem,
			parsingName,
			fileSystemPath);

		return new WindowsStorableSnapshot(
			itemId,
			parsingName,
			name,
			fileSystemPath,
			(attributes & SFGAO_FLAGS.SFGAO_FOLDER) != 0);
	}

	public static string GetRequiredDisplayName(IShellItem shellItem, SIGDN format)
	{
		return TryGetDisplayName(shellItem, format)
			?? throw new InvalidOperationException($"The Shell item does not expose a '{format}' display name.");
	}

	public static string? TryGetDisplayName(IShellItem shellItem, SIGDN format)
	{
		var result = shellItem.GetDisplayName(format, out var displayName);

		if (result.Failed)
		{
			return null;
		}

		try
		{
			var value = displayName.ToString();
			return string.IsNullOrWhiteSpace(value) ? null : value;
		}
		finally
		{
			PInvoke.CoTaskMemFree(displayName.Value);
		}
	}
}
