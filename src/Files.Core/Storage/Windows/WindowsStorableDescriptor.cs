// Copyright (c) Files Community
// Licensed under the MIT License.

using OwlCore.Storage;

namespace Files.Core.Storage.Windows;

/// <summary>
/// Describes a Shell item without retaining an apartment-bound COM object.
/// </summary>
internal sealed record WindowsStorableDescriptor(
	string ItemId,
	StorageAddress Address,
	WindowsItemLocator Locator,
	WindowsStorableSnapshot Snapshot);
