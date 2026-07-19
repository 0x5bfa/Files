// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Browsing;
using Files.Core.ViewSettings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Files.Core.Tests;

[TestClass]
public sealed class BrowseSessionModelTests
{
	[TestMethod]
	public async Task NavigationDisposesPreviousItemsAfterSuccessfulReplacement()
	{
		var factory = new TestModelFactory();
		var first = factory.CreateModel("first", "First", out var firstCore);
		var second = factory.CreateModel("second", "Second", out var secondCore);
		var resolver = new TestBrowseLocationResolver([first]);
		using var session = new BrowseSessionModel(resolver);

		await session.NavigateAsync(new FolderLocation(first.Reference));
		Assert.AreSame(first, session.Items.Single());

		resolver.Items.Clear();
		resolver.Items.Add(second);
		await session.NavigateAsync(new FolderLocation(second.Reference));
		Assert.IsTrue(firstCore.IsDisposed);
		Assert.IsFalse(secondCore.IsDisposed);
	}

	[TestMethod]
	public async Task FailedNavigationKeepsCurrentItemsAndDisposesPartialResults()
	{
		var factory = new TestModelFactory();
		var current = factory.CreateModel("current", "Current", out var currentCore);
		var partial = factory.CreateModel("partial", "Partial", out var partialCore);
		var resolver = new TestBrowseLocationResolver([current]);
		using var session = new BrowseSessionModel(resolver);
		await session.NavigateAsync(new FolderLocation(current.Reference));

		resolver.Items.Clear();
		resolver.Items.Add(partial);
		resolver.Exception = new InvalidOperationException("failure");
		await Assert.ThrowsAsync<InvalidOperationException>(
			async () => await session.NavigateAsync(new FolderLocation(partial.Reference)));

		Assert.IsFalse(currentCore.IsDisposed);
		Assert.IsTrue(partialCore.IsDisposed);
		Assert.IsNotNull(session.Error);
	}

	[TestMethod]
	public async Task ViewSettingsArePersistedByBrowseLocation()
	{
		var factory = new TestModelFactory();
		using var model = factory.CreateModel("folder", "Folder", out _);
		var location = new FolderLocation(model.Reference);
		var settingsStore = new TestViewSettingsStore();
		using var session = new BrowseSessionModel(
			new TestBrowseLocationResolver([]),
			settingsStore);

		await session.NavigateAsync(location);
		var settings = new BrowseViewSettings(ViewLayoutMode.List, sortPropertyId: "name");
		await session.UpdateViewSettingsAsync(settings);

		Assert.AreSame(settings, session.ViewSettings);
		Assert.AreSame(settings, await settingsStore.GetAsync(location));
}
}
