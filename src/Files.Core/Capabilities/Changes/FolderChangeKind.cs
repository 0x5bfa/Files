// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities.Changes;

/// <summary>
/// Describes the kind of change reported for a folder.
/// </summary>
public enum FolderChangeKind
{
	Created,
	Deleted,
	Renamed,
	Updated,
	DirectoryUpdated,
}
