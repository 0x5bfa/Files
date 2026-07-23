// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Browsing;
using Files.Core.Capabilities.Properties;
using Files.Core.Capabilities.Thumbnails;
using Files.Core.Models;
using Files.Core.ViewSettings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Files.Core.Tests;

[TestClass]
public sealed class BrowsePrefetchCoordinatorTests
{
	[TestMethod]
	public async Task PrefetchesVisibleLookAheadAndRemainingItems()
	{
		var factory = new TestModelFactory();
		var locationModel = factory.CreateModel("folder", "Folder", out _);
		var order = new List<string>();
		var propertySources = new Dictionary<string, TestPropertySource>();
		var thumbnailSources = new Dictionary<string, TestThumbnailSource>();
		var models = new List<IStorableModel>();
		foreach (var id in new[] { "a", "b", "c", "d" })
		{
			var propertySource = new TestPropertySource
			{
				Handler = (_, _) =>
				{
					order.Add(id);
					return ValueTask.FromResult<IReadOnlyDictionary<string, object?>>(
						new Dictionary<string, object?>());
				},
			};
			var thumbnailSource = new TestThumbnailSource();
			propertySources.Add(id, propertySource);
			thumbnailSources.Add(id, thumbnailSource);
			models.Add(factory.CreateModel(
				id,
				id.ToUpperInvariant(),
				out _,
				propertySource: propertySource,
				thumbnailSource: thumbnailSource));
		}

		var resolver = new TestBrowseLocationResolver(models)
		{
			LocationModelFactory = _ => locationModel,
		};
		using var session = new BrowseSessionModel(resolver);
		await session.NavigateAsync(new FolderLocation(locationModel.Reference));

		var settings = new BrowseViewSettings(
			columns: [
				new ViewColumnSettings("System.Size", 120, 0),
				new ViewColumnSettings("System.Hidden", 120, 1, isVisible: false)],
			sortPropertyId: "System.DateModified");
		await using var coordinator = new BrowsePrefetchCoordinator(session);
		coordinator.UpdateViewport(
			new BrowseViewport(1, 1, 1),
			settings,
			session.Generation);

		await WaitUntilAsync(() => order.Count is 4);

		CollectionAssert.AreEqual(new[] { "b", "c", "a", "d" }, order);
		foreach (var id in propertySources.Keys)
		{
			Assert.AreEqual(1, propertySources[id].CallCount);
			CollectionAssert.AreEqual(
				new[] { "System.Size", "System.DateModified" },
				propertySources[id].Requests.Single().ToArray());
			Assert.AreEqual(1, thumbnailSources[id].CallCount);
			Assert.AreEqual(96, thumbnailSources[id].Requests.Single().RequestedSize);
			Assert.AreEqual(
				ThumbnailMode.PreferContent,
				thumbnailSources[id].Requests.Single().Mode);
		}
	}

	[TestMethod]
	public async Task ViewportUpdateCancelsPreviousPrefetch()
	{
		var factory = new TestModelFactory();
		var locationModel = factory.CreateModel("folder", "Folder", out _);
		var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var propertySource = new TestPropertySource
		{
			Handler = async (_, cancellationToken) =>
			{
				entered.TrySetResult(true);
				try
				{
					await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					cancelled.TrySetResult(true);
					throw;
				}

				return new Dictionary<string, object?>();
			},
		};
		var item = factory.CreateModel(
			"item",
			"Item",
			out _,
			propertySource: propertySource);
		var resolver = new TestBrowseLocationResolver([item])
		{
			LocationModelFactory = _ => locationModel,
		};
		using var session = new BrowseSessionModel(resolver);
		await session.NavigateAsync(new FolderLocation(locationModel.Reference));
		await using var coordinator = new BrowsePrefetchCoordinator(session);
		var settings = new BrowseViewSettings(
			columns: [new ViewColumnSettings("System.Size", 120, 0)]);

		coordinator.UpdateViewport(new BrowseViewport(0, 1), settings, session.Generation);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
		coordinator.UpdateViewport(new BrowseViewport(0, 1), settings, session.Generation);

		await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
	}

	[TestMethod]
	public async Task NavigationCancelsOldGenerationBeforeUsingItsResult()
	{
		var factory = new TestModelFactory();
		var firstLocation = factory.CreateModel("first", "First", out _);
		var secondLocation = factory.CreateModel("second", "Second", out _);
		var secondItem = factory.CreateModel("second-item", "Second Item", out _);
		var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var firstPropertySource = new TestPropertySource
		{
			Handler = async (_, cancellationToken) =>
			{
				entered.TrySetResult(true);
				try
				{
					await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					completed.TrySetResult(true);
					return new Dictionary<string, object?>();
				}

				return new Dictionary<string, object?>();
			},
		};
		var firstItemWithSource = factory.CreateModel(
			"first-item",
			"First Item",
			out _,
			propertySource: firstPropertySource);
		var locationModels = new Queue<IStorableModel>([firstLocation, secondLocation]);
		var resolver = new TestBrowseLocationResolver([firstItemWithSource])
		{
			LocationModelFactory = _ => locationModels.Dequeue(),
		};
		using var session = new BrowseSessionModel(resolver);
		await session.NavigateAsync(new FolderLocation(firstLocation.Reference));
		await using var coordinator = new BrowsePrefetchCoordinator(session);
		coordinator.UpdateViewport(
			new BrowseViewport(0, 1),
			new BrowseViewSettings(columns: [new ViewColumnSettings("System.Size", 120, 0)]),
			session.Generation);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

		resolver.Items.Clear();
		resolver.Items.Add(secondItem);
		await session.NavigateAsync(new FolderLocation(secondLocation.Reference));
		await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

		Assert.AreEqual(1, firstPropertySource.CallCount);
		Assert.AreSame(secondItem, session.Items.Single());
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(5);
		while (!condition() && DateTime.UtcNow < timeout)
		{
			await Task.Delay(10).ConfigureAwait(false);
		}

		Assert.IsTrue(condition());
	}
}
