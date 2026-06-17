// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;

namespace Files.App.Controls;

public partial class SidebarViewItem
{
	[GeneratedDependencyProperty]
	public partial object? Item { get; set; }

	[GeneratedDependencyProperty]
	public partial object? ItemValue { get; set; }

	[GeneratedDependencyProperty]
	public partial object? Icon { get; set; }

	[GeneratedDependencyProperty]
	public partial object? Text { get; set; }

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
	public partial int Depth { get; set; }

	[GeneratedDependencyProperty(DefaultValue = 1d)]
	public partial double ContentOpacity { get; set; }

	[GeneratedDependencyProperty]
	public partial bool IsInFlyout { get; set; }

	partial void OnItemChanged(object? newValue)
	{
		SynchronizeItemExpansion();
		UpdateStateFromOwner();
	}

	partial void OnItemValueChanged(object? newValue)
	{
		UpdateSelectionFromOwner();
	}

	partial void OnChildrenChanged(object? newValue)
	{
		UpdateSelectionFromOwner();
		UpdateExpansionState();
	}

	partial void OnDepthChanged(int newValue)
	{
		UpdateTemplateSettings();
	}

	partial void OnIsInFlyoutChanged(bool newValue)
	{
		UpdateTemplateSettings();
		UpdateDisplayModeState();
		UpdateExpansionState();
	}

	partial void OnIsSelectedChanged(bool newValue)
	{
		UpdateSelectionState();
	}

	partial void OnIsExpandedChanged(bool newValue)
	{
		if (TryGetSidebarItemModel(out var model) && model.IsExpanded != newValue)
			model.IsExpanded = newValue;

		UpdateExpansionState();
		UpdateSelectionFromOwner();
		Owner?.OnItemExpandedChanged(this);
	}
}
