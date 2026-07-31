// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;

namespace Files.App2.ViewModels;

public sealed record BrowseItemViewModel(
	string Name,
	bool IsFolder,
	StorableReference Reference)
{
	public string Kind => IsFolder ? "Folder" : "File";

	public string ReferenceText =>
		Reference.LastKnownAddress?.Value ?? Reference.ItemId;
}
