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
	private readonly WindowsStorableFactory storableFactory;
	private readonly WindowsShellChangeProvider changeProvider;
	private readonly bool ownsScheduler;
	private bool isDisposed;

	public WindowsStorageSource(
		StorageSourceId? sourceId = null,
		string displayName = "Windows",
		IEnumerable<Guid>? rootFolderIds = null,
		IWindowsShellScheduler? scheduler = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

		SourceId = sourceId ?? new StorageSourceId(DefaultProviderId);
		DisplayName = displayName;
		this.rootFolderIds = Array.AsReadOnly(
			(rootFolderIds ?? [FOLDERID.FOLDERID_ComputerFolder]).ToArray());
		Scheduler = scheduler ?? new WindowsShellScheduler();
		ownsScheduler = scheduler is null;
		storableFactory = new WindowsStorableFactory(Scheduler);
		changeProvider = new WindowsShellChangeProvider(Scheduler);
	}

	public StorageSourceId SourceId { get; }

	public string ProviderId => DefaultProviderId;

	public string DisplayName { get; }

	/// <summary>
	/// Gets the shared scheduler used by Windows-backed capability contributors.
	/// </summary>
	public IWindowsShellScheduler Scheduler { get; }

	internal WindowsShellItemResolver ShellItemResolver => storableFactory.Resolver;

	internal WindowsShellChangeProvider ChangeProvider => changeProvider;

	internal Task<WindowsStorable?> TryCreateFromAbsolutePidlAsync(
		ReadOnlyMemory<byte> absolutePidl,
		CancellationToken cancellationToken = default)
	{
		return storableFactory.TryCreateFromAbsolutePidlAsync(absolutePidl, cancellationToken);
	}

	public async IAsyncEnumerable<IFolder> GetRootsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);

		foreach (var rootFolderId in rootFolderIds)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var root = await storableFactory
				.CreateAsync(rootFolderId, cancellationToken)
				.ConfigureAwait(false);

			if (root is WindowsFolder folder)
			{
				yield return folder;
				continue;
			}

			throw new InvalidOperationException(
				$"Known folder '{rootFolderId}' did not resolve to a folder.");
		}
	}

	public bool CanResolve(StorageAddress address)
	{
		ArgumentNullException.ThrowIfNull(address);

		return address.Scheme.Equals(ShellAddressScheme, StringComparison.OrdinalIgnoreCase)
			|| address.Scheme.Equals(FileAddressScheme, StringComparison.OrdinalIgnoreCase);
	}

	public async ValueTask<IStorable> ResolveAsync(
		StorageAddress address,
		CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);
		ArgumentNullException.ThrowIfNull(address);

		if (!CanResolve(address))
		{
			throw new ArgumentException(
				$"Address scheme '{address.Scheme}' is not supported.",
				nameof(address));
		}

		return await storableFactory
			.CreateAsync(address.Value, cancellationToken)
			.ConfigureAwait(false);
	}

	public async ValueTask<IStorable> ResolveAsync(
		StorableReference reference,
		CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);
		ArgumentNullException.ThrowIfNull(reference);

		if (reference.SourceId != SourceId)
		{
			throw new ArgumentException(
				$"Reference belongs to storage source '{reference.SourceId}'.",
				nameof(reference));
		}

		var storable = await storableFactory
			.TryCreateFromItemIdAsync(
				reference.ItemId,
				reference.LastKnownAddress,
				cancellationToken)
			.ConfigureAwait(false);

		if (storable is not null)
		{
			return storable;
		}

		var lastKnownAddress = reference.LastKnownAddress;
		if (lastKnownAddress is not null && CanResolve(lastKnownAddress))
		{
			var candidate = await storableFactory
				.TryCreateAsync(lastKnownAddress.Value, cancellationToken)
				.ConfigureAwait(false);

			if (candidate is not null
				&& StringComparer.Ordinal.Equals(candidate.Id, reference.ItemId))
			{
				return candidate;
			}
		}

		throw new FileNotFoundException(
			"The Windows Shell item could not be resolved.",
			reference.ItemId);
	}

	public async ValueTask DisposeAsync()
	{
		if (isDisposed)
		{
			return;
		}

		isDisposed = true;
		await changeProvider.DisposeAsync().ConfigureAwait(false);

		if (ownsScheduler)
		{
			await Scheduler.DisposeAsync().ConfigureAwait(false);
		}

		GC.SuppressFinalize(this);
	}
}
