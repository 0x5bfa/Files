// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using OwlCore.Storage;

namespace Files.Core.Storage.Windows;

public sealed class WindowsFolder : WindowsStorable, IChildFolder
{
	private const int EnumerationBatchSize = 32;

	internal WindowsFolder(
		WindowsStorableSnapshot snapshot,
		WindowsStorableFactory factory)
		: base(snapshot, factory)
	{
	}

	public async IAsyncEnumerable<IStorableChild> GetItemsAsync(
		StorableType type = StorableType.All,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (type is StorableType.None)
		{
			yield break;
		}

		await using var enumerator = await Factory
			.CreateEnumeratorAsync(Snapshot, cancellationToken)
			.ConfigureAwait(false);

		while (true)
		{
			var snapshots = await enumerator
				.ReadNextAsync(EnumerationBatchSize, cancellationToken)
				.ConfigureAwait(false);

			if (snapshots.Count is 0)
			{
				yield break;
			}

			foreach (var snapshot in snapshots)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var include = snapshot.IsFolder
					? type.HasFlag(StorableType.Folder)
					: type.HasFlag(StorableType.File);

				if (include)
				{
					yield return Factory.Create(snapshot);
				}
			}
		}
	}
}
