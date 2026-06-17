// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App.Controls;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Files.App.UITests.Views
{
	public sealed partial class SidebarViewPage : Page
	{
		public ObservableCollection<ISidebarItemModel> SampleItems { get; } =
		[
			new SidebarViewSampleItem("Home", "\uE80F"),
			new SidebarViewSampleHeaderItem("Pinned", "\uE838",
			[
				new SidebarViewSampleItem("Desktop", "\uEDA2"),
				new SidebarViewSampleItem("Downloads", "\uE968"),
				new SidebarViewSampleItem("Documents", "\uE968",
				[
					new SidebarViewSampleItem("Work", "\uE8B7",
					[
						new SidebarViewSampleItem("Reports", "\uE8A5"),
						new SidebarViewSampleItem("Invoices", "\uE8A5"),
					])
					{
						IsExpanded = true,
					},
					new SidebarViewSampleItem("Personal", "\uE8B7",
					[
						new SidebarViewSampleItem("Scans", "\uE8A5"),
					]),
				])
				{
					IsExpanded = true,
				},
				new SidebarViewSampleItem("Pictures", "\uE968"),
				new SidebarViewSampleItem("Music", "\uE968"),
				new SidebarViewSampleItem("Videos", "\uE968"),
				new SidebarViewSampleItem("Recycle Bin", "\uE968"),
			])
			{
				IsExpanded = true,
			},
			new SidebarViewSampleHeaderItem("This PC", "\uE838",
			[
				new SidebarViewSampleItem("Local Disk (C:)", "\uEDA2"),
				new SidebarViewSampleItem("Local Disk (D:)", "\uEDA2"),
			]),
		];

		public ObservableCollection<ISidebarItemModel> FooterItems { get; } =
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

	public class SidebarViewSampleItem : ISidebarItemModel
	{
		private bool _isExpanded;

		public SidebarViewSampleItem(string text, string glyph, ObservableCollection<ISidebarItemModel>? children = null)
		{
			Text = text;
			Glyph = glyph;
			Children = children;
		}

		public string Text { get; }
		public string Glyph { get; }
		public ObservableCollection<ISidebarItemModel>? Children { get; }
		public string? Path => null;
		public object ToolTip => Text;
		public object? ItemDecorator => null;
		public IconElement IconElement => new FontIcon
		{
			Width = 16,
			Height = 16,
			FontSize = 16,
			Glyph = Glyph,
		};

		object? ISidebarItemModel.Children => Children;

		public virtual bool IsLeafWithChildren => Children is not null;

		public bool IsExpanded
		{
			get => _isExpanded;
			set
			{
				if (_isExpanded == value)
					return;

				_isExpanded = value;
				PropertyChanged?.Invoke(this, new(nameof(IsExpanded)));
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;
	}

	public sealed class SidebarViewSampleHeaderItem(string text, string glyph, ObservableCollection<ISidebarItemModel>? children = null)
		: SidebarViewSampleItem(text, glyph, children)
	{
		public override bool IsLeafWithChildren => false;
	}

	public sealed class SidebarViewSampleSeparatorItem();
}
