// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using System.Collections;
using System.Numerics;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace Files.App.Controls;

[ContentProperty(Name = nameof(Content))]
public partial class SidebarView2 : Control
{
	private const double CompactMaxWidth = 200;
	private const float PaneOverlayShadowDepth = 16f;
	private const string TemplatePartNameRootSplitView = "PART_RootSplitView";
	private const string TemplatePartNamePaneColumnGrid = "PART_PaneColumnGrid";
	private const string TemplatePartNamePaneToggleButton = "PART_PaneToggleButton";
	private const string TemplatePartNameBackButton = "PART_BackButton";
	private const string TemplatePartNameAutoSuggestButton = "PART_AutoSuggestButton";
	private const string TemplatePartNameSidebarResizer = "PART_SidebarResizer";
	private const string TemplatePartNameSidebarResizerControl = "PART_SidebarResizerControl";
	private const string TemplatePartNamePaneLightDismissLayer = "PART_PaneLightDismissLayer";
	private const string TemplatePartNameMenuItemsHost = "PART_MenuItemsHost";
	private const string TemplatePartNameFooterMenuItemsHost = "PART_FooterMenuItemsHost";
	private const string VisualStateNameClosedCompact = "ClosedCompact";
	private const string VisualStateNameNotClosedCompact = "NotClosedCompact";
	private const string VisualStateNameListSizeCompact = "ListSizeCompact";
	private const string VisualStateNameListSizeFull = "ListSizeFull";
	private const string VisualStateNamePaneOverlaying = "PaneOverlaying";
	private const string VisualStateNamePaneNotOverlaying = "PaneNotOverlaying";

	private bool _draggingSidebarResizer;
	private bool _fromOnApplyTemplate;
	private bool _updateVisualStateForDisplayModeFromOnLoaded;
	private bool _isClosedCompact;
	private double _preManipulationSidebarWidth;
	private SidebarViewDisplayMode _autoDisplayMode = SidebarViewDisplayMode.LeftCompact;
	private long _rootSplitViewIsPaneOpenChangedToken;
	private long _rootSplitViewDisplayModeChangedToken;
	private SplitView? _rootSplitView;
	private Grid? _paneColumnGrid;
	private Button? _paneToggleButton;
	private Button? _backButton;
	private Button? _autoSuggestButton;
	private Border? _sidebarResizer;
	private Control? _sidebarResizerControl;
	private Grid? _paneLightDismissLayer;
	private ItemsControl? _menuItemsHost;
	private ItemsControl? _footerMenuItemsHost;
	private readonly SidebarViewItemFactory _itemFactory;

	public event EventHandler<SidebarView2ItemInvokedEventArgs>? ItemInvoked;
	public event TypedEventHandler<SidebarView2, SidebarView2BackRequestedEventArgs>? BackRequested;
	public SidebarView2TemplateSettings TemplateSettings { get; } = new();
	internal SidebarViewItemFactory ItemFactory => _itemFactory;
	internal SidebarViewDisplayMode EffectiveDisplayMode => DisplayMode == SidebarViewDisplayMode.Auto
		? _autoDisplayMode
		: DisplayMode;
	internal bool IsClosedCompact => _isClosedCompact;
	internal SidebarViewItem? SelectedItemContainer { get; private set; }

