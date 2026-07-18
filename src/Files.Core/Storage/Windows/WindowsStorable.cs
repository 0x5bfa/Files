// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using OwlCore.Storage;
using Windows.Win32.UI.Shell;

namespace Files.Core.Storage.Windows;

public abstract class WindowsStorable : IWindowsStorable, IEquatable<WindowsStorable>
{
	private IShellItem? shellItem;

	internal WindowsStorable(IShellItem shellItem)
	{
		ArgumentNullException.ThrowIfNull(shellItem);

		this.shellItem = shellItem;
		ParsingName = ShellItemHelpers.GetRequiredDisplayName(shellItem, SIGDN.SIGDN_DESKTOPABSOLUTEPARSING);
		Id = ParsingName;
		Name = ShellItemHelpers.TryGetDisplayName(shellItem, SIGDN.SIGDN_PARENTRELATIVEFORUI)
			?? ShellItemHelpers.TryGetDisplayName(shellItem, SIGDN.SIGDN_NORMALDISPLAY)
			?? ParsingName;
		FileSystemPath = ShellItemHelpers.TryGetFileSystemPath(shellItem);
		Address = new StorageAddress(WindowsStorageSource.ShellAddressScheme, ParsingName);
	}

	internal IShellItem ShellItem
	{
		get => shellItem ?? throw new ObjectDisposedException(nameof(WindowsStorable));
	}

	public string Id { get; }

	public string Name { get; }

	public StorageAddress Address { get; }

	public string ParsingName { get; }

	public string? FileSystemPath { get; }

	public bool IsFileSystem => FileSystemPath is not null;

	public static WindowsStorable Create(string parsingName) => WindowsStorableFactory.Create(parsingName);

	public static WindowsStorable Create(Guid knownFolderId) => WindowsStorableFactory.Create(knownFolderId);

	public static bool TryCreate(string parsingName, [NotNullWhen(true)] out WindowsStorable? storable)
		=> WindowsStorableFactory.TryCreate(parsingName, out storable);

	public static bool TryCreate(Guid knownFolderId, [NotNullWhen(true)] out WindowsStorable? storable)
		=> WindowsStorableFactory.TryCreate(knownFolderId, out storable);

	public Task<IFolder?> GetParentAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var result = ShellItem.GetParent(out var parent);

		if (result.Failed)
		{
			return Task.FromResult<IFolder?>(null);
		}

		var parentItem = WindowsStorableFactory.Create(parent);

		if (parentItem is IFolder parentFolder)
		{
			return Task.FromResult<IFolder?>(parentFolder);
		}

		parentItem.Dispose();
		return Task.FromResult<IFolder?>(null);
	}

	public bool Equals(WindowsStorable? other)
	{
		return other is not null && StringComparer.OrdinalIgnoreCase.Equals(Id, other.Id);
	}

	public override bool Equals(object? obj) => Equals(obj as WindowsStorable);

	public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Id);

	public override string ToString() => ParsingName;

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		shellItem = null;
	}
}
