// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;
using Files.Core.Thumbnails;
using OwlCore.Storage;

namespace Files.Core.Models;

/// <summary>
/// Files-specific application model for an OwlCore storage item.
/// </summary>
public interface IStorableModel : IDisposable
{
	IStorable CoreModel { get; }

	StorableReference Reference { get; }

	string Name { get; }

	IThumbnailSource? ThumbnailSource { get; }
}
