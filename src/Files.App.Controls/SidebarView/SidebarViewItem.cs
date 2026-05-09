// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.System;

namespace Files.App.Controls;

public partial class SidebarViewItem : Control
{
	private const string TemplatePartNameRootGrid = "PART_RootGrid";
	private const string TemplatePartNameChildrenItemsControl = "PART_ChildrenItemsControl";
	private const string TemplatePartNameChildrenOverflowFlyoutContentPresenter = "ChildrenOverflowFlyoutContentPresenter";
	private const string VisualStateNameSelected = "Selected";
	private const string VisualStateNameUnselected = "Unselected";
	private const string VisualStateNameNormalUnselected = "NormalUnselected";
	private const string VisualStateNameNormalSelected = "NormalSelected";
	private const string VisualStateNamePointerOverUnselected = "PointerOverUnselected";
	private const string VisualStateNamePointerOverSelected = "PointerOverSelected";
	private const string VisualStateNamePressedUnselected = "PressedUnselected";
	private const string VisualStateNamePressedSelected = "PressedSelected";

	private Grid? _rootGrid;
	private ItemsControl? _childrenItemsControl;
	private Panel? _childrenItemsControlParent;
	private ContentPresenter? _childrenOverflowFlyoutContentPresenter;
	private bool _isPointerOver;
	private bool _isPressed;

