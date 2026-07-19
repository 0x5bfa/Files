// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;
using OwlCore.Storage;

namespace Files.Core.Capabilities;

/// <summary>
/// Describes the storage item for which capabilities are being resolved.
/// </summary>
public sealed record CapabilityContext(
	IStorageSource Source,
	IStorable CoreModel,
	StorableReference Reference);
