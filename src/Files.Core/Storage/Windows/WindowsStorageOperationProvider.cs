// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Files.Core.Storage.Windows;

/// <summary>
/// Executes Windows Shell storage operations.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsStorageOperationProvider : IStorageOperationProvider
{
	private readonly WindowsStorageSource source;

	public WindowsStorageOperationProvider(WindowsStorageSource source)
	{
		ArgumentNullException.ThrowIfNull(source);
		this.source = source;
	}

	public bool CanHandle(StorageOperationRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		return request is RenameOperationRequest rename
			&& rename.Item.SourceId == source.SourceId;
	}

	[SupportedOSPlatform("windows6.0.6000")]
	public async ValueTask<StorageOperationResult> ExecuteAsync(
		StorageOperationRequest request,
		IProgress<StorageOperationProgress>? progress = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (request is not RenameOperationRequest rename
			|| rename.Item.SourceId != source.SourceId)
		{
			return Failed(new NotSupportedException(
				$"The Windows Shell provider cannot handle '{request.GetType().Name}'."));
		}

		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			ValidateNewName(rename.NewName);

			var resolved = await source
				.ResolveAsync(rename.Item, cancellationToken)
				.ConfigureAwait(false);
			if (resolved is not WindowsStorable item
				|| !item.IsFileSystem
				|| item.FileSystemPath is null)
			{
				return Failed(new NotSupportedException(
					"The Windows Shell provider can only rename file-system items."));
			}

			var parentPath = Path.GetDirectoryName(item.FileSystemPath);
			if (string.IsNullOrWhiteSpace(parentPath))
			{
				return Failed(new IOException(
					"The item does not have a resolvable parent directory."));
			}

			progress?.Report(new StorageOperationProgress(0, 1, rename.Item));

			var outcome = await source.ShellItemResolver
				.InvokeOperationAsync(
					item.ParsingName,
					shellItem => ExecuteRename(
						shellItem,
						item.Id,
						rename.NewName),
					cancellationToken)
				.ConfigureAwait(false);

			if (!outcome.Succeeded)
			{
				return Failed(outcome.Error!);
			}

			// Once PerformOperations has returned successfully, cancellation must not
			// turn a committed side effect into an apparent cancellation. Resolve by
			// stable identity so the result reflects the item the Shell actually
			// renamed rather than an unrelated item already present at a guessed path.
			var renamed = await source
				.ResolveAsync(rename.Item, CancellationToken.None)
				.ConfigureAwait(false);
			if (renamed is not IWindowsStorable renamedWindows)
			{
				return Failed(new InvalidOperationException(
					"The renamed Windows Shell item could not be materialized."));
			}

			if (!StringComparer.Ordinal.Equals(
					renamedWindows.Id,
					rename.Item.ItemId)
				|| renamedWindows.FileSystemPath is null
				|| !StringComparer.Ordinal.Equals(
					Path.GetFileName(renamedWindows.FileSystemPath),
					rename.NewName))
			{
				return Failed(new IOException(
					"The Windows Shell did not apply the requested rename to the expected item."));
			}

			var resultItem = new StorableReference(
				source.SourceId,
				renamedWindows.Id,
				renamedWindows.Address);
			progress?.Report(new StorageOperationProgress(1, 1, resultItem));
			return new StorageOperationResult(true, resultItem);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			return Failed(exception);
		}
	}

	[SupportedOSPlatform("windows6.0.6000")]
	private static ShellOperationOutcome ExecuteRename(
		IShellItem shellItem,
		string expectedItemId,
		string newName)
	{
		var currentDescriptor = ShellItemHelpers.CreateDescriptor(
			shellItem,
			new WindowsItemIdentityProvider());
		if (!StringComparer.Ordinal.Equals(
				currentDescriptor.ItemId,
				expectedItemId))
		{
			return new ShellOperationOutcome(
				false,
				new IOException(
					"The Windows Shell rename target no longer identifies the requested item."));
		}

		var createResult = PInvoke.CoCreateInstance(
			typeof(FileOperation).GUID,
			null,
			CLSCTX.CLSCTX_LOCAL_SERVER,
			out IFileOperation? fileOperation);
		if (createResult.Failed || fileOperation is null)
		{
			return Failure(createResult, "The Windows Shell file operation could not be created.");
		}

		var flags = FILEOPERATION_FLAGS.FOF_ALLOWUNDO
			| FILEOPERATION_FLAGS.FOF_NOCONFIRMATION
			| FILEOPERATION_FLAGS.FOF_NOCONFIRMMKDIR
			| FILEOPERATION_FLAGS.FOF_NOERRORUI;
		var result = fileOperation.SetOperationFlags(flags);
		if (result.Failed)
		{
			return Failure(result, "The Windows Shell file operation could not be configured.");
		}

		result = fileOperation.RenameItem(shellItem, newName, null);
		if (result.Failed)
		{
			return Failure(result, "The Windows Shell rename could not be queued.");
		}

		result = fileOperation.PerformOperations();
		if (result.Failed)
		{
			return Failure(result, "The Windows Shell rename failed.");
		}

		result = fileOperation.GetAnyOperationsAborted(out var aborted);
		if (result.Failed)
		{
			return Failure(result, "The Windows Shell rename completion could not be read.");
		}

		return aborted
			? new ShellOperationOutcome(
				false,
				new OperationCanceledException("The Windows Shell rename was aborted."))
			: new ShellOperationOutcome(true, null);
	}

	private static void ValidateNewName(string newName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(newName);

		if (newName is "." or ".."
			|| newName.Length > 255
			|| newName.EndsWith(' ')
			|| newName.EndsWith('.')
			|| newName.Contains(Path.DirectorySeparatorChar)
			|| newName.Contains(Path.AltDirectorySeparatorChar)
			|| newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
			|| IsReservedDosDeviceName(newName))
		{
			throw new ArgumentException(
				"The new name must be a single valid Windows file-system name.",
				nameof(newName));
		}
	}

	private static bool IsReservedDosDeviceName(string newName)
	{
		var extensionIndex = newName.IndexOf('.');
		var stem = extensionIndex < 0
			? newName
			: newName[..extensionIndex];
		return stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
			|| stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
			|| stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
			|| stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
			|| IsNumberedDosDeviceName(stem, "COM")
			|| IsNumberedDosDeviceName(stem, "LPT");
	}

	private static bool IsNumberedDosDeviceName(
		string candidate,
		string prefix)
	{
		return candidate.Length is 4
			&& candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
			&& candidate[3] is >= '1' and <= '9';
	}

	private static ShellOperationOutcome Failure(
		global::Windows.Win32.Foundation.HRESULT result,
		string message)
	{
		return new ShellOperationOutcome(
			false,
			new IOException($"{message} HRESULT={result}."));
	}

	private static StorageOperationResult Failed(Exception exception)
	{
		return new StorageOperationResult(false, null, exception);
	}

	private sealed record ShellOperationOutcome(
		bool Succeeded,
		Exception? Error);
}
