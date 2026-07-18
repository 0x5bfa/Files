// Copyright (c) Files Community
// Licensed under the MIT License.

using Windows.Win32;
using Windows.Win32.System.SystemServices;
using Windows.Win32.UI.Shell;

namespace Files.Core.Storage.Windows;

internal static unsafe class ShellItemHelpers
{
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

	public static string? TryGetFileSystemPath(IShellItem shellItem)
	{
		var result = shellItem.GetAttributes(SFGAO_FLAGS.SFGAO_FILESYSTEM, out var attributes);

		return result.Succeeded && (attributes & SFGAO_FLAGS.SFGAO_FILESYSTEM) != 0
			? TryGetDisplayName(shellItem, SIGDN.SIGDN_FILESYSPATH)
			: null;
	}
}
