// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using Files.Core.Capabilities;
using Files.Core.Storage;
using OwlCore.Storage;

namespace Files.Core.Models;

public sealed class FolderModel : StorableModel, IFolderModel
{
	private readonly IStorageSource source;
	private readonly IStorableModelFactory modelFactory;

	public FolderModel(
		IStorageSource source,
		IFolder folder,
		IStorableModelFactory modelFactory,
		StorableReference reference,
		ICapabilitySet capabilities)
		: base(folder, reference, capabilities)
	{
		ArgumentNullException.ThrowIfNull(modelFactory);

		this.source = source;
		this.modelFactory = modelFactory;
		Folder = folder;
	}

	public IFolder Folder { get; }

	public async IAsyncEnumerable<IStorableModel> GetItemsAsync(
		StorableType type = StorableType.All,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		await foreach (var item in Folder.GetItemsAsync(type, cancellationToken).ConfigureAwait(false))
		{
			yield return modelFactory.Create(source, item);
		}
	}
}
