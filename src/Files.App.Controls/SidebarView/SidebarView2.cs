// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using System.Collections;
using System.Numerics;
using Windows.System;
using Windows.UI.Core;

namespace Files.App.Controls;

[ContentProperty(Name = nameof(Content))]
public partial class SidebarView2 : Control
{
	private const double CompactMaxWidth = 200;
	private const string TemplatePartNamePaneColumnDefinition = "PART_PaneColumnDefinition";
	private const string TemplatePartNamePaneColumnGrid = "PART_PaneColumnGrid";
	private const string TemplatePartNamePaneColumnGridTransform = "PART_PaneColumnGridTransform";
	private const string TemplatePartNameSidebarResizer = "PART_SidebarResizer";
	private const string TemplatePartNameSidebarResizerControl = "PART_SidebarResizerControl";
	private const string TemplatePartNamePaneLightDismissLayer = "PART_PaneLightDismissLayer";
	private const string TemplatePartNameMenuItemsHost = "PART_MenuItemsHost";
	private const string TemplatePartNameFooterMenuItemsHost = "PART_FooterMenuItemsHost";

	private bool _draggingSidebarResizer;
	private double _preManipulationSidebarWidth;
	private ColumnDefinition? _paneColumnDefinition;
	private Grid? _paneColumnGrid;
	private CompositeTransform? _paneColumnGridTransform;
	private Border? _sidebarResizer;
	private Control? _sidebarResizerControl;
	private Grid? _paneLightDismissLayer;
	private ItemsControl? _menuItemsHost;
	private ItemsControl? _footerMenuItemsHost;

	public event EventHandler<SidebarView2ItemInvokedEventArgs>? ItemInvoked;

	public SidebarView2()
	{
		DefaultStyleKey = typeof(SidebarView2);
	}

	protected override void OnApplyTemplate()
	{
		if (_sidebarResizer is not null)
		{
			_sidebarResizer.DoubleTapped -= SidebarResizer_DoubleTapped;
			_sidebarResizer.ManipulationCompleted -= SidebarResizer_ManipulationCompleted;
			_sidebarResizer.ManipulationDelta -= SidebarResizer_ManipulationDelta;
			_sidebarResizer.ManipulationStarted -= SidebarResizer_ManipulationStarted;
			_sidebarResizer.PointerCanceled -= SidebarResizer_PointerExited;
			_sidebarResizer.PointerEntered -= SidebarResizer_PointerEntered;
			_sidebarResizer.PointerExited -= SidebarResizer_PointerExited;
		}

		if (_sidebarResizerControl is not null)
			_sidebarResizerControl.KeyDown -= SidebarResizerControl_KeyDown;

		if (_paneLightDismissLayer is not null)
		{
			_paneLightDismissLayer.PointerPressed -= PaneLightDismissLayer_PointerPressed;
			_paneLightDismissLayer.Tapped -= PaneLightDismissLayer_Tapped;
		}

		if (_menuItemsHost is not null)
			_menuItemsHost.Loaded -= ItemsHost_Loaded;

		if (_footerMenuItemsHost is not null)
			_footerMenuItemsHost.Loaded -= ItemsHost_Loaded;

		base.OnApplyTemplate();

		_paneColumnDefinition = GetTemplateChild(TemplatePartNamePaneColumnDefinition) as ColumnDefinition;
		_paneColumnGrid = GetTemplateChild(TemplatePartNamePaneColumnGrid) as Grid;
		_paneColumnGridTransform = GetTemplateChild(TemplatePartNamePaneColumnGridTransform) as CompositeTransform;
		_sidebarResizer = GetTemplateChild(TemplatePartNameSidebarResizer) as Border;
		_sidebarResizerControl = GetTemplateChild(TemplatePartNameSidebarResizerControl) as Control;
		_paneLightDismissLayer = GetTemplateChild(TemplatePartNamePaneLightDismissLayer) as Grid;
		_menuItemsHost = GetTemplateChild(TemplatePartNameMenuItemsHost) as ItemsControl;
		_footerMenuItemsHost = GetTemplateChild(TemplatePartNameFooterMenuItemsHost) as ItemsControl;

		if (_sidebarResizer is not null)
		{
			_sidebarResizer.DoubleTapped += SidebarResizer_DoubleTapped;
			_sidebarResizer.ManipulationCompleted += SidebarResizer_ManipulationCompleted;
			_sidebarResizer.ManipulationDelta += SidebarResizer_ManipulationDelta;
			_sidebarResizer.ManipulationStarted += SidebarResizer_ManipulationStarted;
			_sidebarResizer.PointerCanceled += SidebarResizer_PointerExited;
			_sidebarResizer.PointerEntered += SidebarResizer_PointerEntered;
			_sidebarResizer.PointerExited += SidebarResizer_PointerExited;
		}

		if (_sidebarResizerControl is not null)
			_sidebarResizerControl.KeyDown += SidebarResizerControl_KeyDown;

		if (_paneLightDismissLayer is not null)
		{
			_paneLightDismissLayer.PointerPressed += PaneLightDismissLayer_PointerPressed;
			_paneLightDismissLayer.Tapped += PaneLightDismissLayer_Tapped;
		}

		if (_menuItemsHost is not null)
			_menuItemsHost.Loaded += ItemsHost_Loaded;

		if (_footerMenuItemsHost is not null)
			_footerMenuItemsHost.Loaded += ItemsHost_Loaded;

		UpdatePreparedMenuItems();
		UpdateDisplayMode();
		UpdateResizerAvailability();
	}

