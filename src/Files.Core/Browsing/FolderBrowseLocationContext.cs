// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using Files.Core.Models;

namespace Files.Core.Browsing;

/// <summary>
/// Keeps a resolved folder model alive for the duration of a browse location.
/// </summary>
public sealed class FolderBrowseLocationContext : IBrowseLocationContext
{
	private readonly FolderLocation location;
	private readonly IFolderModel folderModel;
	private int isDisposed;

	public FolderBrowseLocationContext(FolderLocation location, IFolderModel folderModel)
	{
		ArgumentNullException.ThrowIfNull(location);
		ArgumentNullException.ThrowIfNull(folderModel);

		this.location = location;
		this.folderModel = folderModel;
	}

	public BrowseLocation Location => location;

	public IStorableModel LocationModel => folderModel;

	public async IAsyncEnumerable<IStorableModel> GetItemsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(Volatile.Read(ref isDisposed) != 0, this);

		await foreach (var item in folderModel.GetItemsAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
		{
			yield return item;
		}
	}

	public ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref isDisposed, 1) == 0)
		{
			folderModel.Dispose();
		}

		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}
}
