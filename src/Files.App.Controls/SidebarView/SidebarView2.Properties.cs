// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Media;

namespace Files.App.Controls;

public partial class SidebarView2
{
	[GeneratedDependencyProperty]
	public partial object? Content { get; set; }

	[GeneratedDependencyProperty]
	public partial DataTemplate? ContentTemplate { get; set; }

	[GeneratedDependencyProperty(DefaultValue = true)]
	public partial bool IsPaneOpen { get; set; }

	[GeneratedDependencyProperty(DefaultValue = 240d)]
	public partial double OpenPaneLength { get; set; }

	[GeneratedDependencyProperty(DefaultValue = -240d)]
	public partial double NegativeOpenPaneLength { get; set; }

	[GeneratedDependencyProperty(DefaultValue = 56d)]
	public partial double CompactPaneLength { get; set; }

	[GeneratedDependencyProperty(DefaultValue = -56d)]
	public partial double NegativeCompactPaneLength { get; set; }

	[GeneratedDependencyProperty(DefaultValue = SidebarDisplayMode.Expanded)]
	public partial SidebarDisplayMode DisplayMode { get; set; }

	[GeneratedDependencyProperty(DefaultValue = true)]
	public partial bool CanResizePane { get; set; }

	[GeneratedDependencyProperty]
	public partial Brush? PaneBackground { get; set; }

	[GeneratedDependencyProperty]
	public partial object? PaneHeader { get; set; }

	[GeneratedDependencyProperty]
	public partial object? PaneFooter { get; set; }

	[GeneratedDependencyProperty]
	public partial object? PaneContent { get; set; }

	[GeneratedDependencyProperty]
	public partial object? MenuItemsSource { get; set; }

	[GeneratedDependencyProperty]
	public partial DataTemplate? MenuItemTemplate { get; set; }

	[GeneratedDependencyProperty]
	public partial DataTemplateSelector? MenuItemTemplateSelector { get; set; }

	[GeneratedDependencyProperty]
	public partial object? FooterMenuItemsSource { get; set; }

	[GeneratedDependencyProperty]
	public partial DataTemplate? FooterMenuItemTemplate { get; set; }

	[GeneratedDependencyProperty]
	public partial DataTemplateSelector? FooterMenuItemTemplateSelector { get; set; }

	[GeneratedDependencyProperty]
	public partial object? SelectedItem { get; set; }

	partial void OnSelectedItemChanged(object? newValue)
	{
		UpdatePreparedMenuItems();
	}

	partial void OnMenuItemsSourceChanged(object? newValue)
	{
		QueueUpdatePreparedMenuItems();
	}

	partial void OnMenuItemTemplateChanged(DataTemplate? newValue)
	{
		QueueUpdatePreparedMenuItems();
	}

	partial void OnMenuItemTemplateSelectorChanged(DataTemplateSelector? newValue)
	{
		QueueUpdatePreparedMenuItems();
	}

	partial void OnFooterMenuItemTemplateChanged(DataTemplate? newValue)
	{
		QueueUpdatePreparedMenuItems();
	}

	partial void OnFooterMenuItemTemplateSelectorChanged(DataTemplateSelector? newValue)
	{
		QueueUpdatePreparedMenuItems();
	}

	partial void OnOpenPaneLengthChanged(double newValue)
	{
		NegativeOpenPaneLength = -newValue;
		UpdateOpenPaneLengthColumn();
	}

	partial void OnDisplayModeChanged(SidebarDisplayMode newValue)
	{
		UpdateDisplayMode();
	}

	partial void OnIsPaneOpenChanged(bool newValue)
	{
		UpdateMinimalMode();
	}

	partial void OnCanResizePaneChanged(bool newValue)
	{
		UpdateResizerAvailability();
	}

	partial void OnCompactPaneLengthChanged(double newValue)
	{
		NegativeCompactPaneLength = -newValue;
		UpdateDisplayMode();
	}
}
