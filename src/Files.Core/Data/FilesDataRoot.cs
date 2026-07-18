// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Files.Core.Models;
using Files.Core.Storage;

namespace Files.Core.Data;

/// <summary>
/// Composes configured storage sources and owns their lifetime.
/// </summary>
public sealed class FilesDataRoot : IFilesDataRoot
{
	private readonly IReadOnlyDictionary<StorageSourceId, IStorageSource> sourcesById;
	private bool isDisposed;

	public FilesDataRoot(IEnumerable<IStorageSource> sources, IStorableModelFactory modelFactory)
	{
		ArgumentNullException.ThrowIfNull(sources);
		ArgumentNullException.ThrowIfNull(modelFactory);

		var sourceList = sources.ToArray();
		var sourceMap = new Dictionary<StorageSourceId, IStorageSource>();

		foreach (var source in sourceList)
		{
			ArgumentNullException.ThrowIfNull(source);

			if (!sourceMap.TryAdd(source.SourceId, source))
			{
				throw new ArgumentException($"A storage source with ID '{source.SourceId}' was supplied more than once.", nameof(sources));
			}
		}

		Sources = Array.AsReadOnly(sourceList);
		sourcesById = new ReadOnlyDictionary<StorageSourceId, IStorageSource>(sourceMap);
		ModelFactory = modelFactory;
	}

	public IReadOnlyList<IStorageSource> Sources { get; }

	public IStorableModelFactory ModelFactory { get; }

	public async IAsyncEnumerable<IFolderModel> GetRootsAsync(
		StorageSourceId sourceId,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var source = GetSource(sourceId);

		await foreach (var root in source.GetRootsAsync(cancellationToken).ConfigureAwait(false))
		{
			var model = ModelFactory.Create(source, root);

			if (model is not IFolderModel folderModel)
			{
				throw new InvalidOperationException($"Storage source '{source.SourceId}' returned a root that is not a folder.");
			}

			yield return folderModel;
		}
	}

	public async ValueTask<IStorableModel> ResolveAsync(
		StorableReference reference,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(reference);

		var source = GetSource(reference.SourceId);
		var coreModel = await source.ResolveAsync(reference, cancellationToken).ConfigureAwait(false);
		return ModelFactory.Create(source, coreModel);
	}

	public async ValueTask DisposeAsync()
	{
		if (isDisposed)
		{
			return;
		}

		isDisposed = true;

		foreach (var source in Sources)
		{
			await source.DisposeAsync().ConfigureAwait(false);
		}

		GC.SuppressFinalize(this);
	}

	private IStorageSource GetSource(StorageSourceId sourceId)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);
		ArgumentNullException.ThrowIfNull(sourceId);

		if (!sourcesById.TryGetValue(sourceId, out var source))
		{
			throw new KeyNotFoundException($"Storage source '{sourceId}' is not registered.");
		}

		return source;
	}
}
