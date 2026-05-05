// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;

namespace Files.App.Controls;

public partial class SidebarViewItem
{
	[GeneratedDependencyProperty]
	public partial object? Icon { get; set; }

	[GeneratedDependencyProperty]
	public partial string? Text { get; set; }

	[GeneratedDependencyProperty]
	public partial object? Decorator { get; set; }

	[GeneratedDependencyProperty]
	public partial object? ToolTip { get; set; }

	[GeneratedDependencyProperty]
	public partial bool IsSelected { get; set; }

	[GeneratedDependencyProperty(DefaultValue = true)]
	public partial bool IsExpanded { get; set; }

	[GeneratedDependencyProperty]
	public partial SidebarView2? Owner { get; set; }

	[GeneratedDependencyProperty]
	public partial object? Children { get; set; }

	[GeneratedDependencyProperty]
	public partial DataTemplate? ChildItemTemplate { get; set; }

	[GeneratedDependencyProperty]
	public partial DataTemplateSelector? ChildItemTemplateSelector { get; set; }

	partial void OnChildrenChanged(object? newValue)
	{
		UpdateExpansionState();
	}

	partial void OnIsSelectedChanged(bool newValue)
	{
		UpdateSelectionState();
	}

	partial void OnIsExpandedChanged(bool newValue)
	{
		UpdateExpansionState();
		Owner?.OnItemExpandedChanged(this);
	}
}
