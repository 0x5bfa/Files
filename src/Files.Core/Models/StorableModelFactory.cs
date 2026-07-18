// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;
using Files.Core.Thumbnails;
using OwlCore.Storage;

namespace Files.Core.Models;

public sealed class StorableModelFactory : IStorableModelFactory
{
	private readonly Func<IStorageSource, IStorable, IThumbnailSource?> resolveThumbnailSource;

	public StorableModelFactory(Func<IStorageSource, IStorable, IThumbnailSource?>? resolveThumbnailSource = null)
	{
		this.resolveThumbnailSource = resolveThumbnailSource ?? ResolveThumbnailSource;
	}

	public IStorableModel Create(IStorageSource source, IStorable coreModel)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(coreModel);

		IThumbnailSource? thumbnailSource = null;

		try
		{
			thumbnailSource = resolveThumbnailSource(source, coreModel);

			return coreModel switch
			{
				IFile file => new FileModel(source, file, thumbnailSource),
				IFolder folder => new FolderModel(source, folder, this, thumbnailSource),
				_ => new StorableModel(source, coreModel, thumbnailSource),
			};
		}
		catch
		{
			if (thumbnailSource is IDisposable disposableThumbnail && !ReferenceEquals(thumbnailSource, coreModel))
			{
				disposableThumbnail.Dispose();
			}

			if (coreModel is IDisposable disposableCoreModel)
			{
				disposableCoreModel.Dispose();
			}

			throw;
		}
	}

	private static IThumbnailSource? ResolveThumbnailSource(IStorageSource source, IStorable coreModel)
	{
		_ = source;
		return coreModel as IThumbnailSource;
	}
}
