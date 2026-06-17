// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections;
using Windows.System;

namespace Files.App.Controls;

public partial class SidebarViewItem : Control
{
	private const string TemplatePartNameRootGrid = "PART_RootGrid";
	private const string TemplatePartNameChevronContainer = "PART_ChevronContainer";
	private const string TemplatePartNameFlyoutChildrenPresenter = "PART_FlyoutChildrenPresenter";
	private const string VisualStateNameSelected = "Selected";
	private const string VisualStateNameUnselected = "Unselected";
	private const string VisualStateNameNormalUnselected = "NormalUnselected";
	private const string VisualStateNameNormalSelected = "NormalSelected";
	private const string VisualStateNamePointerOverUnselected = "PointerOverUnselected";
	private const string VisualStateNamePointerOverSelected = "PointerOverSelected";
	private const string VisualStateNamePressedUnselected = "PressedUnselected";
	private const string VisualStateNamePressedSelected = "PressedSelected";
	private const string VisualStateNameChevronHidden = "ChevronHidden";
	private const string VisualStateNameChevronVisibleOpen = "ChevronVisibleOpen";
	private const string VisualStateNameChevronVisibleClosed = "ChevronVisibleClosed";
	private const string VisualStateNameNormalChevronHidden = "NormalChevronHidden";
	private const string VisualStateNameNormalChevronVisibleOpen = "NormalChevronVisibleOpen";
	private const string VisualStateNameNormalChevronVisibleClosed = "NormalChevronVisibleClosed";
	private const string VisualStateNamePointerOverChevronHidden = "PointerOverChevronHidden";
	private const string VisualStateNamePointerOverChevronVisibleOpen = "PointerOverChevronVisibleOpen";
	private const string VisualStateNamePointerOverChevronVisibleClosed = "PointerOverChevronVisibleClosed";
	private const string VisualStateNamePressedChevronHidden = "PressedChevronHidden";
	private const string VisualStateNamePressedChevronVisibleOpen = "PressedChevronVisibleOpen";
	private const string VisualStateNamePressedChevronVisibleClosed = "PressedChevronVisibleClosed";

	private Grid? _rootGrid;
	private FrameworkElement? _chevronContainer;
	private ItemsRepeater? _flyoutChildrenPresenter;
	private FlyoutBase? _childrenFlyout;
	private bool _isPointerOver;
	private bool _isPressed;
	private bool _isChevronPressed;
	private bool _suppressChildrenFlyoutClosing;

	public SidebarViewItem()
	{
		DefaultStyleKey = typeof(SidebarViewItem);
		Loaded += SidebarViewItem_Loaded;
		UpdateTemplateSettings();
	}

	public SidebarViewItemTemplateSettings TemplateSettings { get; } = new();

	internal object? ResolvedItemValue
	{
		get
		{
			if (ItemValue is not null)
				return ItemValue;

			if (Item is not null)
				return Item;

			if (DataContext is FlatSidebarItem flatSidebarItem)
				return flatSidebarItem.Item;

			return DataContext ?? this;
		}
	}