	public SidebarView2()
	{
		DefaultStyleKey = typeof(SidebarView2);
		_itemFactory = new(this);
		Loaded += SidebarView2_Loaded;
		SizeChanged += SidebarView2_SizeChanged;
		UpdateTemplateSettings();
	}

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new SidebarView2AutomationPeer(this);
	}

	protected override void OnApplyTemplate()
	{
		_fromOnApplyTemplate = true;
		try
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

			if (_paneToggleButton is not null)
				_paneToggleButton.Click -= PaneToggleButton_Click;

			if (_backButton is not null)
				_backButton.Click -= BackButton_Click;

			if (_autoSuggestButton is not null)
				_autoSuggestButton.Click -= AutoSuggestButton_Click;

			if (_paneLightDismissLayer is not null)
			{
				_paneLightDismissLayer.PointerPressed -= PaneLightDismissLayer_PointerPressed;
				_paneLightDismissLayer.Tapped -= PaneLightDismissLayer_Tapped;
			}

			if (_menuItemsHost is not null)
				_menuItemsHost.Loaded -= ItemsHost_Loaded;

			if (_footerMenuItemsHost is not null)
				_footerMenuItemsHost.Loaded -= ItemsHost_Loaded;

			if (_rootSplitView is not null)
			{
				if (_rootSplitViewIsPaneOpenChangedToken != 0)
				{
					_rootSplitView.UnregisterPropertyChangedCallback(SplitView.IsPaneOpenProperty, _rootSplitViewIsPaneOpenChangedToken);
					_rootSplitViewIsPaneOpenChangedToken = 0;
				}

				if (_rootSplitViewDisplayModeChangedToken != 0)
				{
					_rootSplitView.UnregisterPropertyChangedCallback(SplitView.DisplayModeProperty, _rootSplitViewDisplayModeChangedToken);
					_rootSplitViewDisplayModeChangedToken = 0;
				}
			}

			base.OnApplyTemplate();

			_rootSplitView = GetTemplateChild(TemplatePartNameRootSplitView) as SplitView;
			_paneColumnGrid = GetTemplateChild(TemplatePartNamePaneColumnGrid) as Grid;
			_paneToggleButton = GetTemplateChild(TemplatePartNamePaneToggleButton) as Button;
			_backButton = GetTemplateChild(TemplatePartNameBackButton) as Button;
			_autoSuggestButton = GetTemplateChild(TemplatePartNameAutoSuggestButton) as Button;
			_sidebarResizer = GetTemplateChild(TemplatePartNameSidebarResizer) as Border;
			_sidebarResizerControl = GetTemplateChild(TemplatePartNameSidebarResizerControl) as Control;
			_paneLightDismissLayer = GetTemplateChild(TemplatePartNamePaneLightDismissLayer) as Grid;
			_menuItemsHost = GetTemplateChild(TemplatePartNameMenuItemsHost) as ItemsControl;
			_footerMenuItemsHost = GetTemplateChild(TemplatePartNameFooterMenuItemsHost) as ItemsControl;

			if (_rootSplitView is not null)
			{
				_rootSplitViewIsPaneOpenChangedToken = _rootSplitView.RegisterPropertyChangedCallback(SplitView.IsPaneOpenProperty, OnSplitViewClosedCompactChanged);
				_rootSplitViewDisplayModeChangedToken = _rootSplitView.RegisterPropertyChangedCallback(SplitView.DisplayModeProperty, OnSplitViewClosedCompactChanged);
				UpdateIsClosedCompact();
				UpdatePaneOverlayGroup();
			}

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

			if (_paneToggleButton is not null)
				_paneToggleButton.Click += PaneToggleButton_Click;

			if (_backButton is not null)
				_backButton.Click += BackButton_Click;

			if (_autoSuggestButton is not null)
				_autoSuggestButton.Click += AutoSuggestButton_Click;

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
			UpdateAdaptiveDisplayMode(ActualWidth, true);
			UpdatePaneButtons();
			UpdateResizerAvailability();
		}
		finally
		{
			_fromOnApplyTemplate = false;
		}
	}

	internal void RaiseItemInvoked(SidebarViewItem item)
	{
		SelectedItem = item.ItemValue;
		SelectedItemContainer = item;
		UpdatePreparedMenuItems();
		ItemInvoked?.Invoke(this, new(item.ItemValue));
	}

	internal void OnItemExpandedChanged(SidebarViewItem item)
	{
		QueueUpdatePreparedMenuItems();
	}

	private void ItemsHost_Loaded(object sender, RoutedEventArgs e)
	{
		UpdatePreparedMenuItems();
	}

	private void SidebarView2_Loaded(object sender, RoutedEventArgs e)
	{
		if (!_updateVisualStateForDisplayModeFromOnLoaded)
			return;

		_updateVisualStateForDisplayModeFromOnLoaded = false;
		UpdateVisualStateForDisplayModeGroup(GetVisualStateDisplayMode());
		UpdateIsClosedCompact();
		UpdatePaneOverlayGroup();
		UpdatePaneShadow();
		UpdatePaneButtons();
	}

	private void SidebarView2_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		UpdateAdaptiveDisplayMode(e.NewSize.Width);
	}

	internal bool HasSelectedDescendant(SidebarViewItem item)
	{
		return item.HasSelectedDescendant(SelectedItem);
	}

	internal void UpdateSelectedItemContainer(SidebarViewItem item)
	{
		if (Equals(SelectedItem, item.ItemValue) || ReferenceEquals(SelectedItem, item))
			SelectedItemContainer = item;
	}

	internal bool ContainsItemValue(SidebarViewItem item, object itemValue)
	{
		if (item.ParentItem?.Children is IEnumerable parentItems && ContainsItemValue(parentItems, itemValue))
			return true;

		return ContainsItemValue(MenuItemsSource, itemValue) ||
			ContainsItemValue(FooterMenuItemsSource, itemValue);
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
		UpdateStateOfItems(_menuItemsHost);
		UpdateStateOfItems(_footerMenuItemsHost);
	}

	private void QueueUpdatePreparedMenuItems()
	{
		DispatcherQueue.TryEnqueue(UpdatePreparedMenuItems);
	}

	private void UpdateStateOfItems(DependencyObject? host)
	{
		foreach (var item in EnumerateDirectSidebarViewItems(host))
			_itemFactory.Prepare(item);
	}

	private static bool ContainsItemValue(object? itemsSource, object itemValue)
	{
		if (itemsSource is not IEnumerable items || itemsSource is string)
			return false;

		foreach (var item in items)
		{
			if (Equals(item, itemValue))
				return true;
		}

		return false;
	}

	private static IEnumerable<SidebarViewItem> EnumerateDirectSidebarViewItems(DependencyObject? parent)
	{
		if (parent is null)
			yield break;

		if (parent is SidebarViewItem item)
		{
			yield return item;
			yield break;
		}

		var childCount = VisualTreeHelper.GetChildrenCount(parent);
		for (var index = 0; index < childCount; index++)
		{
			foreach (var descendantItem in EnumerateDirectSidebarViewItems(VisualTreeHelper.GetChild(parent, index)))
				yield return descendantItem;
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

	private void UpdatePaneState()
	{
		UpdateIsClosedCompact();
		UpdatePaneOverlayGroup();
		UpdatePaneShadow();
		UpdatePaneButtons();

		if (GetVisualStateDisplayMode() == SidebarViewVisualStateDisplayMode.Minimal)
			VisualStateManager.GoToState(this, IsPaneOpen ? "MinimalExpanded" : "MinimalCollapsed", true);
	}

	private void UpdateDisplayMode()
	{
		var displayMode = GetVisualStateDisplayMode();
		switch (displayMode)
		{
			case SidebarViewVisualStateDisplayMode.Expanded:
				OpenPane();
				break;
			case SidebarViewVisualStateDisplayMode.Compact:
			case SidebarViewVisualStateDisplayMode.Minimal:
				ClosePane();
				break;
		}

		UpdateVisualStateForDisplayModeGroup(displayMode);
		UpdatePreparedMenuItems();
		UpdatePaneButtons();
		UpdateResizerAvailability();
	}

	private void UpdateAdaptiveDisplayMode(double width, bool forceSetDisplayMode = false)
	{
		if (DisplayMode != SidebarViewDisplayMode.Auto)
		{
			if (forceSetDisplayMode)
				UpdateDisplayMode();

			return;
		}

		var displayMode = SidebarViewDisplayMode.LeftCompact;
		if (width >= ExpandedModeThresholdWidth)
		{
			displayMode = SidebarViewDisplayMode.Left;
		}
		else if (width > 0 && width < CompactModeThresholdWidth)
		{
			displayMode = SidebarViewDisplayMode.LeftMinimal;
		}

		if (!forceSetDisplayMode && _autoDisplayMode == displayMode)
			return;

		_autoDisplayMode = displayMode;
		UpdateDisplayMode();
	}

	private SidebarViewVisualStateDisplayMode GetVisualStateDisplayMode()
	{
		return EffectiveDisplayMode switch
		{
			SidebarViewDisplayMode.LeftCompact => SidebarViewVisualStateDisplayMode.Compact,
			SidebarViewDisplayMode.LeftMinimal => SidebarViewVisualStateDisplayMode.Minimal,
			_ => SidebarViewVisualStateDisplayMode.Expanded,
		};
	}

	private void UpdateDisplayModeForPaneWidth(double newPaneWidth)
	{
		if (newPaneWidth < CompactMaxWidth)
		{
			DisplayMode = SidebarViewDisplayMode.LeftCompact;
		}
		else if (newPaneWidth > CompactMaxWidth)
		{
			DisplayMode = SidebarViewDisplayMode.Left;
			OpenPaneLength = newPaneWidth;
		}
	}

	private void UpdateTemplateSettings()
	{
		if (TemplateSettings is null)
			return;

		TemplateSettings.NegativeOpenPaneLength = -OpenPaneLength;
		TemplateSettings.NegativeCompactPaneLength = -CompactPaneLength;
	}

	private void UpdateSplitViewDisplayMode()
	{
		UpdateVisualStateForDisplayModeGroup(GetVisualStateDisplayMode());
	}

	private void UpdateVisualStateForDisplayModeGroup(SidebarViewVisualStateDisplayMode displayMode)
	{
		var visualStateName = displayMode switch
		{
			SidebarViewVisualStateDisplayMode.Compact => "Compact",
			SidebarViewVisualStateDisplayMode.Minimal => IsPaneOpen ? "MinimalExpanded" : "MinimalCollapsed",
			_ => "Expanded",
		};

		VisualStateManager.GoToState(this, visualStateName, true);
		UpdateSplitViewDisplayMode(displayMode);
	}

	private void UpdateSplitViewDisplayMode(SidebarViewVisualStateDisplayMode displayMode)
	{
		if (_rootSplitView is null)
			return;

		var splitViewDisplayMode = displayMode switch
		{
			SidebarViewVisualStateDisplayMode.Compact => SplitViewDisplayMode.CompactOverlay,
			SidebarViewVisualStateDisplayMode.Minimal => SplitViewDisplayMode.Overlay,
			_ => SplitViewDisplayMode.CompactInline,
		};

		if (_fromOnApplyTemplate)
		{
			_updateVisualStateForDisplayModeFromOnLoaded = true;
			return;
		}

		_rootSplitView.DisplayMode = splitViewDisplayMode;
		UpdateIsClosedCompact();
		UpdatePaneOverlayGroup();
		UpdatePaneShadow();
		UpdatePaneButtons();
	}

	private void UpdatePaneShadow()
	{
		if (_paneColumnGrid is null)
			return;

		var translation = _paneColumnGrid.Translation;
		if (IsPaneOverlaying())
		{
			_paneColumnGrid.Shadow ??= new ThemeShadow();
			_paneColumnGrid.Translation = new Vector3(translation.X, translation.Y, PaneOverlayShadowDepth);
		}
		else
		{
			_paneColumnGrid.Shadow = null;
			_paneColumnGrid.Translation = new Vector3(translation.X, translation.Y, 0);
		}
	}

	private void UpdatePaneOverlayGroup()
	{
		VisualStateManager.GoToState(
			this,
			IsPaneOverlaying() ? VisualStateNamePaneOverlaying : VisualStateNamePaneNotOverlaying,
			true);
	}

	private bool IsPaneOverlaying()
	{
		if (_rootSplitView is null)
			return false;

		var splitViewDisplayMode = _rootSplitView.DisplayMode;
		return IsPaneOpen &&
			(splitViewDisplayMode == SplitViewDisplayMode.CompactOverlay ||
			 splitViewDisplayMode == SplitViewDisplayMode.Overlay);
	}

	private void UpdateIsClosedCompact()
	{
		if (_rootSplitView is null)
			return;

		var splitViewDisplayMode = _rootSplitView.DisplayMode;
		var isClosedCompact = !_rootSplitView.IsPaneOpen &&
			(splitViewDisplayMode == SplitViewDisplayMode.CompactOverlay ||
			 splitViewDisplayMode == SplitViewDisplayMode.CompactInline);
		var closedCompactChanged = _isClosedCompact != isClosedCompact;

		_isClosedCompact = isClosedCompact;
		VisualStateManager.GoToState(this, isClosedCompact ? VisualStateNameClosedCompact : VisualStateNameNotClosedCompact, true);
		VisualStateManager.GoToState(this, isClosedCompact ? VisualStateNameListSizeCompact : VisualStateNameListSizeFull, true);

		if (closedCompactChanged)
			UpdatePreparedMenuItems();

		UpdatePaneButtons();
	}

	private void OnSplitViewClosedCompactChanged(DependencyObject sender, DependencyProperty property)
	{
		if (property == SplitView.IsPaneOpenProperty || property == SplitView.DisplayModeProperty)
		{
			UpdateIsClosedCompact();
			UpdatePaneOverlayGroup();
			UpdatePaneShadow();
		}
	}

	private void OpenPane()
	{
		IsPaneOpen = true;
	}

	private void ClosePane()
	{
		IsPaneOpen = false;
	}

	private void PaneToggleButton_Click(object sender, RoutedEventArgs e)
	{
		if (IsPaneOpen)
		{
			ClosePane();
		}
		else
		{
			OpenPane();
		}
	}

	private void BackButton_Click(object sender, RoutedEventArgs e)
	{
		BackRequested?.Invoke(this, new SidebarView2BackRequestedEventArgs());
	}

	private void AutoSuggestButton_Click(object sender, RoutedEventArgs e)
	{
		OpenPane();
		AutoSuggestBox?.Focus(FocusState.Keyboard);
	}

	private void UpdatePaneButtons()
	{
		UpdatePaneToggleButtonVisibility();
		UpdateBackButtonVisibility();
		UpdateAutoSuggestButtonVisibility();
		UpdatePaneToggleButtonAutomationName();
	}

	private void UpdatePaneToggleButtonVisibility()
	{
		if (_paneToggleButton is null)
			return;

		_paneToggleButton.Visibility = IsPaneToggleButtonVisible ? Visibility.Visible : Visibility.Collapsed;
	}

	private void UpdateBackButtonVisibility()
	{
		if (_backButton is null)
			return;

		_backButton.Visibility = ShouldShowBackButton() ? Visibility.Visible : Visibility.Collapsed;
	}

	private void UpdateAutoSuggestButtonVisibility()
	{
		if (_autoSuggestButton is null)
			return;

		_autoSuggestButton.Visibility = ShouldShowAutoSuggestButton() ? Visibility.Visible : Visibility.Collapsed;
	}

	private void UpdatePaneToggleButtonAutomationName()
	{
		if (_paneToggleButton is null)
			return;

		var name = IsPaneOpen ? "Close navigation pane" : "Open navigation pane";
		AutomationProperties.SetName(_paneToggleButton, name);
		ToolTipService.SetToolTip(_paneToggleButton, name);
	}

	private bool ShouldShowBackButton()
	{
		if (_backButton is null)
			return false;

		if (GetVisualStateDisplayMode() == SidebarViewVisualStateDisplayMode.Minimal && IsPaneOpen)
			return false;

		return IsBackButtonVisible == SidebarViewBackButtonVisible.Visible ||
			IsBackButtonVisible == SidebarViewBackButtonVisible.Auto;
	}

	private bool ShouldShowAutoSuggestButton()
	{
		return AutoSuggestBox is not null && IsClosedCompact;
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
		if (EffectiveDisplayMode != SidebarViewDisplayMode.LeftMinimal)
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
		if (EffectiveDisplayMode == SidebarViewDisplayMode.Left)
		{
			if (primaryInvocation)
			{
				DisplayMode = SidebarViewDisplayMode.LeftCompact;
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

		if (EffectiveDisplayMode == SidebarViewDisplayMode.LeftCompact && (primaryInvocation || e.Key == VirtualKey.Right))
		{
			ExpandPane();
			e.Handled = true;
		}
	}

	private void PaneLightDismissLayer_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		ClosePane();
		e.Handled = true;
	}

	private void PaneLightDismissLayer_Tapped(object sender, TappedRoutedEventArgs e)
	{
		ClosePane();
		e.Handled = true;
	}

	private void SidebarResizer_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
	{
		if (!CanResizePane)
			return;

		if (EffectiveDisplayMode == SidebarViewDisplayMode.Left)
		{
			DisplayMode = SidebarViewDisplayMode.LeftCompact;
		}
		else
		{
			ExpandPane();
		}

		e.Handled = true;
	}

	private void ExpandPane()
	{
		UpdateDisplayModeForPaneWidth(Math.Max(OpenPaneLength, CompactMaxWidth + 1));
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

	private enum SidebarViewVisualStateDisplayMode
	{
		Compact,
		Expanded,
		Minimal,
	}
}

public sealed class SidebarView2ItemInvokedEventArgs(object? invokedItem)
{
	public object? InvokedItem { get; } = invokedItem;
}

public sealed class SidebarView2BackRequestedEventArgs
{
}
