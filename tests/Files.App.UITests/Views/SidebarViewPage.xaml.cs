// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App.Controls;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Files.App.UITests.Views
{
	public sealed partial class SidebarViewPage : Page
	{
		public ObservableCollection<object> SampleItems { get; } =
		[
			new SidebarViewSampleItem("Home", "\uE80F"),
			new SidebarViewSampleHeaderItem("Locations", "\uE838",
			[
				new SidebarViewSampleItem("Favorite", "\uE840", [
					new SidebarViewSampleItem("Network", "\uE968"),
					new SidebarViewSampleItem("Network", "\uE968"),
					new SidebarViewSampleItem("Network", "\uE968"),
				]),
				new SidebarViewSampleItem("Drives", "\uEDA2"),
				new SidebarViewSampleItem("Network", "\uE968"),
			]),
			new SidebarViewSampleItem("Pinned", "\uE80F"),
			new SidebarViewSampleSeparatorItem(),
			new SidebarViewSampleItem("Family", "\uE840"),
		];

		public ObservableCollection<object> FooterItems { get; } =
		[
			new SidebarViewSampleItem("Settings", "\uE713"),
		];

		public Dictionary<SidebarDisplayMode, string> DisplayModes { get; } = new()
		{
			[SidebarDisplayMode.Compact] = "Compact",
			[SidebarDisplayMode.Minimal] = "Minimal",
			[SidebarDisplayMode.Expanded] = "Expanded",
		};

		public SidebarViewPage()
		{
			InitializeComponent();
		}

		private void DisplayModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (sender is not ComboBox comboBox || comboBox.SelectedValue is not SidebarDisplayMode displayMode)
				return;

			SampleSidebarView.DisplayMode = displayMode;
		}
	}

	public sealed class SidebarViewSampleItem(string text, string glyph, ObservableCollection<SidebarViewSampleItem>? children = null)
	{
		public string Text { get; set; } = text;
		public string Glyph { get; set; } = glyph;
		public ObservableCollection<SidebarViewSampleItem>? Children { get; set; } = children;
	}

	public sealed class SidebarViewSampleHeaderItem(string text, string glyph, ObservableCollection<SidebarViewSampleItem>? children = null)
	{
		public string Text { get; set; } = text;
		public string Glyph { get; set; } = glyph;
		public ObservableCollection<SidebarViewSampleItem>? Children { get; set; } = children;
		public bool IsExpanded { get; set; } = false;
	}

	public sealed class SidebarViewSampleSeparatorItem();
}
