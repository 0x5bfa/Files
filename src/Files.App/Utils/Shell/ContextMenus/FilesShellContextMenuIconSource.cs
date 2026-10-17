// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

#pragma warning disable IDE0130 // Windows APIs share a namespace across responsibility folders.

using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Files.App.Utils.Shell.ContextMenus;

internal static class FilesShellContextMenuIconSource
{
	private const string PackageFamilyName = "MicrosoftWindows.Client.FileExp_cw5n1h2txyewy";
	private static readonly ImageConverter IconConverter = new();

	internal static byte[]? Read(string canonicalVerb, FilesShellMenuIcon? icon, HBITMAP nativeIcon, int pixelSize, FilesShellContextMenuIconTheme theme)
	{
		if (ReadCloudVerbIcon(canonicalVerb, theme) is { Length: > 0 } cloudVerbIcon)
			return cloudVerbIcon;

		if (!nativeIcon.IsNull && TryEncodeBitmap(nativeIcon) is { Length: > 0 } bitmapData)
			return bitmapData;

		if (icon is null)
			return null;

		var iconData = Read(icon.Path, icon.Index, icon.PackageFullName, pixelSize);
		if (iconData is { Length: > 0 } || string.IsNullOrWhiteSpace(icon.FallbackPath))
			return iconData;

		return Read(icon.FallbackPath, icon.FallbackIndex, icon.FallbackPackageFullName, pixelSize);
	}

	private static byte[]? ReadCloudVerbIcon(string canonicalVerb, FilesShellContextMenuIconTheme theme)
	{
		var filename = canonicalVerb switch
		{
			"MakeAvailableOffline" => "MakeAvailableOffline.svg",
			"MakeAvailableOnline" => "MakeAvailableOnline.svg",
			_ => null,
		};
		if (filename is null || GetPackagePath() is not { Length: > 0 } packagePath)
			return null;

		var themePath = theme switch
		{
			FilesShellContextMenuIconTheme.Dark => Path.Combine("contrast-standard", "theme-dark"),
			FilesShellContextMenuIconTheme.HighContrastWhite => "contrast-white",
			FilesShellContextMenuIconTheme.HighContrastBlack => "contrast-black",
			_ => Path.Combine("contrast-standard", "theme-light"),
		};
		try
		{
			return File.ReadAllBytes(Path.Combine(packagePath, "FileExplorerExtensions", "Assets", "images", themePath, filename));
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine(exception);
			return null;
		}
	}

	private static byte[]? Read(string path, int index, string? packageFullName, int pixelSize)
	{
		var resolvedPath = ResolvePath(path, packageFullName);
		if (resolvedPath is null)
			return null;

		try
		{
			var extension = Path.GetExtension(resolvedPath);
			if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
			{
				return File.ReadAllBytes(resolvedPath);
			}

			IList<int> iconIndexes = index is 0 or int.MinValue ? [index] : [-index, index];
			return Win32Helper.ExtractSelectedIconsFromDLL(resolvedPath, iconIndexes, pixelSize).FirstOrDefault()?.IconData;
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
		{
			Debug.WriteLine(exception);
			return null;
		}
	}

	private static string? ResolvePath(string path, string? packageFullName)
	{
		if (path.StartsWith("@{", StringComparison.Ordinal))
		{
			var buffer = new char[32768];
			if (PInvoke.SHLoadIndirectString(path, buffer).Failed)
				return null;

			path = buffer.AsSpan().TrimEnd('\0').ToString();
		}

		if (Path.IsPathFullyQualified(path))
			return path;

		if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
		{
			if (uri.IsFile)
				return uri.LocalPath;

			if (!string.Equals(uri.Scheme, "ms-appx", StringComparison.OrdinalIgnoreCase))
				return null;

			path = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/', '\\'));
		}
		else if (path.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		else
		{
			path = path.TrimStart('/', '\\');
		}

		if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(packageFullName) || GetPackagePath(packageFullName) is not { Length: > 0 } packagePath)
			return null;

		try
		{
			var resolvedPath = Path.GetFullPath(Path.Combine(packagePath, path.Replace('/', Path.DirectorySeparatorChar)));
			var packageDirectoryPrefix = Path.EndsInDirectorySeparator(packagePath) ? packagePath : string.Concat(packagePath, Path.DirectorySeparatorChar);
			return resolvedPath.StartsWith(packageDirectoryPrefix, StringComparison.OrdinalIgnoreCase) ? resolvedPath : null;
		}
		catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
		{
			return null;
		}
	}

	private static string? GetPackagePath(string packageFullName)
	{
		uint pathLength = 0;
		if (PInvoke.GetPackagePathByFullName(packageFullName, ref pathLength, []) != WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER || pathLength is 0)
			return null;

		var pathBuffer = new char[checked((int)pathLength)];
		var result = PInvoke.GetPackagePathByFullName(packageFullName, ref pathLength, pathBuffer);

		return result == WIN32_ERROR.ERROR_SUCCESS ? new string(pathBuffer).TrimEnd('\0') : null;
	}

	private static unsafe string? GetPackagePath()
	{
		uint count = 0;
		uint bufferLength = 0;
		var result = PInvoke.GetPackagesByPackageFamily(PackageFamilyName, ref count, [], ref bufferLength, []);
		if (result != WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER || count is 0 || bufferLength is 0)
			return null;

		var names = new PWSTR[checked((int)count)];
		var buffer = new char[checked((int)bufferLength)];
		fixed (char* characters = buffer)
		{
			result = PInvoke.GetPackagesByPackageFamily(PackageFamilyName, ref count, names, ref bufferLength, new Span<char>(characters, buffer.Length));
			return result == WIN32_ERROR.ERROR_SUCCESS && count is not 0 ? GetPackagePath(names[0].ToString()) : null;
		}
	}

	private static byte[]? TryEncodeBitmap(HBITMAP handle)
	{
		try
		{
			using var bitmap = Win32Helper.GetBitmapFromHBitmap(handle);
			if (bitmap is null)
				return null;

			bitmap.MakeTransparent();
			return IconConverter.ConvertTo(bitmap, typeof(byte[])) as byte[];
		}
		catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or ExternalException)
		{
			Debug.WriteLine(exception);
			return null;
		}
	}
}

internal enum FilesShellContextMenuIconTheme
{
	Light,
	Dark,
	HighContrastWhite,
	HighContrastBlack,
}
