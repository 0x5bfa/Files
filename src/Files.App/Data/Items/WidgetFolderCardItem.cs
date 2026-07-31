// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Media.Imaging;

namespace Files.App.Data.Items
{
	public sealed partial class WidgetFolderCardItem : WidgetCardItem, IWidgetCardItem<IWindowsStorable>, IDisposable
	{
		// Properties

		public string? AutomationProperties { get; set; }

		public new IWindowsStorable Item { get; private set; }

		public string? Text { get; set; }

		public bool IsPinned { get; set; }

		public string Tooltip { get; set; }

		private BitmapImage? _Thumbnail;
		public BitmapImage? Thumbnail { get => _Thumbnail; set => SetProperty(ref _Thumbnail, value); }

		// Constructor

		public WidgetFolderCardItem(IWindowsStorable item, string text, bool isPinned, string tooltip)
		{
			AutomationProperties = text;
			Item = item;
			Text = text;
			IsPinned = isPinned;
			Path = item.FileSystemPath ?? item.ParsingName;
			Tooltip = tooltip;
		}

		// Methods

		public async Task LoadCardThumbnailAsync()
		{
			if (string.IsNullOrEmpty(Path))
				return;

			Thumbnail = await NavigationHelpers.GetIconForPathAsync(Path) as BitmapImage;
		}

		public void Dispose()
		{
			// The Core runtime owns the Windows source and its Shell scheduler.
		}
	}
}