	internal void RaiseItemInvoked(SidebarViewItem item)
	{
		SelectedItem = item;
		ItemInvoked?.Invoke(this, new(item));
	}

	internal void OnItemExpandedChanged(SidebarViewItem item)
	{
		QueueUpdatePreparedMenuItems();
	}

	private void ItemsHost_Loaded(object sender, RoutedEventArgs e)
	{
		UpdatePreparedMenuItems();
	}

	private static object? GetItemValue(SidebarViewItem item)
	{
		return item;
	}

	internal bool HasSelectedDescendant(SidebarViewItem item)
	{
		if (SelectedItem is null)
			return false;

		foreach (var child in EnumerateDescendants(item))
		{
			if (Equals(child, SelectedItem))
				return true;
		}

		return false;
	}

	internal static void CreateAndAttachMoveAnimation(Microsoft.UI.Composition.Visual visual)
	{
		var compositor = visual.Compositor;
		var cubicFunction = compositor.CreateCubicBezierEasingFunction(new Vector2(0.0f, 0.35f), new Vector2(0.15f, 1.0f));
		var moveAnimation = compositor.CreateVector3KeyFrameAnimation();
		moveAnimation.Target = "Offset";
		moveAnimation.InsertExpressionKeyFrame(1.0f, "this.FinalValue", cubicFunction);
		moveAnimation.Duration = TimeSpan.FromMilliseconds(200);

		var collection = compositor.CreateImplicitAnimationCollection();
		collection["Offset"] = moveAnimation;
		visual.ImplicitAnimations = collection;
	}

	private void UpdatePreparedMenuItems()
	{
		PrepareItemsHost(_menuItemsHost);
		PrepareItemsHost(_footerMenuItemsHost);
	}

	private void QueueUpdatePreparedMenuItems()
	{
		DispatcherQueue.TryEnqueue(UpdatePreparedMenuItems);
	}

	private void PrepareItemsHost(DependencyObject? host)
	{
		foreach (var element in EnumerateDescendants(host))
		{
			if (element is SidebarViewItem item)
			{
				item.Owner = this;
				item.ChildItemTemplate = MenuItemTemplate;
				item.ChildItemTemplateSelector = MenuItemTemplateSelector;

				item.IsSelected = item is not null && Equals(item, SelectedItem);
				item.IsSelected |= !item.IsExpanded && HasSelectedDescendant(item);
				item.UpdateExpansionState();
			}
		}
	}

	private static IEnumerable EnumerateDescendants(DependencyObject? parent)
	{
		if (parent is null)
			yield break;

		var childCount = VisualTreeHelper.GetChildrenCount(parent);
		for (var index = 0; index < childCount; index++)
		{
			var child = VisualTreeHelper.GetChild(parent, index);
			yield return child;

			foreach (var descendant in EnumerateDescendants(child))
				yield return descendant;
		}
	}

	private void UpdateMinimalMode()
	{
		if (DisplayMode != SidebarDisplayMode.Minimal)
			return;

		VisualStateManager.GoToState(this, IsPaneOpen ? "MinimalExpanded" : "MinimalCollapsed", true);
	}

	private void UpdateDisplayMode()
	{
		switch (DisplayMode)
		{
			case SidebarDisplayMode.Compact:
				VisualStateManager.GoToState(this, "Compact", true);
				break;
			case SidebarDisplayMode.Expanded:
				UpdateOpenPaneLengthColumn();
				VisualStateManager.GoToState(this, "Expanded", true);
				break;
			case SidebarDisplayMode.Minimal:
				IsPaneOpen = false;
				UpdateMinimalMode();
				break;
		}

		UpdateResizerAvailability();
	}

