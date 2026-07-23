// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;

namespace Files.Core.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsStorageOperationTests
{
	[TestMethod]
	public async Task RenameUsesShellOperationAndReturnsUpdatedReference()
	{
		var directoryPath = Path.Combine(
			Path.GetTempPath(),
			$"Files.Core.OperationTests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directoryPath);
		var oldPath = Path.Combine(directoryPath, "old.txt");
		var newPath = Path.Combine(directoryPath, "new.txt");
		File.WriteAllText(oldPath, "content");

		try
		{
			await using var scheduler = new WindowsShellScheduler();
			await using var source = new WindowsStorageSource(scheduler: scheduler);
			var original = (IWindowsStorable)await source.ResolveAsync(
				new StorageAddress(WindowsStorageSource.FileAddressScheme, oldPath));
			var request = new RenameOperationRequest(
				new StorableReference(source.SourceId, original.Id, original.Address),
				"new.txt");
			var progress = new List<StorageOperationProgress>();
			var service = new StorageOperationService(
				[new WindowsStorageOperationProvider(source)]);

			var result = await service.ExecuteAsync(
				request,
				new Progress<StorageOperationProgress>(progress.Add));

			Assert.IsTrue(result.Succeeded, result.Error?.ToString());
			Assert.IsNull(result.Error);
			Assert.IsNotNull(result.ResultItem);
			Assert.IsFalse(File.Exists(oldPath));
			Assert.IsTrue(File.Exists(newPath));
			Assert.AreEqual(original.Id, result.ResultItem!.ItemId);
			Assert.AreEqual(newPath, result.ResultItem.LastKnownAddress!.Value);
			Assert.AreEqual(2, progress.Count);
			Assert.AreEqual(0, progress[0].CompletedItems);
			Assert.AreEqual(1, progress[^1].CompletedItems);
		}
		finally
		{
			if (File.Exists(oldPath))
			{
				File.Delete(oldPath);
			}

			if (File.Exists(newPath))
			{
				File.Delete(newPath);
			}

			Directory.Delete(directoryPath, recursive: true);
		}
	}

	[TestMethod]
	public async Task RejectsPathTraversalAsFailedResult()
	{
		await using var scheduler = new WindowsShellScheduler();
		await using var source = new WindowsStorageSource(scheduler: scheduler);
		var provider = new WindowsStorageOperationProvider(source);
		var request = new RenameOperationRequest(
			new StorableReference(
				source.SourceId,
				"winfs:v1:missing",
				new StorageAddress(WindowsStorageSource.FileAddressScheme, "C:\\missing.txt")),
			"..\\escape.txt");

		var result = await provider.ExecuteAsync(request);

		Assert.IsFalse(result.Succeeded);
		Assert.IsInstanceOfType<ArgumentException>(result.Error);
	}
}
