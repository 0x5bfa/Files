// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;
using Files.App.UITests.Data;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Files.App.UITests.Views
{
	internal sealed partial class TableViewPage : Page
	{
		private static readonly TableViewItemModel[] SampleItems =
		[
			new() { Name = "Designs", DateUpdated = "Today, 9:14 AM", Type = "File folder", Size = string.Empty },
			new() { Name = "Quarterly Report Q1 2026.xlsx", DateUpdated = "Today, 8:02 AM", Type = "Microsoft Excel Worksheet", Size = "2.4 MB" },
			new() { Name = "Product Roadmap.pptx", DateUpdated = "Yesterday, 4:42 PM", Type = "Microsoft PowerPoint Presentation", Size = "18.7 MB" },
			new() { Name = "Brand Guidelines.pdf", DateUpdated = "Yesterday, 2:18 PM", Type = "PDF Document", Size = "6.1 MB" },
			new() { Name = "HeroBanner_Final.png", DateUpdated = "Mar 6, 2026 11:31 AM", Type = "PNG File", Size = "4.8 MB" },
			new() { Name = "SpringCampaign", DateUpdated = "Mar 6, 2026 9:07 AM", Type = "File folder", Size = string.Empty },
			new() { Name = "Invoice_10482.docx", DateUpdated = "Mar 5, 2026 3:26 PM", Type = "Microsoft Word Document", Size = "184 KB" },
			new() { Name = "Onboarding Checklist.txt", DateUpdated = "Mar 5, 2026 10:13 AM", Type = "Text Document", Size = "12 KB" },
			new() { Name = "Team Photo.jpg", DateUpdated = "Mar 4, 2026 6:54 PM", Type = "JPEG File", Size = "3.2 MB" },
			new() { Name = "ReleaseNotes-v3.8.md", DateUpdated = "Mar 4, 2026 1:22 PM", Type = "Markdown Source File", Size = "28 KB" },
			new() { Name = "Customer Interviews", DateUpdated = "Mar 3, 2026 5:40 PM", Type = "File folder", Size = string.Empty },
			new() { Name = "Demo Reel.mp4", DateUpdated = "Mar 3, 2026 11:08 AM", Type = "MP4 Video", Size = "124 MB" },
			new() { Name = "appsettings.json", DateUpdated = "Mar 2, 2026 8:49 PM", Type = "JSON Source File", Size = "5 KB" },
			new() { Name = "prototype-v12.fig", DateUpdated = "Mar 2, 2026 2:16 PM", Type = "FIG File", Size = "31.5 MB" },
			new() { Name = "Assets", DateUpdated = "Mar 1, 2026 4:03 PM", Type = "File folder", Size = string.Empty },
			new() { Name = "archive-2025.zip", DateUpdated = "Feb 28, 2026 7:12 PM", Type = "Compressed (zipped) Folder", Size = "842 MB" },
			new() { Name = "setup.exe", DateUpdated = "Feb 28, 2026 10:55 AM", Type = "Application", Size = "67.9 MB" },
			new() { Name = "Vacation Budget.csv", DateUpdated = "Feb 27, 2026 1:47 PM", Type = "CSV File", Size = "96 KB" },
			new() { Name = "wireframes.sketch", DateUpdated = "Feb 26, 2026 9:33 AM", Type = "SKETCH File", Size = "14.2 MB" },
			new() { Name = "Meeting Recording.m4a", DateUpdated = "Feb 25, 2026 5:11 PM", Type = "M4A Audio File", Size = "48.6 MB" },
		];

		public ObservableCollection<TableViewColumnModel> Columns { get; } =
		[
			new("Name" , nameof(TableViewItemModel.Name)),
			new("Date updated", nameof(TableViewItemModel.DateUpdated)),
			new("Type" , nameof(TableViewItemModel.Type)),
			new("Size" , nameof(TableViewItemModel.Size)),
		];

		public ObservableCollection<TableViewItemModel> Items { get; set; }

		public TableViewPage()
		{
			Items = [];

			InitializeComponent();
		}

		private async void TableViewPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
		{
			var list = await Task.Run(() => new List<TableViewItemModel>(SampleItems));

			await DispatcherQueue.EnqueueAsync(() =>
			{
				Items.Clear();

				foreach (var item in list)
					Items.Add(item);

				Items = [.. list];
			});

		}
	}
}
