// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Files.App.Controls
{
	public partial class BreadcrumbBarItem
	{
		private void ItemButton_PointerEntered(object sender, PointerRoutedEventArgs e)
		{
			VisualStateManager.GoToState(this, "PointerOverItem", true);
		}

		private void ItemButton_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			VisualStateManager.GoToState(this, "PointerPressedOnItem", true);
		}

		private void ItemButton_PointerReleased(object sender, PointerRoutedEventArgs e)
		{
			OnItemClicked();

			VisualStateManager.GoToState(this, "PointerOverItem", true);
		}

		private void ItemButton_PointerExited(object sender, PointerRoutedEventArgs e)
		{
			VisualStateManager.GoToState(this, "PointerNormal", true);
		}

		private void ChevronButton_PointerEntered(object sender, PointerRoutedEventArgs e)
		{
			VisualStateManager.GoToState(this, "PointerOverChevron", true);
		}

		private void ChevronButton_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			VisualStateManager.GoToState(this, "PointerPressedOnChevron", true);
		}

		private void ChevronButton_PointerReleased(object sender, PointerRoutedEventArgs e)
		{
			VisualStateManager.GoToState(this, "PointerOverChevron", true);

			FlyoutBase.ShowAttachedFlyout(_itemChevronButton);
		}

		private void ChevronButton_PointerExited(object sender, PointerRoutedEventArgs e)
		{
			VisualStateManager.GoToState(this, "PointerNormal", true);
		}

		private void ChevronDropDownMenuFlyout_Opening(object? sender, object e)
		{
			if (_ownerRef is null ||
				_ownerRef.TryGetTarget(out var breadcrumbBar) is false ||
				sender is not MenuFlyout flyout)
				return;

			breadcrumbBar.RaiseItemDropDownFlyoutOpening(this, flyout);
		}

		private void ChevronDropDownMenuFlyout_Opened(object? sender, object e)
		{
			VisualStateManager.GoToState(this, "ChevronNormalOn", true);
		}

		private void ChevronDropDownMenuFlyout_Closed(object? sender, object e)
		{
			if (_ownerRef is null ||
				_ownerRef.TryGetTarget(out var breadcrumbBar) is false ||
				sender is not MenuFlyout flyout)
				return;

			breadcrumbBar.RaiseItemDropDownFlyoutClosed(this, flyout);

			VisualStateManager.GoToState(this, "ChevronNormalOff", true);
			VisualStateManager.GoToState(this, "PointerNormal", true);
		}
	}
}
