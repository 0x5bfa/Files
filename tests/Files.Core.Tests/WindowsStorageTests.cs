// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsStorageTests
{
	[TestMethod]
	public async Task FileSystemFolderEnumerationBatchesAndFiltersItems()
	{
		var directoryPath = Path.Combine(Path.GetTempPath(), $"Files.Core.Tests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directoryPath);

		try
		{
			for (var index = 0; index < 40; index++)
			{
				File.WriteAllText(Path.Combine(directoryPath, $"file-{index:D2}.txt"), index.ToString());
			}

			Directory.CreateDirectory(Path.Combine(directoryPath, "folder"));
			await using var scheduler = new WindowsShellScheduler();
			await using var source = new WindowsStorageSource(scheduler: scheduler);
			var folder = await source.ResolveAsync(
				new Files.Core.Storage.StorageAddress("file", directoryPath));
			var coreFolder = (IFolder)folder;

			var all = new List<IStorableChild>();
			await foreach (var item in coreFolder.GetItemsAsync())
			{
				all.Add(item);
			}

			var files = new List<IStorableChild>();
			await foreach (var item in coreFolder.GetItemsAsync(StorableType.File))
			{
				files.Add(item);
			}

			Assert.AreEqual(41, all.Count);
			Assert.AreEqual(40, files.Count);
		Assert.AreEqual(40, all.Count(item => item is IFile));
		Assert.AreEqual(1, all.Count(item => item is IFolder));
			Assert.AreEqual(41, all.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
		}
		finally
		{
			Directory.Delete(directoryPath, recursive: true);
		}
	}

	[TestMethod]
	public async Task FileSystemStreamReadsAndSeeks()
	{
		var directoryPath = Path.Combine(Path.GetTempPath(), $"Files.Core.Tests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directoryPath);
		var filePath = Path.Combine(directoryPath, "content.bin");
		var expected = Enumerable.Range(0, 256).Select(static value => (byte)value).ToArray();
		File.WriteAllBytes(filePath, expected);

		try
		{
			await using var scheduler = new WindowsShellScheduler();
			await using var source = new WindowsStorageSource(scheduler: scheduler);
			var storable = await source.ResolveAsync(
				new Files.Core.Storage.StorageAddress("file", filePath));
			var file = (IFile)storable;
			using var stream = await file.OpenStreamAsync(FileAccess.Read);
			Assert.AreEqual(expected.Length, stream.Length);
			stream.Seek(32, SeekOrigin.Begin);
			var buffer = new byte[16];
			Assert.AreEqual(16, stream.Read(buffer, 0, buffer.Length));
			CollectionAssert.AreEqual(expected.Skip(32).Take(16).ToArray(), buffer);
		}
		finally
		{
			Directory.Delete(directoryPath, recursive: true);
		}
	}

	[TestMethod]
	public async Task InjectedSchedulerIsBorrowedByTheSource()
	{
		await using var scheduler = new WindowsShellScheduler();
		var source = new WindowsStorageSource(scheduler: scheduler);
		await source.DisposeAsync();

		var result = await scheduler.InvokeAsync(static () => true);
		Assert.IsTrue(result);
}
}