	public SidebarViewItem()
	{
		DefaultStyleKey = typeof(SidebarViewItem);
	}

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new SidebarViewItemAutomationPeer(this);
	}

	protected override void OnApplyTemplate()
	{
		_rootGrid?.PointerEntered -= RootBorder_PointerEntered;
		_rootGrid?.PointerExited -= RootBorder_PointerExited;
		_rootGrid?.PointerCanceled -= RootBorder_PointerCanceled;
		_rootGrid?.PointerPressed -= RootBorder_PointerPressed;
		_rootGrid?.Tapped -= RootBorder_Tapped;

		base.OnApplyTemplate();

		_rootGrid = GetTemplateChild(TemplatePartNameRootGrid) as Grid;
		_childrenItemsControl = GetTemplateChild(TemplatePartNameChildrenItemsControl) as ItemsControl;
		_childrenItemsControlParent = VisualTreeHelper.GetParent(_childrenItemsControl) as Panel;
		_childrenOverflowFlyoutContentPresenter = GetTemplateChild(TemplatePartNameChildrenOverflowFlyoutContentPresenter) as ContentPresenter;

		_rootGrid?.PointerEntered += RootBorder_PointerEntered;
		_rootGrid?.PointerExited += RootBorder_PointerExited;
		_rootGrid?.PointerCanceled += RootBorder_PointerCanceled;
		_rootGrid?.PointerPressed += RootBorder_PointerPressed;
		_rootGrid?.Tapped += RootBorder_Tapped;

		UpdateSelectionState();
		UpdateExpansionState();

		AttachMoveAnimations();
	}

	private void AttachMoveAnimations()
	{
		if (VisualTreeHelper.GetParent(this) is ContentPresenter parentPresenter)
			SidebarView2.CreateAndAttachMoveAnimation(ElementCompositionPreview.GetElementVisual(parentPresenter));
	}

	protected override void OnKeyDown(KeyRoutedEventArgs e)
	{
		base.OnKeyDown(e);

		if (e.Key is not VirtualKey.Enter and not VirtualKey.Space)
			return;

		Invoke();
		e.Handled = true;
	}

	private void RootBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
	{
		_isPointerOver = true;
		UpdateBackgroundState();
	}

	private void RootBorder_PointerExited(object sender, PointerRoutedEventArgs e)
	{
		_isPointerOver = false;
		_isPressed = false;
		UpdateBackgroundState();
	}

	private void RootBorder_PointerCanceled(object sender, PointerRoutedEventArgs e)
	{
		_isPressed = false;
		UpdateBackgroundState();
	}

	private void RootBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		_isPressed = true;
		UpdateBackgroundState();
	}

	private void RootBorder_Tapped(object sender, TappedRoutedEventArgs e)
	{
		Invoke();
		_isPressed = false;
		UpdateBackgroundState();
		e.Handled = true;
	}

	protected virtual void Invoke()
	{
		if (Children is IList { Count: > 0 }) // Has children
		{
			if (Owner?.DisplayMode != SidebarDisplayMode.Compact)
			{
				IsExpanded = !IsExpanded;
			}
			else
			{
				SetFlyoutOpen(true);
			}

			return;
		}

		Owner?.RaiseItemInvoked(this);
	}

	internal void InvokeFromAutomationPeer()
	{
		Invoke();
	}

	internal void UpdateExpansionState()
	{
		var hasChildren = Children is IList { Count: > 0 };
		if (!hasChildren)
		{
			SetFlyoutOpen(false);
			ReparentChildrenInline();
			VisualStateManager.GoToState(this, "NoChildren", true);
			return;
		}

		if (Owner?.DisplayMode == SidebarDisplayMode.Compact)
		{
			ReparentChildrenToFlyout();
			VisualStateManager.GoToState(this, "NoExpansion", true);
			VisualStateManager.GoToState(this, "CollapsedIconNormal", true);
			return;
		}

		SetFlyoutOpen(false);
		ReparentChildrenInline();
		VisualStateManager.GoToState(this, IsExpanded ? "Expanded" : "Collapsed", true);
		VisualStateManager.GoToState(this, IsExpanded ? "ExpandedIconNormal" : "CollapsedIconNormal", true);
	}

	private void SetFlyoutOpen(bool isOpen)
	{
		if (_rootGrid is null)
			return;

		var attachedFlyout = FlyoutBase.GetAttachedFlyout(_rootGrid);
		if (attachedFlyout is null)
			return;

		if (isOpen)
		{
			ReparentChildrenToFlyout();
			if (_childrenItemsControl is not null)
				_childrenItemsControl.Visibility = Visibility.Visible;

			FlyoutBase.ShowAttachedFlyout(_rootGrid);
		}
		else
		{
			attachedFlyout.Hide();
		}
	}

	private void ReparentChildrenToFlyout()
	{
		if (_childrenItemsControl is null || _childrenOverflowFlyoutContentPresenter is null)
			return;

		if (ReferenceEquals(_childrenOverflowFlyoutContentPresenter.Content, _childrenItemsControl))
			return;

		if (_childrenItemsControlParent?.Children.Contains(_childrenItemsControl) == true)
			_childrenItemsControlParent.Children.Remove(_childrenItemsControl);

		_childrenOverflowFlyoutContentPresenter.Content = _childrenItemsControl;
	}

	private void ReparentChildrenInline()
	{
		if (_childrenItemsControl is null || _childrenItemsControlParent is null)
			return;

		if (_childrenItemsControlParent.Children.Contains(_childrenItemsControl))
			return;

		if (ReferenceEquals(_childrenOverflowFlyoutContentPresenter?.Content, _childrenItemsControl))
			_childrenOverflowFlyoutContentPresenter.Content = null;

		Grid.SetRow(_childrenItemsControl, 1);
		_childrenItemsControlParent.Children.Add(_childrenItemsControl);
	}

	protected void UpdateSelectionState()
	{
		VisualStateManager.GoToState(this, IsSelected ? VisualStateNameSelected : VisualStateNameUnselected, true);
		UpdateBackgroundState();
	}

	private void UpdateBackgroundState()
	{
		var state = _isPressed
			? IsSelected ? VisualStateNamePressedSelected : VisualStateNamePressedUnselected
			: _isPointerOver
				? IsSelected ? VisualStateNamePointerOverSelected : VisualStateNamePointerOverUnselected
				: IsSelected ? VisualStateNameNormalSelected : VisualStateNameNormalUnselected;

		VisualStateManager.GoToState(this, state, true);
	}
}
