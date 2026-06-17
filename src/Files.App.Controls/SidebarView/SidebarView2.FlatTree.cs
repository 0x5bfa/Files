// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Files.App.Controls;

public partial class SidebarView2
{
	private readonly SidebarViewFlatTree _menuFlatTree = new();
	private readonly SidebarViewFlatTree _footerFlatTree = new();

	internal IReadOnlyList<FlatSidebarItem> MenuVisibleItems => _menuFlatTree.Items;
	internal IReadOnlyList<FlatSidebarItem> FooterVisibleItems => _footerFlatTree.Items;

	private void SetMenuItemsSource(object? source)
	{
		SetFlatTreeSource(_menuFlatTree, source);
	}

	private void SetFooterMenuItemsSource(object? source)
	{
		SetFlatTreeSource(_footerFlatTree, source);
	}

	private void SetFlatTreeSource(SidebarViewFlatTree flatTree, object? source)
	{
		if (ReferenceEquals(flatTree.Source, source))
		{
			RebuildFlatTree(flatTree);
			return;
		}

		if (flatTree.SourceCollectionChanged is not null)
			flatTree.SourceCollectionChanged.CollectionChanged -= FlatTreeSource_CollectionChanged;

		UnregisterVisibleItems(flatTree);

		flatTree.Source = source;
		flatTree.SourceCollectionChanged = source as INotifyCollectionChanged;

		if (flatTree.SourceCollectionChanged is not null)
			flatTree.SourceCollectionChanged.CollectionChanged += FlatTreeSource_CollectionChanged;

		RebuildFlatTree(flatTree);
	}

	private void RebuildFlatTrees()
	{
		RebuildFlatTree(_menuFlatTree);
		RebuildFlatTree(_footerFlatTree);
	}

	private void RebuildFlatTree(SidebarViewFlatTree flatTree)
	{
		UnregisterVisibleItems(flatTree);
		flatTree.Items.Clear();

		foreach (var item in EnumerateModelItems(flatTree.Source))
			CollectVisibleSubtree(flatTree, item, 0, flatTree.Items);

		UpdateSectionGapMargins(flatTree);
		QueueUpdatePreparedMenuItems();
	}

	private void FlatTreeSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (ReferenceEquals(sender, _menuFlatTree.SourceCollectionChanged))
			DispatcherQueue.TryEnqueue(() => RebuildFlatTree(_menuFlatTree));

