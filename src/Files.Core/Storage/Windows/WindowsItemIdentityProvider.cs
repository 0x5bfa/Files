// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.UI.Shell;

namespace Files.Core.Storage.Windows;

/// <summary>
/// Uses the filesystem volume and file index when available and otherwise keeps an explicit address fallback.
/// </summary>
[SupportedOSPlatform("windows6.0.6000")]
internal sealed class WindowsItemIdentityProvider : IWindowsItemIdentityProvider
{
	private const string AddressPrefix = "address:";
	private const FileOptions BackupSemantics = (FileOptions)0x02000000;

	public string GetItemId(
		IShellItem shellItem,
		string parsingName,
		string? fileSystemPath)
	{
		ArgumentNullException.ThrowIfNull(shellItem);
		ArgumentException.ThrowIfNullOrWhiteSpace(parsingName);

		if (fileSystemPath is not null && TryGetFileId(fileSystemPath, out var fileId))
		{
			return $"file:{fileId.VolumeSerialNumber:X8}:{fileId.FileIndex:X16}";
		}

		return $"{AddressPrefix}{parsingName}";
	}

	public bool TryGetParsingName(
		string itemId,
		out string parsingName)
	{
		parsingName = string.Empty;

		if (!itemId.StartsWith(AddressPrefix, StringComparison.Ordinal))
		{
			return false;
		}

		parsingName = itemId[AddressPrefix.Length..];
		return !string.IsNullOrWhiteSpace(parsingName);
	}

	private static bool TryGetFileId(
		string fileSystemPath,
		out WindowsFileId fileId)
	{
		fileId = default;

		try
		{
			using var handle = File.OpenHandle(
				fileSystemPath,
				FileMode.Open,
				FileAccess.Read,
				FileShare.ReadWrite | FileShare.Delete,
				BackupSemantics);

			if (handle.IsInvalid
				|| !PInvoke.GetFileInformationByHandle(handle, out var information))
			{
				return false;
			}

			fileId = new WindowsFileId(
				information.dwVolumeSerialNumber,
				((ulong)information.nFileIndexHigh << 32) | information.nFileIndexLow);
			return true;
		}
		catch (IOException)
		{
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}

	private readonly record struct WindowsFileId(
		uint VolumeSerialNumber,
		ulong FileIndex);
}
