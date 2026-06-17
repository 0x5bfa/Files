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
	public partial object? Header { get; set; }

	[GeneratedDependencyProperty]
	public partial DataTemplate? ContentTemplate { get; set; }

	[GeneratedDependencyProperty(DefaultValue = true)]
	public partial bool IsPaneOpen { get; set; }

	[GeneratedDependencyProperty(DefaultValue = 240d)]
	public partial double OpenPaneLength { get; set; }

	[GeneratedDependencyProperty(DefaultValue = 56d)]
	public partial double CompactPaneLength { get; set; }

	[GeneratedDependencyProperty(DefaultValue = 641d)]
	public partial double CompactModeThresholdWidth { get; set; }

	[GeneratedDependencyProperty(DefaultValue = 1008d)]
	public partial double ExpandedModeThresholdWidth { get; set; }

	[GeneratedDependencyProperty(DefaultValue = SidebarViewDisplayMode.Auto)]
	public partial SidebarViewDisplayMode DisplayMode { get; set; }

	[GeneratedDependencyProperty(DefaultValue = true)]
	public partial bool CanResizePane { get; set; }

	[GeneratedDependencyProperty(DefaultValue = true)]
	public partial bool IsPaneToggleButtonVisible { get; set; }

	[GeneratedDependencyProperty(DefaultValue = SidebarViewBackButtonVisible.Auto)]
	public partial SidebarViewBackButtonVisible IsBackButtonVisible { get; set; }

	[GeneratedDependencyProperty(DefaultValue = false)]
	public partial bool IsBackEnabled { get; set; }

	[GeneratedDependencyProperty]
	public partial Brush? PaneBackground { get; set; }

	[GeneratedDependencyProperty]
	public partial AutoSuggestBox? AutoSuggestBox { get; set; }

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
		SelectedItemContainer = null;
		UpdatePreparedMenuItems();
	}

	partial void OnMenuItemsSourceChanged(object? newValue)
	{
		SetMenuItemsSource(newValue);
	}

	partial void OnFooterMenuItemsSourceChanged(object? newValue)
	{
		SetFooterMenuItemsSource(newValue);
	}

	partial void OnMenuItemTemplateChanged(DataTemplate? newValue)
	{
		UpdateItemsHostTemplates();
		QueueUpdatePreparedMenuItems();
	}

	partial void OnMenuItemTemplateSelectorChanged(DataTemplateSelector? newValue)
	{
		QueueUpdatePreparedMenuItems();
	}

	partial void OnFooterMenuItemTemplateChanged(DataTemplate? newValue)
	{
		UpdateItemsHostTemplates();
		QueueUpdatePreparedMenuItems();
	}

	partial void OnFooterMenuItemTemplateSelectorChanged(DataTemplateSelector? newValue)
	{
		QueueUpdatePreparedMenuItems();
	}

	partial void OnOpenPaneLengthChanged(double newValue)
	{
		UpdateTemplateSettings();
		UpdateSplitViewDisplayMode();
	}

	partial void OnDisplayModeChanged(SidebarViewDisplayMode newValue)
	{
		UpdateAdaptiveDisplayMode(ActualWidth, true);
	}

	partial void OnIsPaneOpenChanged(bool newValue)
	{
		UpdatePaneState();
	}

	partial void OnIsPaneToggleButtonVisibleChanged(bool newValue)
	{
		UpdatePaneButtons();
	}

	partial void OnIsBackButtonVisibleChanged(SidebarViewBackButtonVisible newValue)
	{
		UpdatePaneButtons();
	}

	partial void OnCanResizePaneChanged(bool newValue)
	{
		UpdateResizerAvailability();
	}

	partial void OnAutoSuggestBoxChanged(AutoSuggestBox? newValue)
	{
		UpdatePaneButtons();
	}

	partial void OnCompactPaneLengthChanged(double newValue)
	{
		UpdateTemplateSettings();
		UpdateDisplayMode();
	}

	partial void OnCompactModeThresholdWidthChanged(double newValue)
	{
		UpdateAdaptiveDisplayMode(ActualWidth);
	}

	partial void OnExpandedModeThresholdWidthChanged(double newValue)
	{
		UpdateAdaptiveDisplayMode(ActualWidth);
	}

	partial void OnHeaderChanged(object? newValue)
	{
		UpdateHeaderVisibility();
	}
}
