// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using Files.Core.Data;
using Files.Core.Models;

namespace Files.Core.Browsing;

public sealed class FolderBrowseLocationHandler : IBrowseLocationHandler
{
	private readonly IFilesDataRoot dataRoot;

	public FolderBrowseLocationHandler(IFilesDataRoot dataRoot)
	{
		ArgumentNullException.ThrowIfNull(dataRoot);
		this.dataRoot = dataRoot;
	}

	public bool CanHandle(BrowseLocation location) => location is FolderLocation;

	public async IAsyncEnumerable<IStorableModel> GetItemsAsync(
		BrowseLocation location,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		if (location is not FolderLocation folderLocation)
		{
			throw new ArgumentException("The location must identify a folder.", nameof(location));
		}

		using var model = await dataRoot.ResolveAsync(folderLocation.Folder, cancellationToken).ConfigureAwait(false);

		if (model is not IFolderModel folderModel)
		{
			throw new InvalidOperationException($"Item '{folderLocation.Folder.ItemId}' is not a folder.");
		}

		await foreach (var child in folderModel.GetItemsAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
		{
			yield return child;
		}
	}
}
