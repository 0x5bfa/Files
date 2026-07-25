// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Browsing;
using Files.Core.Storage;

namespace Files.Core.Tests;

[TestClass]
public sealed class StorageIdentityTests
{
	[TestMethod]
	public void LastKnownAddressDoesNotParticipateInReferenceIdentity()
	{
		var sourceId = new StorageSourceId("source");
		var before = new StorableReference(
			sourceId,
			"item",
			new StorageAddress("file", @"C:\before.txt"));
		var after = new StorableReference(
			sourceId,
			"item",
			new StorageAddress("file", @"C:\after.txt"));

		Assert.AreEqual(before, after);
		Assert.AreEqual(before.GetHashCode(), after.GetHashCode());
		Assert.AreEqual(
			new FolderLocation(before),
			new FolderLocation(after));
	}

	[TestMethod]
	public void ItemIdsRemainOpaqueAndCaseSensitive()
	{
		var sourceId = new StorageSourceId("source");
		var lower = new StorableReference(sourceId, "item-a");
		var upper = new StorableReference(sourceId, "ITEM-A");

		Assert.AreNotEqual(lower, upper);
	}
}
