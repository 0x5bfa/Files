// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;

namespace Files.Core.Capabilities.Changes;

/// <summary>
/// Describes one folder change without exposing Windows Shell pointers.
/// </summary>
public sealed record FolderChange(
	FolderChangeKind Kind,
	StorableReference? CurrentItem,
	StorableReference? PreviousItem,
	bool RequiresRefresh);
