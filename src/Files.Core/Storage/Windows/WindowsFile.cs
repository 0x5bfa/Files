// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;
using OwlCore.Storage;
using Windows.Win32;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Files.Core.Storage.Windows;

public sealed class WindowsFile : WindowsStorable, IChildFile
{
	internal WindowsFile(IShellItem shellItem)
		: base(shellItem)
	{
	}

	public Task<Stream> OpenStreamAsync(FileAccess accessMode, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		_ = ShellItem;

		if (FileSystemPath is { } fileSystemPath)
		{
			Stream stream = new FileStream(fileSystemPath, new FileStreamOptions
			{
				Mode = FileMode.Open,
				Access = accessMode,
				Share = FileShare.ReadWrite | FileShare.Delete,
				Options = FileOptions.Asynchronous,
			});

			return Task.FromResult(stream);
		}

		if (accessMode is not FileAccess.Read)
		{
			throw new UnauthorizedAccessException("The virtual Shell item does not expose a writable file-system path.");
		}

		var result = ShellItem.BindToHandler(null, PInvoke.BHID_Stream, out IStream? shellStream);
		result.ThrowOnFailure();

		if (shellStream is null)
		{
			throw new IOException("The virtual Shell item returned no stream.");
		}

		return Task.FromResult<Stream>(new ShellReadStream(shellStream));
	}
}