		if (ReferenceEquals(sender, _footerFlatTree.SourceCollectionChanged))
			DispatcherQueue.TryEnqueue(() => RebuildFlatTree(_footerFlatTree));
	}

	private void FlatTreeItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not ISidebarItemModel item)
			return;

		if (!string.IsNullOrEmpty(e.PropertyName) &&
			e.PropertyName != nameof(ISidebarItemModel.IsExpanded) &&
			e.PropertyName != nameof(ISidebarItemModel.Children))
		{
			return;
		}

		DispatcherQueue.TryEnqueue(() =>
		{
			UpdateFlatTreeItem(_menuFlatTree, item, e.PropertyName);
			UpdateFlatTreeItem(_footerFlatTree, item, e.PropertyName);
		});
	}

	private void FlatTreeChildren_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (sender is not INotifyCollectionChanged collection)
			return;

		DispatcherQueue.TryEnqueue(() =>
		{
			if (_menuFlatTree.ChildCollectionParents.TryGetValue(collection, out var menuParent))
				RefreshFlatTreeChildren(_menuFlatTree, menuParent);

			if (_footerFlatTree.ChildCollectionParents.TryGetValue(collection, out var footerParent))
				RefreshFlatTreeChildren(_footerFlatTree, footerParent);
		});
	}

	private void UpdateFlatTreeItem(SidebarViewFlatTree flatTree, ISidebarItemModel item, string? propertyName)
	{
		if (!flatTree.VisibleItems.TryGetValue(item, out var flatItem))
			return;

		if (string.IsNullOrEmpty(propertyName) || propertyName == nameof(ISidebarItemModel.Children))
			RegisterChildCollection(flatTree, item);

		if (string.IsNullOrEmpty(propertyName) || propertyName == nameof(ISidebarItemModel.IsExpanded))
			UpdateFlatTreeExpansion(flatTree, flatItem);

		if (string.IsNullOrEmpty(propertyName) || propertyName == nameof(ISidebarItemModel.Children))
			RefreshFlatTreeChildren(flatTree, item);

		QueueUpdatePreparedMenuItems();
	}

	private void UpdateFlatTreeExpansion(SidebarViewFlatTree flatTree, FlatSidebarItem flatItem)
	{
		var index = flatTree.Items.IndexOf(flatItem);
		if (index < 0)
			return;

		if (flatItem.Item.IsExpanded)
		{
			if (index + 1 < flatTree.Items.Count && flatTree.Items[index + 1].Depth > flatItem.Depth)
				return;

			InsertVisibleChildren(flatTree, flatItem.Item, flatItem.Depth + 1, index + 1);
		}
		else
		{
			RemoveVisibleDescendants(flatTree, index, flatItem.Depth);
		}

		UpdateSectionGapMargins(flatTree);
	}

	private void RefreshFlatTreeChildren(SidebarViewFlatTree flatTree, ISidebarItemModel item)
	{
		RegisterChildCollection(flatTree, item);

		if (!flatTree.VisibleItems.TryGetValue(item, out var flatItem))
			return;

		var index = flatTree.Items.IndexOf(flatItem);
		if (index < 0)
			return;

		RemoveVisibleDescendants(flatTree, index, flatItem.Depth);

		if (item.IsExpanded)
			InsertVisibleChildren(flatTree, item, flatItem.Depth + 1, index + 1);

		UpdateSectionGapMargins(flatTree);
		QueueUpdatePreparedMenuItems();
	}

	private void InsertVisibleChildren(SidebarViewFlatTree flatTree, ISidebarItemModel item, int depth, int insertIndex)
	{
		var children = new List<FlatSidebarItem>();
		foreach (var child in EnumerateModelItems(item.Children))
			CollectVisibleSubtree(flatTree, child, depth, children);

		for (var index = 0; index < children.Count; index++)
			flatTree.Items.Insert(insertIndex + index, children[index]);
	}

	private void RemoveVisibleDescendants(SidebarViewFlatTree flatTree, int parentIndex, int parentDepth)
	{
		var removeIndex = parentIndex + 1;
		var removeCount = 0;

		while (removeIndex + removeCount < flatTree.Items.Count &&
			flatTree.Items[removeIndex + removeCount].Depth > parentDepth)
		{
			removeCount++;
		}

		for (var index = removeIndex + removeCount - 1; index >= removeIndex; index--)
		{
			UnregisterVisibleItem(flatTree, flatTree.Items[index]);
			flatTree.Items.RemoveAt(index);
		}
	}

	private void CollectVisibleSubtree(
		SidebarViewFlatTree flatTree,
		ISidebarItemModel item,
		int depth,
		IList<FlatSidebarItem> destination)
	{
		if (depth > 0 && IsClosedCompact)
			return;

		var flatItem = new FlatSidebarItem(item, depth);
		destination.Add(flatItem);
		RegisterVisibleItem(flatTree, flatItem);

		if (!item.IsExpanded)
			return;

		foreach (var child in EnumerateModelItems(item.Children))
			CollectVisibleSubtree(flatTree, child, depth + 1, destination);
	}

	private void RegisterVisibleItem(SidebarViewFlatTree flatTree, FlatSidebarItem flatItem)
	{
		if (!flatTree.VisibleItems.TryAdd(flatItem.Item, flatItem))
			return;

		flatItem.Item.PropertyChanged += FlatTreeItem_PropertyChanged;
		RegisterChildCollection(flatTree, flatItem.Item);
	}

	private void UnregisterVisibleItems(SidebarViewFlatTree flatTree)
	{
		foreach (var flatItem in flatTree.VisibleItems.Values.ToList())
			UnregisterVisibleItem(flatTree, flatItem);
	}

	private void UnregisterVisibleItem(SidebarViewFlatTree flatTree, FlatSidebarItem flatItem)
	{
		if (!flatTree.VisibleItems.Remove(flatItem.Item))
			return;

		flatItem.Item.PropertyChanged -= FlatTreeItem_PropertyChanged;

		if (flatTree.ChildCollections.Remove(flatItem.Item, out var collection))
		{
			collection.CollectionChanged -= FlatTreeChildren_CollectionChanged;
			flatTree.ChildCollectionParents.Remove(collection);
		}
	}

	private void RegisterChildCollection(SidebarViewFlatTree flatTree, ISidebarItemModel item)
	{
		if (flatTree.ChildCollections.Remove(item, out var oldCollection))
		{
			oldCollection.CollectionChanged -= FlatTreeChildren_CollectionChanged;
			flatTree.ChildCollectionParents.Remove(oldCollection);
		}

		if (item.Children is not INotifyCollectionChanged newCollection)
			return;

		flatTree.ChildCollections[item] = newCollection;
		flatTree.ChildCollectionParents[newCollection] = item;
		newCollection.CollectionChanged += FlatTreeChildren_CollectionChanged;
	}

	private static void UpdateSectionGapMargins(SidebarViewFlatTree flatTree)
	{
		for (var index = 0; index < flatTree.Items.Count; index++)
		{
			var flatItem = flatTree.Items[index];
			var previousItem = index > 0 ? flatTree.Items[index - 1] : null;
			flatItem.HasExpandedPredecessor = flatItem.Depth == 0 && previousItem?.Depth > 0;
		}
	}

	private static IEnumerable<ISidebarItemModel> EnumerateModelItems(object? source)
	{
		if (source is not IEnumerable items || source is string)
			yield break;

		foreach (var item in items)
		{
			if (item is FlatSidebarItem flatSidebarItem)
			{
				yield return flatSidebarItem.Item;
			}
			else if (item is ISidebarItemModel sidebarItemModel)
			{
				yield return sidebarItemModel;
			}
		}
	}

	private sealed class SidebarViewFlatTree
	{
		public ObservableCollection<FlatSidebarItem> Items { get; } = [];
		public Dictionary<ISidebarItemModel, FlatSidebarItem> VisibleItems { get; } = [];
		public Dictionary<ISidebarItemModel, INotifyCollectionChanged> ChildCollections { get; } = [];
		public Dictionary<INotifyCollectionChanged, ISidebarItemModel> ChildCollectionParents { get; } = [];
		public object? Source { get; set; }
		public INotifyCollectionChanged? SourceCollectionChanged { get; set; }
	}
}
