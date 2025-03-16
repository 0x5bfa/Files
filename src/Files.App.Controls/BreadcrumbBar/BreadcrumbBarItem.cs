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
	public partial class BreadcrumbBarItem : ContentControl
	{
		// Constants

		private const string TemplatePartName_ItemContentButton = "PART_ItemContentButton";
		private const string TemplatePartName_ItemChevronButton = "PART_ItemChevronButton";
		private const string TemplatePartName_ItemEllipsisDropDownMenuFlyout = "PART_ItemEllipsisDropDownMenuFlyout";
		private const string TemplatePartName_ItemChevronDropDownMenuFlyout = "PART_ItemChevronDropDownMenuFlyout";

		// Fields

		private WeakReference<BreadcrumbBar>? _ownerRef;

		private Border _itemContentButton = null!;
		private Border _itemChevronButton = null!;
		private MenuFlyout _itemEllipsisDropDownMenuFlyout = null!;
		private MenuFlyout _itemChevronDropDownMenuFlyout = null!;

		// Constructor

		public BreadcrumbBarItem()
		{
			DefaultStyleKey = typeof(BreadcrumbBarItem);
		}

		// Methods

		protected override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			_itemContentButton = GetTemplateChild(TemplatePartName_ItemContentButton) as Border
				?? throw new MissingFieldException($"Could not find {TemplatePartName_ItemContentButton} in the given {nameof(BreadcrumbBarItem)}'s style.");
			_itemChevronButton = GetTemplateChild(TemplatePartName_ItemChevronButton) as Border
				?? throw new MissingFieldException($"Could not find {TemplatePartName_ItemChevronButton} in the given {nameof(BreadcrumbBarItem)}'s style.");
			_itemEllipsisDropDownMenuFlyout = GetTemplateChild(TemplatePartName_ItemEllipsisDropDownMenuFlyout) as MenuFlyout
				?? throw new MissingFieldException($"Could not find {TemplatePartName_ItemEllipsisDropDownMenuFlyout} in the given {nameof(BreadcrumbBarItem)}'s style.");
			_itemChevronDropDownMenuFlyout = GetTemplateChild(TemplatePartName_ItemChevronDropDownMenuFlyout) as MenuFlyout
				?? throw new MissingFieldException($"Could not find {TemplatePartName_ItemChevronDropDownMenuFlyout} in the given {nameof(BreadcrumbBarItem)}'s style.");

			if (IsEllipsis || IsLastItem)
				VisualStateManager.GoToState(this, "ChevronCollapsed", true);

			_itemContentButton.PointerEntered += ItemButton_PointerEntered;
			_itemContentButton.PointerPressed += ItemButton_PointerPressed;
			_itemContentButton.PointerReleased += ItemButton_PointerReleased;
			_itemContentButton.PointerExited += ItemButton_PointerExited;

			_itemChevronButton.PointerEntered += ChevronButton_PointerEntered;
			_itemChevronButton.PointerPressed += ChevronButton_PointerPressed;
			_itemChevronButton.PointerReleased += ChevronButton_PointerReleased;
			_itemChevronButton.PointerExited += ChevronButton_PointerExited;

			_itemChevronDropDownMenuFlyout.Opening += ChevronDropDownMenuFlyout_Opening;
			_itemChevronDropDownMenuFlyout.Opened += ChevronDropDownMenuFlyout_Opened;
			_itemChevronDropDownMenuFlyout.Closed += ChevronDropDownMenuFlyout_Closed;
		}

		public void OnItemClicked()
		{
			if (_ownerRef is not null &&
				_ownerRef.TryGetTarget(out var breadcrumbBar))
			{
				if (IsEllipsis)
				{
					// Clear items in the ellipsis flyout
					_itemEllipsisDropDownMenuFlyout.Items.Clear();

					// Populate items in the ellipsis flyout
					for (int index = 0; index < breadcrumbBar.IndexAfterEllipsis; index++)
					{
						if (breadcrumbBar.TryGetElement(index, out var item) && item?.Content is string text)
						{
							_itemEllipsisDropDownMenuFlyout.Items.Add(new MenuFlyoutItem() { Text = text });
						}
					}

					// Open the ellipsis flyout
					FlyoutBase.ShowAttachedFlyout(_itemContentButton);
				}
				else
				{
					// Fire a click event
					breadcrumbBar.RaiseItemClickedEvent(this);
				}
			}
		}

		public void SetOwner(BreadcrumbBar breadcrumbBar)
		{
			_ownerRef = new(breadcrumbBar);
		}
	}
}
