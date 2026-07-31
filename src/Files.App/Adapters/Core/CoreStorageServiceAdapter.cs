// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App.Bootstrap;
using Files.Core.Storage;
using Files.Core.Storage.Windows;
using OwlCore.Storage;
using System.IO;

namespace Files.App.Adapters.Core
{
	public interface IStorageService
	{
		Task<IFile> GetFileAsync(
			string id,
			CancellationToken cancellationToken = default);

		Task<IFolder> GetFolderAsync(
			string id,
			CancellationToken cancellationToken = default);

		Task<IStorable?> TryGetStorableAsync(
			string id,
			CancellationToken cancellationToken = default);

		Task<IFile?> TryGetFileAsync(
			string id,
			CancellationToken cancellationToken = default);

		Task<IFolder?> TryGetFolderAsync(
			string id,
			CancellationToken cancellationToken = default);
	}

	/// <summary>
	/// Keeps legacy app consumers on the Core Windows source while their UI contracts are migrated.
	/// </summary>
	internal sealed class CoreStorageServiceAdapter : IStorageService
	{
		private readonly IStorageSource windowsSource;

		public CoreStorageServiceAdapter(FilesAppCoreHost host)
		{
			ArgumentNullException.ThrowIfNull(host);

			windowsSource = host.Runtime.DataRoot.Sources.Single(
				source => source.SourceType == WindowsStorageSource.DefaultSourceType);
		}

		public async Task<IFile> GetFileAsync(
			string id,
			CancellationToken cancellationToken = default)
		{
			return await ResolveAsync(id, cancellationToken).ConfigureAwait(false)
				is IFile file
					? file
					: throw new FileNotFoundException("The Core storage item is not a file.", id);
		}

		public async Task<IFolder> GetFolderAsync(
			string id,
			CancellationToken cancellationToken = default)
		{
			return await ResolveAsync(id, cancellationToken).ConfigureAwait(false)
				is IFolder folder
					? folder
					: throw new DirectoryNotFoundException(
						$"The Core storage item '{id}' is not a folder.");
		}

		public async Task<IStorable?> TryGetStorableAsync(
			string id,
			CancellationToken cancellationToken = default)
		{
			try
			{
				return await ResolveAsync(id, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception exception) when (
				exception is IOException
					or UnauthorizedAccessException
					or ArgumentException)
			{
				return null;
			}
		}

		public async Task<IFile?> TryGetFileAsync(
			string id,
			CancellationToken cancellationToken = default)
		{
			return await TryGetStorableAsync(id, cancellationToken).ConfigureAwait(false)
				as IFile;
		}

		public async Task<IFolder?> TryGetFolderAsync(
			string id,
			CancellationToken cancellationToken = default)
		{
			return await TryGetStorableAsync(id, cancellationToken).ConfigureAwait(false)
				as IFolder;
		}

		private ValueTask<IStorable> ResolveAsync(
			string id,
			CancellationToken cancellationToken)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(id);
			return windowsSource.ResolveAsync(
				new StorageAddress(
					WindowsStorageSource.FileAddressScheme,
					id),
				cancellationToken);
		}
	}
}
