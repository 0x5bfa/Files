// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;
using System.Runtime.CompilerServices;
using OwlCore.Storage;
using Windows.Win32;
using Windows.Win32.UI.Shell;

namespace Files.Core.Storage.Windows;

/// <summary>
/// Resolves file-system and virtual items through the Windows Shell namespace.
/// </summary>
public sealed class WindowsStorageSource : IStorageSource
{
	public const string DefaultProviderId = "windows-shell";
	public const string FileAddressScheme = "file";
	public const string ShellAddressScheme = "shell";

	private readonly IReadOnlyList<Guid> rootFolderIds;
	private bool isDisposed;

	public WindowsStorageSource(
		StorageSourceId? sourceId = null,
		string displayName = "Windows",
		IEnumerable<Guid>? rootFolderIds = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

		SourceId = sourceId ?? new StorageSourceId(DefaultProviderId);
		DisplayName = displayName;
		this.rootFolderIds = Array.AsReadOnly(
			(rootFolderIds ?? [FOLDERID.FOLDERID_ComputerFolder]).ToArray());
	}

	public StorageSourceId SourceId { get; }

	public string ProviderId => DefaultProviderId;

	public string DisplayName { get; }

	public async IAsyncEnumerable<IFolder> GetRootsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);
		await Task.CompletedTask.ConfigureAwait(false);

		foreach (var rootFolderId in rootFolderIds)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var root = WindowsStorable.Create(rootFolderId);

			if (root is WindowsFolder folder)
			{
				yield return folder;
			}
			else
			{
				root.Dispose();
				throw new InvalidOperationException($"Known folder '{rootFolderId}' did not resolve to a folder.");
			}
		}
	}

	public bool CanResolve(StorageAddress address)
	{
		ArgumentNullException.ThrowIfNull(address);

		return address.Scheme.Equals(ShellAddressScheme, StringComparison.OrdinalIgnoreCase)
			|| address.Scheme.Equals(FileAddressScheme, StringComparison.OrdinalIgnoreCase);
	}

	public ValueTask<IStorable> ResolveAsync(
		StorageAddress address,
		CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);
		ArgumentNullException.ThrowIfNull(address);
		cancellationToken.ThrowIfCancellationRequested();

		if (!CanResolve(address))
		{
			throw new ArgumentException($"Address scheme '{address.Scheme}' is not supported.", nameof(address));
		}

		return ValueTask.FromResult<IStorable>(WindowsStorable.Create(address.Value));
	}

	public ValueTask<IStorable> ResolveAsync(
		StorableReference reference,
		CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);
		ArgumentNullException.ThrowIfNull(reference);
		cancellationToken.ThrowIfCancellationRequested();

		if (reference.SourceId != SourceId)
		{
			throw new ArgumentException($"Reference belongs to storage source '{reference.SourceId}'.", nameof(reference));
		}

		if (WindowsStorable.TryCreate(reference.ItemId, out var storable))
		{
			return ValueTask.FromResult<IStorable>(storable);
		}

		if (reference.LastKnownAddress is not null && CanResolve(reference.LastKnownAddress))
		{
			return ResolveAsync(reference.LastKnownAddress, cancellationToken);
		}

		throw new FileNotFoundException("The Windows Shell item could not be resolved.", reference.ItemId);
	}

	public ValueTask DisposeAsync()
	{
		isDisposed = true;
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}
