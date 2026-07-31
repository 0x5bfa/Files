// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App.Bootstrap;
using Files.Core.Storage;
using Files.Core.Storage.Windows;
using OwlCore.Storage;
using System.IO;
using System.Runtime.CompilerServices;

namespace Files.App.Adapters.Core
{
	public interface IHomeFolder : IFolder
	{
		IAsyncEnumerable<IStorableChild> GetQuickAccessFolderAsync(
			CancellationToken cancellationToken = default);

		IAsyncEnumerable<IStorableChild> GetLogicalDrivesAsync(
			CancellationToken cancellationToken = default);

		IAsyncEnumerable<IStorableChild> GetNetworkLocationsAsync(
			CancellationToken cancellationToken = default);

		IAsyncEnumerable<IStorableChild> GetRecentFilesAsync(
			CancellationToken cancellationToken = default);
	}

	internal sealed class CoreHomeFolder : IHomeFolder
	{
		private const string QuickAccessParsingName =
			"shell:::{3936E9E4-D92C-4EEE-A85A-BC16D5EA0819}";
		private const string NetworkLocationsParsingName =
			"shell:::{C5ABBF53-E17F-4121-8900-86626FC2C973}";
		private const string RecentItemsParsingName =
			"shell:::{AE50C081-EBD2-438A-8655-8A092E34987A}";

		private readonly IStorageSource windowsSource;

		public CoreHomeFolder(FilesAppCoreHost host)
		{
			ArgumentNullException.ThrowIfNull(host);

			windowsSource = host.Runtime.DataRoot.Sources.Single(
				source => source.SourceType == WindowsStorageSource.DefaultSourceType);
		}

		public string Id => "Home";

		public string Name => "Home";

		public async IAsyncEnumerable<IStorableChild> GetItemsAsync(
			StorableType type = StorableType.Folder,
			[EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			await foreach (var item in GetQuickAccessFolderAsync(cancellationToken))
				yield return item;

			await foreach (var item in GetLogicalDrivesAsync(cancellationToken))
				yield return item;

			await foreach (var item in GetNetworkLocationsAsync(cancellationToken))
				yield return item;
		}

		public IAsyncEnumerable<IStorableChild> GetQuickAccessFolderAsync(
			CancellationToken cancellationToken = default)
		{
			return EnumerateShellFolderAsync(
				QuickAccessParsingName,
				StorableType.Folder,
				cancellationToken);
		}

		public async IAsyncEnumerable<IStorableChild> GetLogicalDrivesAsync(
			[EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			foreach (var drive in DriveInfo.GetDrives())
			{
				cancellationToken.ThrowIfCancellationRequested();
				var item = await windowsSource.ResolveAsync(
					new StorageAddress(
						WindowsStorageSource.FileAddressScheme,
						drive.Name),
					cancellationToken).ConfigureAwait(false);
				if (item is IStorableChild child)
					yield return child;
			}
		}

		public IAsyncEnumerable<IStorableChild> GetNetworkLocationsAsync(
			CancellationToken cancellationToken = default)
		{
			return EnumerateShellFolderAsync(
				NetworkLocationsParsingName,
				StorableType.Folder,
				cancellationToken);
		}

		public IAsyncEnumerable<IStorableChild> GetRecentFilesAsync(
			CancellationToken cancellationToken = default)
		{
			return EnumerateShellFolderAsync(
				RecentItemsParsingName,
				StorableType.File,
				cancellationToken);
		}

		private async IAsyncEnumerable<IStorableChild> EnumerateShellFolderAsync(
			string parsingName,
			StorableType type,
			[EnumeratorCancellation] CancellationToken cancellationToken)
		{
			var item = await windowsSource.ResolveAsync(
				new StorageAddress(
					WindowsStorageSource.ShellAddressScheme,
					parsingName),
				cancellationToken).ConfigureAwait(false);
			if (item is not IFolder folder)
				yield break;

			await foreach (var child in folder
				.GetItemsAsync(type, cancellationToken)
				.ConfigureAwait(false))
			{
				yield return child;
			}
		}
	}
}
