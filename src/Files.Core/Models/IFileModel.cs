// Copyright (c) Files Community
// Licensed under the MIT License.

using OwlCore.Storage;

namespace Files.Core.Models;

public interface IFileModel : IStorableModel
{
	IFile File { get; }
}
