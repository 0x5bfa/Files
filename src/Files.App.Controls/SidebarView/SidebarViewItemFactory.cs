// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App.Controls;

internal sealed class SidebarViewItemFactory
{
	private readonly SidebarView2 _owner;

	public SidebarViewItemFactory(SidebarView2 owner)
	{
		_owner = owner;
	}

	public void Prepare(SidebarViewItem item, SidebarViewItem? parentItem = null)
	{
		item.Owner = _owner;
		item.ParentItem = parentItem;
		item.ApplyOwnerTemplates();
		item.UpdateStateFromOwner();
		item.PrepareChildItems();
	}
}
