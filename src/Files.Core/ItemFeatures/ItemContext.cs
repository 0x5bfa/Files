// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;
using OwlCore.Storage;

namespace Files.Core.ItemFeatures;

/// <summary>
/// Describes the storage item receiving optional features.
/// </summary>
public sealed record ItemContext(
	IStorageSource Source,
	IStorable CoreModel,
	StorableReference Reference);
