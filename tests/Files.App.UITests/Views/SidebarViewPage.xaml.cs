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
			new SidebarViewSampleHeaderItem("Pinned", "\uE838",
			[
				new SidebarViewSampleItem("Desktop", "\uEDA2"),
				new SidebarViewSampleItem("Downloads", "\uE968"),
				new SidebarViewSampleItem("Documents", "\uE968"),
				new SidebarViewSampleItem("Pictures", "\uE968"),
				new SidebarViewSampleItem("Music", "\uE968"),
				new SidebarViewSampleItem("Videos", "\uE968"),
				new SidebarViewSampleItem("Recycle Bin", "\uE968"),
			]),
			new SidebarViewSampleHeaderItem("This PC", "\uE838",
			[
				new SidebarViewSampleItem("Local Disk (C:)", "\uEDA2"),
				new SidebarViewSampleItem("Local Disk (D:)", "\uEDA2"),
			]),
		];

		public ObservableCollection<object> FooterItems { get; } =
		[
			new SidebarViewSampleItem("Settings", "\uE713"),
		];

		public Dictionary<SidebarViewDisplayMode, string> DisplayModes { get; } = new()
		{
			[SidebarViewDisplayMode.Auto] = "Auto",
			[SidebarViewDisplayMode.LeftCompact] = "LeftCompact",
			[SidebarViewDisplayMode.LeftMinimal] = "LeftMinimal",
			[SidebarViewDisplayMode.Left] = "Left",
		};

		public SidebarViewPage()
		{
			InitializeComponent();
		}

		private void DisplayModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (sender is not ComboBox comboBox || comboBox.SelectedValue is not SidebarViewDisplayMode displayMode)
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
