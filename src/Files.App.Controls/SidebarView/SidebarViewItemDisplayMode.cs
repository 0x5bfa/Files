// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App.Controls;

/// <summary>
/// Defines how <see cref="SidebarView2"/> displays hierarchical menu items.
/// </summary>
public enum SidebarViewItemDisplayMode
{
	/// <summary>
	/// Menu items are displayed like NavigationView items, with only root items in the pane list.
	/// </summary>
	NavigationView,

	/// <summary>
	/// Menu items are displayed like TreeView items, with expanded children flattened into the pane list.
	/// </summary>
	TreeView,
}
