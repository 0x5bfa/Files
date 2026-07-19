// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;
using Windows.Win32;
using Windows.Win32.System.Com;
using Windows.Win32.System.SystemServices;
using Windows.Win32.UI.Shell;

namespace Files.Core.Storage.Windows;

/// <summary>
/// Resolves Shell interfaces on the ordered STA lane and returns managed models or affine wrappers.
/// </summary>
internal sealed unsafe class WindowsStorableFactory
{
	private readonly IWindowsShellScheduler scheduler;

	public WindowsStorableFactory(IWindowsShellScheduler scheduler)
	{
		ArgumentNullException.ThrowIfNull(scheduler);
		this.scheduler = scheduler;
	}

	public Task<WindowsStorable> CreateAsync(
		string parsingName,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(parsingName);

		return scheduler.InvokeAsync<WindowsStorable>(
			() =>
			{
				var result = PInvoke.SHCreateItemFromParsingName(
					parsingName,
					null,
					out IShellItem shellItem);
				result.ThrowOnFailure();
				return Create(ShellItemHelpers.CreateSnapshot(shellItem));
			},
			cancellationToken);
	}

	public Task<WindowsStorable> CreateAsync(
		Guid knownFolderId,
		CancellationToken cancellationToken = default)
	{
		return scheduler.InvokeAsync<WindowsStorable>(
			() =>
			{
				var result = PInvoke.SHGetKnownFolderItem(
					knownFolderId,
					KNOWN_FOLDER_FLAG.KF_FLAG_DEFAULT,
					null,
					out IShellItem shellItem);
				result.ThrowOnFailure();
				return Create(ShellItemHelpers.CreateSnapshot(shellItem));
			},
			cancellationToken);
	}

	public Task<WindowsStorable?> TryCreateAsync(
		string parsingName,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(parsingName))
		{
			return Task.FromResult<WindowsStorable?>(null);
		}

		return scheduler.InvokeAsync<WindowsStorable?>(
			() =>
			{
				var result = PInvoke.SHCreateItemFromParsingName(
					parsingName,
					null,
					out IShellItem shellItem);

				return result.Failed
					? null
					: Create(ShellItemHelpers.CreateSnapshot(shellItem));
			},
			cancellationToken);
	}

	public Task<WindowsFolder?> GetParentAsync(
		WindowsStorableSnapshot snapshot,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		return scheduler.InvokeAsync<WindowsFolder?>(
			() =>
			{
				var createResult = PInvoke.SHCreateItemFromParsingName(
					snapshot.ParsingName,
					null,
					out IShellItem shellItem);
				createResult.ThrowOnFailure();

				var parentResult = shellItem.GetParent(out var parent);

				if (parentResult.Failed)
				{
					return null;
				}

				return Create(ShellItemHelpers.CreateSnapshot(parent)) as WindowsFolder;
			},
			cancellationToken);
	}

	public Task<ShellFolderEnumerator> CreateEnumeratorAsync(
		WindowsStorableSnapshot snapshot,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		return scheduler.InvokeAsync(
			() =>
			{
				var createResult = PInvoke.SHCreateItemFromParsingName(
					snapshot.ParsingName,
					null,
					out IShellItem shellItem);
				createResult.ThrowOnFailure();

				var bindResult = shellItem.BindToHandler(
					null,
					PInvoke.BHID_EnumItems,
					out IEnumShellItems? enumerator);
				bindResult.ThrowOnFailure();

				if (enumerator is null)
				{
					throw new InvalidOperationException("The Shell folder returned no item enumerator.");
				}

				return new ShellFolderEnumerator(scheduler, enumerator);
			},
			cancellationToken);
	}

	public Task<Stream> OpenReadStreamAsync(
		WindowsStorableSnapshot snapshot,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		return scheduler.InvokeAsync<Stream>(
			() =>
			{
				var createResult = PInvoke.SHCreateItemFromParsingName(
					snapshot.ParsingName,
					null,
					out IShellItem shellItem);
				createResult.ThrowOnFailure();

				var bindResult = shellItem.BindToHandler(
					null,
					PInvoke.BHID_Stream,
					out IStream? shellStream);
				bindResult.ThrowOnFailure();

				if (shellStream is null)
				{
					throw new IOException("The virtual Shell item returned no stream.");
				}

				return new ShellReadStream(scheduler, shellStream);
			},
			cancellationToken);
	}

	internal WindowsStorable Create(WindowsStorableSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		return snapshot.IsFolder
			? new WindowsFolder(snapshot, this)
			: new WindowsFile(snapshot, this);
	}
}
