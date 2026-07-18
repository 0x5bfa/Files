// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;
using OwlCore.Storage;

namespace Files.Core.Models;

public interface IStorableModelFactory
{
	IStorableModel Create(IStorageSource source, IStorable coreModel);
}