	private void UpdateDisplayModeForPaneWidth(double newPaneWidth)
	{
		if (newPaneWidth < CompactMaxWidth)
		{
			DisplayMode = SidebarDisplayMode.Compact;
		}
		else if (newPaneWidth > CompactMaxWidth)
		{
			DisplayMode = SidebarDisplayMode.Expanded;
			OpenPaneLength = newPaneWidth;
		}
	}

	private void UpdateOpenPaneLengthColumn()
	{
		if (DisplayMode != SidebarDisplayMode.Expanded || _paneColumnDefinition is null)
			return;

		_paneColumnDefinition.Width = new GridLength(OpenPaneLength);
	}

	private void UpdateResizerAvailability()
	{
		if (_sidebarResizer is null)
			return;

		if (!CanResizePane)
		{
			_sidebarResizer.Visibility = Visibility.Collapsed;
			_sidebarResizer.IsHitTestVisible = false;
			return;
		}

		_sidebarResizer.IsHitTestVisible = true;
		if (DisplayMode != SidebarDisplayMode.Minimal)
			_sidebarResizer.Visibility = Visibility.Visible;
	}

	private void SidebarResizer_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
	{
		if (!CanResizePane || _paneColumnGrid is null)
			return;

		_draggingSidebarResizer = true;
		_preManipulationSidebarWidth = _paneColumnGrid.ActualWidth;
		VisualStateManager.GoToState(this, "ResizerPressed", true);
		e.Handled = true;
	}

	private void SidebarResizer_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
	{
		var newWidth = _preManipulationSidebarWidth + e.Cumulative.Translation.X;
		UpdateDisplayModeForPaneWidth(newWidth);
		e.Handled = true;
	}

	private void SidebarResizerControl_KeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (!CanResizePane)
			return;

		if (e.Key != VirtualKey.Space &&
			e.Key != VirtualKey.Enter &&
			e.Key != VirtualKey.Left &&
			e.Key != VirtualKey.Right &&
			e.Key != VirtualKey.Control)
		{
			return;
		}

		var primaryInvocation = e.Key == VirtualKey.Space || e.Key == VirtualKey.Enter;
		if (DisplayMode == SidebarDisplayMode.Expanded)
		{
			if (primaryInvocation)
			{
				DisplayMode = SidebarDisplayMode.Compact;
				return;
			}

			var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
			var increment = ctrl.HasFlag(CoreVirtualKeyStates.Down) ? 5 : 1;
			if (e.Key == VirtualKey.Left)
				increment = -increment;

			var newWidth = OpenPaneLength + increment;
			UpdateDisplayModeForPaneWidth(newWidth);
			e.Handled = true;
			return;
		}

		if (DisplayMode == SidebarDisplayMode.Compact && (primaryInvocation || e.Key == VirtualKey.Right))
		{
			DisplayMode = SidebarDisplayMode.Expanded;
			e.Handled = true;
		}
	}

	private void PaneLightDismissLayer_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		IsPaneOpen = false;
		e.Handled = true;
	}

	private void PaneLightDismissLayer_Tapped(object sender, TappedRoutedEventArgs e)
	{
		IsPaneOpen = false;
		e.Handled = true;
	}

	private void SidebarResizer_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
	{
		if (!CanResizePane)
			return;

		DisplayMode = DisplayMode == SidebarDisplayMode.Expanded
			? SidebarDisplayMode.Compact
			: SidebarDisplayMode.Expanded;
		e.Handled = true;
	}

	private void SidebarResizer_PointerEntered(object sender, PointerRoutedEventArgs e)
	{
		if (!CanResizePane)
			return;

		var sidebarResizer = (FrameworkElement)sender;
		sidebarResizer.ChangeCursor(InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast));
		VisualStateManager.GoToState(this, "ResizerPointerOver", true);
		e.Handled = true;
	}

	private void SidebarResizer_PointerExited(object sender, PointerRoutedEventArgs e)
	{
		if (!CanResizePane || _draggingSidebarResizer)
			return;

		var sidebarResizer = (FrameworkElement)sender;
		sidebarResizer.ChangeCursor(InputSystemCursor.Create(InputSystemCursorShape.Arrow));
		VisualStateManager.GoToState(this, "ResizerNormal", true);
		e.Handled = true;
	}

	private void SidebarResizer_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
	{
		_draggingSidebarResizer = false;
		VisualStateManager.GoToState(this, "ResizerNormal", true);
		e.Handled = true;
	}
}

public sealed class SidebarView2ItemInvokedEventArgs(object? invokedItem)
{
	public object? InvokedItem { get; } = invokedItem;
}
