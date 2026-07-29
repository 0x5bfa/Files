// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.ItemFeatures;
using Files.Core.Storage;
using OwlCore.Storage;

namespace Files.Core.Models;

public sealed class FileModel : StorableModel, IFileModel
{
	public FileModel(
		IFile file,
		StorableReference reference,
		IItemFeatures features)
		: base(file, reference, features)
	{
		File = file;
	}

	public IFile File { get; }
}
