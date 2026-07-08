// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Files.App.Controls;

public partial class SidebarView2
{
	internal bool IsNavigationViewItemDisplayMode => ItemDisplayMode == SidebarViewItemDisplayMode.NavigationView;

	private bool TryCollectNavigationViewItem(
		SidebarViewFlatTree flatTree,
		ISidebarItemModel item,
		IList<FlatSidebarItem> destination)
	{
		if (!IsNavigationViewItemDisplayMode)
			return false;

		var flatItem = new FlatSidebarItem(item, 0);
		destination.Add(flatItem);
		RegisterVisibleItem(flatTree, flatItem);
		return true;
	}

	private bool TryUpdateNavigationViewItem(SidebarViewFlatTree flatTree, ISidebarItemModel item, string? propertyName)
	{
		if (!IsNavigationViewItemDisplayMode)
			return false;

		if (string.IsNullOrEmpty(propertyName) || propertyName == nameof(ISidebarItemModel.Children))
			RegisterChildCollection(flatTree, item);

		QueueUpdatePreparedMenuItems();
		return true;
	}

	private bool TryRefreshNavigationViewChildren(SidebarViewFlatTree flatTree, ISidebarItemModel item)
	{
		if (!IsNavigationViewItemDisplayMode)
			return false;

		RegisterChildCollection(flatTree, item);
		QueueUpdatePreparedMenuItems();
		return true;
	}
}
