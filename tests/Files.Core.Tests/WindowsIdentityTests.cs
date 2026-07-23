// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;
using Files.Core.Storage.Windows;

namespace Files.Core.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsIdentityTests
{
	[TestMethod]
	public async Task FileIdentitySurvivesRenameButChangesAfterRecreate()
	{
		var directoryPath = Path.Combine(Path.GetTempPath(), $"Files.Core.IdentityTests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directoryPath);
		var oldPath = Path.Combine(directoryPath, "old.txt");
		var newPath = Path.Combine(directoryPath, "new.txt");
		File.WriteAllText(oldPath, "original");

		try
		{
			await using var scheduler = new WindowsShellScheduler();
			await using var source = new WindowsStorageSource(scheduler: scheduler);

			var original = (IWindowsStorable)await source.ResolveAsync(
				new StorageAddress(WindowsStorageSource.FileAddressScheme, oldPath));
			var originalReference = new StorableReference(
				source.SourceId,
				 original.Id,
				 original.Address);

			File.Move(oldPath, newPath);

			var renamed = (IWindowsStorable)await source.ResolveAsync(
				new StorageAddress(WindowsStorageSource.FileAddressScheme, newPath));
			Assert.AreNotSame(original, renamed);
			Assert.AreEqual(originalReference.ItemId, renamed.Id);
			Assert.AreNotEqual(originalReference.LastKnownAddress, renamed.Address);

			var resolvedByReference = (IWindowsStorable)await source.ResolveAsync(
				new StorableReference(source.SourceId, original.Id, renamed.Address));
			Assert.AreEqual(original.Id, resolvedByReference.Id);

			File.Delete(newPath);
			File.WriteAllText(newPath, "recreated");

			var recreated = (IWindowsStorable)await source.ResolveAsync(
				new StorageAddress(WindowsStorageSource.FileAddressScheme, newPath));
			Assert.AreNotEqual(renamed.Id, recreated.Id);
		}
		finally
		{
			Directory.Delete(directoryPath, recursive: true);
		}
	}
}
