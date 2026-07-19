// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Windows;

/// <summary>
/// Contains apartment-neutral identity and display data copied from a Shell item.
/// </summary>
internal sealed record WindowsStorableSnapshot(
	string ParsingName,
	string Name,
	string? FileSystemPath,
	bool IsFolder);
