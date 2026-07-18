// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;
using Files.Core.Thumbnails;
using OwlCore.Storage;

namespace Files.Core.Models;

public sealed class FileModel : StorableModel, IFileModel
{
	public FileModel(IStorageSource source, IFile file, IThumbnailSource? thumbnailSource = null)
		: base(source, file, thumbnailSource)
	{
		File = file;
	}

	public IFile File { get; }
}
