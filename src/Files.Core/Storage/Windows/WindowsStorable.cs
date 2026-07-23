// Copyright (c) Files Community
// Licensed under the MIT License.

using OwlCore.Storage;

namespace Files.Core.Storage.Windows;

/// <summary>
/// Represents an apartment-neutral snapshot of a Windows Shell item.
/// </summary>
public abstract class WindowsStorable : IWindowsStorable, IEquatable<WindowsStorable>
{
	private readonly WindowsStorableSnapshot snapshot;

	internal WindowsStorable(
		WindowsStorableSnapshot snapshot,
		WindowsStorableFactory factory)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		ArgumentNullException.ThrowIfNull(factory);

		this.snapshot = snapshot;
		Factory = factory;
		Id = snapshot.ItemId;
		Name = snapshot.Name;
		Address = new StorageAddress(WindowsStorageSource.ShellAddressScheme, snapshot.ParsingName);
	}

	internal WindowsStorableFactory Factory { get; }

	internal WindowsStorableSnapshot Snapshot => snapshot;

	public string Id { get; }

	public string Name { get; }

	public StorageAddress Address { get; }

	public string ParsingName => snapshot.ParsingName;

	public string? FileSystemPath => snapshot.FileSystemPath;

	public bool IsFileSystem => FileSystemPath is not null;

	public async Task<IFolder?> GetParentAsync(CancellationToken cancellationToken = default)
	{
		return await Factory.GetParentAsync(snapshot, cancellationToken).ConfigureAwait(false);
	}

	public bool Equals(WindowsStorable? other)
	{
		return other is not null && StringComparer.OrdinalIgnoreCase.Equals(Id, other.Id);
	}

	public override bool Equals(object? obj) => Equals(obj as WindowsStorable);

	public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Id);

	public override string ToString() => ParsingName;
}
