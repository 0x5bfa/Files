// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;
using Files.Core.Thumbnails;
using OwlCore.Storage;

namespace Files.Core.Models;

public class StorableModel : IStorableModel
{
	private bool isDisposed;

	public StorableModel(IStorageSource source, IStorable coreModel, IThumbnailSource? thumbnailSource = null)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(coreModel);

		CoreModel = coreModel;
		Reference = new StorableReference(
			source.SourceId,
			coreModel.Id,
			(coreModel as IStorageAddressSource)?.Address);
		Name = coreModel.Name;
		ThumbnailSource = thumbnailSource;
	}

	public IStorable CoreModel { get; }

	public StorableReference Reference { get; }

	public string Name { get; }

	public IThumbnailSource? ThumbnailSource { get; }

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (isDisposed)
		{
			return;
		}

		isDisposed = true;

		if (disposing && ThumbnailSource is IDisposable thumbnailSource && !ReferenceEquals(thumbnailSource, CoreModel))
		{
			thumbnailSource.Dispose();
		}

		if (disposing && CoreModel is IDisposable coreModel)
		{
			coreModel.Dispose();
		}
	}
}
