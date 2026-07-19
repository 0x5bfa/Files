// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;
using Files.Core.Storage;
using OwlCore.Storage;

namespace Files.Core.Models;

public sealed class FileModel : StorableModel, IFileModel
{
	public FileModel(
		IFile file,
		StorableReference reference,
		ICapabilitySet capabilities)
		: base(file, reference, capabilities)
	{
		File = file;
	}

	public IFile File { get; }
}
