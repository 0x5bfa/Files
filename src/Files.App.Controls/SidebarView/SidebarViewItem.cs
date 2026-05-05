// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Files.App.Controls;

public partial class SidebarViewItem : Control
{
	private const string TemplatePartNameRootGrid = "PART_RootGrid";
	private const string VisualStateNameSelected = "Selected";
	private const string VisualStateNameUnselected = "Unselected";
	private const string VisualStateNameNormalUnselected = "NormalUnselected";
	private const string VisualStateNameNormalSelected = "NormalSelected";
	private const string VisualStateNamePointerOverUnselected = "PointerOverUnselected";
	private const string VisualStateNamePointerOverSelected = "PointerOverSelected";
	private const string VisualStateNamePressedUnselected = "PressedUnselected";
	private const string VisualStateNamePressedSelected = "PressedSelected";

	private Grid? _rootGrid;
	private bool _isPointerOver;
	private bool _isPressed;

	public SidebarViewItem()
	{
		DefaultStyleKey = typeof(SidebarViewItem);
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
		if (Children is IList { Count: > 0 })
		{
			IsExpanded = !IsExpanded;
			return;
		}

		Owner?.RaiseItemInvoked(this);
	}

	internal void UpdateExpansionState()
	{
		var hasChildren = Children is IList { Count: > 0 };
		VisualStateManager.GoToState(this, hasChildren ? IsExpanded ? "Expanded" : "Collapsed" : "NoChildren", true);
		VisualStateManager.GoToState(this, IsExpanded ? "ExpandedIconNormal" : "CollapsedIconNormal", true);
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
