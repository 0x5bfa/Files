// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;
using System.Collections.Specialized;

namespace Files.App.Controls
{
	public sealed partial class SidebarItem : Control
	{
		[GeneratedDependencyProperty]
		public partial bool IsSelected { get; set; }

		[GeneratedDependencyProperty(DefaultValue = true)]
		public partial bool IsExpanded { get; set; }

		[GeneratedDependencyProperty(DefaultValue = 30D)]
		public partial double ChildrenPresenterHeight { get; set; }

		[GeneratedDependencyProperty]
		public partial bool UseReorderDrop { get; set; }

		[GeneratedDependencyProperty]
		public partial FrameworkElement? Icon { get; set; }

		[GeneratedDependencyProperty]
		public partial FrameworkElement? Decorator { get; set; }

		[GeneratedDependencyProperty(DefaultValue = SidebarDisplayMode.Expanded)]
		public partial SidebarDisplayMode DisplayMode { get; set; }

		[GeneratedDependencyProperty]
		public partial string? Text { get; set; }

		[GeneratedDependencyProperty]
		public partial object? ToolTip { get; set; }

		[GeneratedDependencyProperty]
		public partial bool IsPaddedItem { get; set; }

		[GeneratedDependencyProperty]
		public partial object? Children { get; set; }

		[GeneratedDependencyProperty, Obsolete]
		public partial object? Item { get; set; }

		[GeneratedDependencyProperty]
		public partial SidebarView? Owner { get; set; }

		[GeneratedDependencyProperty]
		public partial bool IsInFlyout { get; set; }

		partial void OnDisplayModePropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			SidebarDisplayModeChanged((SidebarDisplayMode)e.OldValue);
		}

		partial void OnIsSelectedPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			UpdateSelectionState();
		}

		partial void OnIsExpandedPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			UpdateExpansionState();
		}

		partial void OnItemPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			HandleItemChange();
		}

		partial void OnChildrenPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			if (e.OldValue is INotifyCollectionChanged oldNCC)
				oldNCC.CollectionChanged -= ChildItems_CollectionChanged;
			if (e.NewValue is INotifyCollectionChanged newNCC)
				newNCC.CollectionChanged += ChildItems_CollectionChanged;
		}

		partial void OnOwnerPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			if (Owner is null)
				return;

			DisplayMode = Owner.DisplayMode;

			Owner.RegisterPropertyChangedCallback(SidebarView.DisplayModeProperty, (sender, args) =>
			{
				DisplayMode = Owner.DisplayMode;
			});
			Owner.RegisterPropertyChangedCallback(SidebarView.SelectedItemProperty, (sender, args) =>
			{
				ReevaluateSelection();
			});

			ReevaluateSelection();
		}

		partial void OnIsInFlyoutPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			VisualStateManager.GoToState(this, DisplayMode is SidebarDisplayMode.Compact && !IsInFlyout ? "Compact" : "NonCompact", true);
		}
	}
}