	internal SidebarViewItem? ParentItem { get; set; }

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new SidebarViewItemAutomationPeer(this);
	}

	private void SidebarViewItem_Loaded(object sender, RoutedEventArgs e)
	{
		var owner = this.FindAscendant<SidebarView2>();
		if (owner is null)
			return;

		owner.ItemFactory.Prepare(this);
	}

	protected override void OnApplyTemplate()
	{
		UnhookTemplateEvents();

		base.OnApplyTemplate();

		_rootGrid = GetTemplateChild(TemplatePartNameRootGrid) as Grid;
		_chevronContainer = GetTemplateChild(TemplatePartNameChevronContainer) as FrameworkElement;
		_flyoutChildrenPresenter = GetTemplateChild(TemplatePartNameFlyoutChildrenPresenter) as ItemsRepeater;
		_childrenFlyout = _rootGrid is null ? null : FlyoutBase.GetAttachedFlyout(_rootGrid);

		HookTemplateEvents();
		SynchronizeItemExpansion();
		UpdateStateFromOwner();
		AttachMoveAnimations();
	}

	private void HookTemplateEvents()
	{
		if (_rootGrid is not null)
		{
			_rootGrid.PointerEntered += RootBorder_PointerEntered;
			_rootGrid.PointerExited += RootBorder_PointerExited;
			_rootGrid.PointerCanceled += RootBorder_PointerCanceled;
			_rootGrid.PointerPressed += RootBorder_PointerPressed;
			_rootGrid.PointerReleased += RootBorder_PointerReleased;
			_rootGrid.Tapped += RootBorder_Tapped;
		}

		if (_chevronContainer is not null)
		{
			_chevronContainer.PointerPressed += ChevronContainer_PointerPressed;
			_chevronContainer.PointerReleased += ChevronContainer_PointerReleased;
			_chevronContainer.PointerCanceled += ChevronContainer_PointerCanceled;
			_chevronContainer.PointerCaptureLost += ChevronContainer_PointerCaptureLost;
			_chevronContainer.Tapped += ChevronContainer_Tapped;
		}

		if (_flyoutChildrenPresenter is not null)
			_flyoutChildrenPresenter.ElementPrepared += FlyoutChildrenPresenter_ElementPrepared;

		if (_childrenFlyout is not null)
			_childrenFlyout.Closing += ChildrenFlyout_Closing;
	}

	private void UnhookTemplateEvents()
	{
		if (_rootGrid is not null)
		{
			_rootGrid.PointerEntered -= RootBorder_PointerEntered;
			_rootGrid.PointerExited -= RootBorder_PointerExited;
			_rootGrid.PointerCanceled -= RootBorder_PointerCanceled;
			_rootGrid.PointerPressed -= RootBorder_PointerPressed;
			_rootGrid.PointerReleased -= RootBorder_PointerReleased;
			_rootGrid.Tapped -= RootBorder_Tapped;
		}

		if (_chevronContainer is not null)
		{
			_chevronContainer.PointerPressed -= ChevronContainer_PointerPressed;
			_chevronContainer.PointerReleased -= ChevronContainer_PointerReleased;
			_chevronContainer.PointerCanceled -= ChevronContainer_PointerCanceled;
			_chevronContainer.PointerCaptureLost -= ChevronContainer_PointerCaptureLost;
			_chevronContainer.Tapped -= ChevronContainer_Tapped;
		}

		if (_flyoutChildrenPresenter is not null)
			_flyoutChildrenPresenter.ElementPrepared -= FlyoutChildrenPresenter_ElementPrepared;

		if (_childrenFlyout is not null)
			_childrenFlyout.Closing -= ChildrenFlyout_Closing;
	}

	private void AttachMoveAnimations()
	{
		if (VisualTreeHelper.GetParent(this) is ContentPresenter parentPresenter)
			SidebarView2.CreateAndAttachMoveAnimation(ElementCompositionPreview.GetElementVisual(parentPresenter));
	}

	protected override void OnKeyDown(KeyRoutedEventArgs e)
	{
		base.OnKeyDown(e);

		switch (e.Key)
		{
			case VirtualKey.Enter:
			case VirtualKey.Space:
				Invoke();
				e.Handled = true;
				break;

			case VirtualKey.Right when HasChildren() && !IsExpanded:
				IsExpanded = true;
				e.Handled = true;
				break;

			case VirtualKey.Left when HasChildren() && IsExpanded:
				IsExpanded = false;
				e.Handled = true;
				break;
		}
	}

	private void RootBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
	{
		_isPointerOver = true;
		UpdateBackgroundState();
		UpdateChevronState();
	}

	private void RootBorder_PointerExited(object sender, PointerRoutedEventArgs e)
	{
		_isPointerOver = false;
		if (!_isChevronPressed)
			_isPressed = false;
		UpdateBackgroundState();
		UpdateChevronState();
	}

	private void RootBorder_PointerCanceled(object sender, PointerRoutedEventArgs e)
	{
		_isChevronPressed = false;
		_isPressed = false;
		UpdateBackgroundState();
		UpdateChevronState();
	}

	private void RootBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		_isPressed = true;
		UpdateBackgroundState();
		UpdateChevronState();
	}

	private void RootBorder_PointerReleased(object sender, PointerRoutedEventArgs e)
	{
		_isChevronPressed = false;
		_isPressed = false;
		UpdateBackgroundState();
		UpdateChevronState();
	}

	private void RootBorder_Tapped(object sender, TappedRoutedEventArgs e)
	{
		Invoke();
		_isPressed = false;
		UpdateBackgroundState();
		UpdateChevronState();
		e.Handled = true;
	}

	private void ChevronContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		if (!HasChildren())
			return;

		_isChevronPressed = true;
		_isPressed = true;
		_chevronContainer?.CapturePointer(e.Pointer);
		UpdateBackgroundState();
		UpdateChevronState();
		e.Handled = true;
	}

	private void ChevronContainer_PointerReleased(object sender, PointerRoutedEventArgs e)
	{
		if (!_isChevronPressed)
			return;

		var shouldInvoke = IsPointerOverElement(_chevronContainer, e);
		_isChevronPressed = false;
		_isPressed = false;
		_chevronContainer?.ReleasePointerCapture(e.Pointer);

		if (shouldInvoke)
			InvokeChevron();

		UpdateBackgroundState();
		UpdateChevronState();
		e.Handled = true;
	}

	private void ChevronContainer_PointerCanceled(object sender, PointerRoutedEventArgs e)
	{
		CancelChevronPress(e.Pointer);
		e.Handled = true;
	}

	private void ChevronContainer_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
	{
		CancelChevronPress(e.Pointer);
	}

	private void ChevronContainer_Tapped(object sender, TappedRoutedEventArgs e)
	{
		e.Handled = true;
	}

	private void CancelChevronPress(Pointer pointer)
	{
		if (!_isChevronPressed)
			return;

		_isChevronPressed = false;
		_isPressed = false;
		_chevronContainer?.ReleasePointerCapture(pointer);
		UpdateBackgroundState();
		UpdateChevronState();
	}

	protected virtual void Invoke()
	{
		if (ShouldShowChildrenInFlyout())
		{
			ShowChildrenFlyout();
			return;
		}

		if (IsGroupHeader())
		{
			ToggleExpansion();
			return;
		}

		Owner?.RaiseItemInvoked(this);
	}

	internal void InvokeFromAutomationPeer()
	{
		Invoke();
	}

	internal void ApplyOwnerTemplates()
	{
	}

	internal void PrepareChildItems()
	{
	}

	internal void UpdateStateFromOwner()
	{
		UpdateTemplateSettings();
		UpdateDisplayModeState();
		UpdateFlyoutItemsSource();
		UpdateSelectionFromOwner();
		UpdateExpansionState();
		UpdateSelectionState();
	}

	internal void SynchronizeItemExpansion()
	{
		if (TryGetSidebarItemModel(out var model) && IsExpanded != model.IsExpanded)
			IsExpanded = model.IsExpanded;
	}

	internal void UpdateExpansionState()
	{
		UpdateTemplateSettings();
		UpdateDisplayModeState();
		UpdateFlyoutItemsSource();

		if (!HasChildren())
		{
			SetFlyoutOpen(false);
			VisualStateManager.GoToState(this, "NoChildren", true);
			UpdateChevronState();
			return;
		}

		if (ShouldShowChildrenInFlyout())
		{
			VisualStateManager.GoToState(this, "ChildrenInFlyout", true);
			UpdateChevronState();
			return;
		}

		SetFlyoutOpen(false);

		if (IsLeafWithChildren())
		{
			VisualStateManager.GoToState(this, "LeafWithChildren", true);
			UpdateChevronState();
			return;
		}

		VisualStateManager.GoToState(this, IsExpanded ? "Expanded" : "Collapsed", true);
		UpdateChevronState();
	}

	internal bool HasChildren()
	{
		if (TryGetSidebarItemModel(out var model))
			return HasChildren(model);

		return Children is IList { Count: > 0 };
	}

	internal bool HasSelectedDescendant(object? selectedItem)
	{
		if (selectedItem is null || !TryGetSidebarItemModel(out var model))
			return false;

		return ContainsDescendantItemValue(selectedItem, model.Children);
	}

	internal bool IsSelectedItem(object? selectedItem)
	{
		return Equals(this, selectedItem) ||
			Equals(ResolvedItemValue, selectedItem);
	}

	internal bool TryGetSidebarItemModel(out ISidebarItemModel model)
	{
		switch (Item)
		{
			case ISidebarItemModel itemModel:
				model = itemModel;
				return true;

			case FlatSidebarItem flatSidebarItem:
				model = flatSidebarItem.Item;
				return true;
		}

		if (DataContext is FlatSidebarItem dataContextFlatSidebarItem)
		{
			model = dataContextFlatSidebarItem.Item;
			return true;
		}

		model = null!;
		return false;
	}

	private bool ToggleExpansion()
	{
		if (!HasChildren())
			return false;

		if (ShouldShowChildrenInFlyout())
		{
			ShowChildrenFlyout();
			return true;
		}

		IsExpanded = !IsExpanded;
		return true;
	}

	private void InvokeChevron()
	{
		if (ShouldShowChildrenInFlyout())
		{
			ShowChildrenFlyout();
			return;
		}

		ToggleExpansion();
	}

	private bool IsGroupHeader()
	{
		return HasChildren() && !IsLeafWithChildren();
	}

	private bool IsLeafWithChildren()
	{
		return TryGetSidebarItemModel(out var model) && model.IsLeafWithChildren;
	}

	private void UpdateSelectionFromOwner()
	{
		if (Owner is null)
			return;

		var selectedItem = Owner.SelectedItem;
		var isSelected = IsSelectedItem(selectedItem);
		isSelected |= (Owner.IsClosedCompact || !IsExpanded) && HasSelectedDescendant(selectedItem);
		if (IsSelected != isSelected)
			IsSelected = isSelected;

		if (IsSelectedItem(selectedItem))
			Owner.UpdateSelectedItemContainer(this);
	}

	private void UpdateTemplateSettings()
	{
		TemplateSettings.IndentWidth = ShouldUseCompactLayout()
			? 0d
			: Math.Max(0, Depth) * 16d;
	}

	private void UpdateDisplayModeState()
	{
		VisualStateManager.GoToState(this, ShouldUseCompactLayout() ? "Compact" : "NonCompact", true);
	}

	private bool ShouldUseCompactLayout()
	{
		return Owner?.IsClosedCompact == true && !IsInFlyout;
	}

	private bool ShouldShowChildrenInFlyout()
	{
		return HasChildren() && (ShouldUseCompactLayout() || IsInFlyout);
	}

	private void ShowChildrenFlyout()
	{
		if (!HasChildren())
			return;

		IsExpanded = true;
		UpdateFlyoutItemsSource();
		SetFlyoutOpen(true);
	}

	private void SetFlyoutOpen(bool isOpen)
	{
		if (_rootGrid is null)
			return;

		var attachedFlyout = FlyoutBase.GetAttachedFlyout(_rootGrid);
		if (attachedFlyout is null)
			return;

		try
		{
			if (isOpen)
			{
				FlyoutBase.ShowAttachedFlyout(_rootGrid);
			}
			else
			{
				_suppressChildrenFlyoutClosing = true;
				try
				{
					attachedFlyout.Hide();
				}
				finally
				{
					_suppressChildrenFlyoutClosing = false;
				}
			}
		}
		catch (ArgumentException)
		{
		}
	}

	private void UpdateFlyoutItemsSource()
	{
		if (_flyoutChildrenPresenter is null)
			return;

		_flyoutChildrenPresenter.ItemsSource = TryGetSidebarItemModel(out var model)
			? model.Children
			: Children;
	}

	private void FlyoutChildrenPresenter_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
	{
		if (args.Element is not SidebarViewItem item)
			return;

		item.Owner = Owner;
		item.IsInFlyout = true;
		item.Depth = 0;
		item.UpdateStateFromOwner();
	}

	private void ChildrenFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
	{
		if (_suppressChildrenFlyoutClosing)
			return;

		if (ShouldShowChildrenInFlyout() && IsExpanded)
			IsExpanded = false;
	}

	private static bool HasChildren(ISidebarItemModel model)
	{
		if (model.HasUnrealizedChildren)
			return true;

		if (model.Children is not IEnumerable children || model.Children is string)
			return false;

		if (children is ICollection collection)
			return collection.Count > 0;

		var enumerator = children.GetEnumerator();
		try
		{
			return enumerator.MoveNext();
		}
		finally
		{
			if (enumerator is IDisposable disposable)
				disposable.Dispose();
		}
	}

	private static bool ContainsDescendantItemValue(object selectedItem, object? children)
	{
		if (children is not IEnumerable childItems || children is string)
			return false;

		foreach (var childItem in childItems)
		{
			if (childItem is FlatSidebarItem flatSidebarItem)
			{
				if (Equals(flatSidebarItem.Item, selectedItem))
					return true;

				if (ContainsDescendantItemValue(selectedItem, flatSidebarItem.Item.Children))
					return true;

				continue;
			}

			if (Equals(childItem, selectedItem))
				return true;

			if (childItem is ISidebarItemModel sidebarItemModel &&
				ContainsDescendantItemValue(selectedItem, sidebarItemModel.Children))
			{
				return true;
			}
		}

		return false;
	}

	protected void UpdateSelectionState()
	{
		VisualStateManager.GoToState(this, IsSelected ? VisualStateNameSelected : VisualStateNameUnselected, true);
		UpdateBackgroundState();
	}

	private void UpdateChevronState()
	{
		var chevronState = HasChildren() && !ShouldUseCompactLayout()
			? IsExpanded ? ChevronState.VisibleOpen : ChevronState.VisibleClosed
			: ChevronState.Hidden;

		var pointerChevronStateName = GetPointerChevronStateName(chevronState);
		VisualStateManager.GoToState(this, pointerChevronStateName, true);

		var chevronStateName = chevronState switch
		{
			ChevronState.VisibleOpen => VisualStateNameChevronVisibleOpen,
			ChevronState.VisibleClosed => VisualStateNameChevronVisibleClosed,
			_ => VisualStateNameChevronHidden,
		};

		VisualStateManager.GoToState(this, chevronStateName, true);
	}

	private string GetPointerChevronStateName(ChevronState chevronState)
	{
		if (_isPressed)
		{
			return chevronState switch
			{
				ChevronState.VisibleOpen => VisualStateNamePressedChevronVisibleOpen,
				ChevronState.VisibleClosed => VisualStateNamePressedChevronVisibleClosed,
				_ => VisualStateNamePressedChevronHidden,
			};
		}

		if (_isPointerOver)
		{
			return chevronState switch
			{
				ChevronState.VisibleOpen => VisualStateNamePointerOverChevronVisibleOpen,
				ChevronState.VisibleClosed => VisualStateNamePointerOverChevronVisibleClosed,
				_ => VisualStateNamePointerOverChevronHidden,
			};
		}

		return chevronState switch
		{
			ChevronState.VisibleOpen => VisualStateNameNormalChevronVisibleOpen,
			ChevronState.VisibleClosed => VisualStateNameNormalChevronVisibleClosed,
			_ => VisualStateNameNormalChevronHidden,
		};
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

	private static bool IsPointerOverElement(FrameworkElement? element, PointerRoutedEventArgs e)
	{
		if (element is null)
			return false;

		var position = e.GetCurrentPoint(element).Position;
		return position.X >= 0 &&
			position.Y >= 0 &&
			position.X <= element.ActualWidth &&
			position.Y <= element.ActualHeight;
	}

	private enum ChevronState
	{
		Hidden,
		VisibleOpen,
		VisibleClosed,
	}
}
