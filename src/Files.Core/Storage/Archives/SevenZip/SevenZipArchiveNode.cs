// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Archives.SevenZip;

internal sealed record SevenZipArchiveNode(
	string Path,
	string Name,
	bool IsDirectory,
	int? EntryIndex,
	ulong Size);
