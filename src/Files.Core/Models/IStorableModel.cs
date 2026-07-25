// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;
using Files.Core.Storage;
using OwlCore.Storage;

namespace Files.Core.Models;

/// <summary>
/// Files-specific application model for an OwlCore storage item.
/// </summary>
public interface IStorableModel : ICapabilityHost, IDisposable, IAsyncDisposable
{
	IStorable CoreModel { get; }

	StorableReference Reference { get; }

	string Name { get; }

}
