// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;
using Files.Core.Capabilities.Changes;
using Files.Core.Storage;
using Files.Core.Storage.Windows;

namespace Files.Core.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsFolderChangeTests
{
	[TestMethod]
	public async Task FolderChangeSourceReportsCreateRenameAndDelete()
	{
		var directoryPath = Path.Combine(
			Path.GetTempPath(),
			$"Files.Core.FolderChangeTests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directoryPath);
		var createdPath = Path.Combine(directoryPath, "created.txt");
		var renamedPath = Path.Combine(directoryPath, "renamed.txt");

		try
		{
			await using var scheduler = new WindowsShellScheduler();
			await using var source = new WindowsStorageSource(scheduler: scheduler);
			var folder = (WindowsFolder)await source.ResolveAsync(
				new StorageAddress(WindowsStorageSource.FileAddressScheme, directoryPath));
			var reference = new StorableReference(
				source.SourceId,
				folder.Id,
				folder.Address);
			using var changeSource = new FolderChangeCapabilityContributor().Create(
				new CapabilityContext(source, folder, reference));

			Assert.IsNotNull(changeSource);
			await using var enumerator = changeSource
				.WatchAsync()
				.GetAsyncEnumerator();

			var firstChangeTask = enumerator.MoveNextAsync().AsTask();
			await Task.Delay(100);
			File.WriteAllText(createdPath, "created");
			Assert.IsTrue(await firstChangeTask.WaitAsync(TimeSpan.FromSeconds(10)));
			var created = enumerator.Current;
			if (created.Kind is not FolderChangeKind.Created)
			{
				created = await ReadUntilAsync(
					enumerator,
					static change => change.Kind is FolderChangeKind.Created);
			}
			Assert.IsNotNull(created.CurrentItem);

			var renamedTask = ReadUntilAsync(
				enumerator,
				static change => change.Kind is FolderChangeKind.Renamed);
			File.Move(createdPath, renamedPath);
			var renamed = await renamedTask;
			Assert.IsNotNull(renamed.CurrentItem);

			var deletedTask = ReadUntilAsync(
				enumerator,
				static change => change.Kind is FolderChangeKind.Deleted);
			File.Delete(renamedPath);
			var deleted = await deletedTask;
			Assert.IsNotNull(deleted.PreviousItem);
		}
		finally
		{
			Directory.Delete(directoryPath, recursive: true);
		}
	}

	[TestMethod]
	public async Task SharedProviderDoesNotDeliverChangesToAnotherFolder()
	{
		var rootPath = Path.Combine(
			Path.GetTempPath(),
			$"Files.Core.FolderChangeIsolationTests-{Guid.NewGuid():N}");
		var leftPath = Path.Combine(rootPath, "left");
		var rightPath = Path.Combine(rootPath, "right");
		Directory.CreateDirectory(leftPath);
		Directory.CreateDirectory(rightPath);

		try
		{
			await using var scheduler = new WindowsShellScheduler();
			await using var source = new WindowsStorageSource(scheduler: scheduler);
			var leftFolder = (WindowsFolder)await source.ResolveAsync(
				new StorageAddress(WindowsStorageSource.FileAddressScheme, leftPath));
			var rightFolder = (WindowsFolder)await source.ResolveAsync(
				new StorageAddress(WindowsStorageSource.FileAddressScheme, rightPath));
			using var leftChanges = CreateChangeSource(source, leftFolder);
			using var rightChanges = CreateChangeSource(source, rightFolder);
			await using var leftEnumerator = leftChanges
				.WatchAsync()
				.GetAsyncEnumerator();
			await using var rightEnumerator = rightChanges
				.WatchAsync()
				.GetAsyncEnumerator();

			var leftChangeTask = leftEnumerator.MoveNextAsync().AsTask();
			var rightChangeTask = rightEnumerator.MoveNextAsync().AsTask();
			await Task.Delay(100);

			var leftFilePath = Path.Combine(leftPath, "left.txt");
			File.WriteAllText(leftFilePath, "left");

			Assert.IsTrue(await leftChangeTask.WaitAsync(TimeSpan.FromSeconds(10)));
			Assert.IsFalse(rightChangeTask.IsCompleted);

			var rightFilePath = Path.Combine(rightPath, "right.txt");
			File.WriteAllText(rightFilePath, "right");
			Assert.IsTrue(await rightChangeTask.WaitAsync(TimeSpan.FromSeconds(10)));
			Assert.IsNotNull(rightEnumerator.Current.CurrentItem);
			StringAssert.Contains(
				rightEnumerator.Current.CurrentItem!.LastKnownAddress!.Value,
				rightFilePath);
		}
		finally
		{
			Directory.Delete(rootPath, recursive: true);
		}
	}

	private static IFolderChangeSource CreateChangeSource(
		WindowsStorageSource source,
		WindowsFolder folder)
	{
		var reference = new StorableReference(
			source.SourceId,
			folder.Id,
			folder.Address);
		var changeSource = new FolderChangeCapabilityContributor().Create(
			new CapabilityContext(source, folder, reference));

		Assert.IsNotNull(changeSource);
		return changeSource!;
	}

	private static async Task<FolderChange> ReadUntilAsync(
		IAsyncEnumerator<FolderChange> enumerator,
		Func<FolderChange, bool> predicate)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

		while (await enumerator.MoveNextAsync().AsTask().WaitAsync(timeout.Token))
		{
			if (predicate(enumerator.Current))
			{
				return enumerator.Current;
			}
		}

		throw new AssertFailedException("The expected folder change was not received.");
	}
}
