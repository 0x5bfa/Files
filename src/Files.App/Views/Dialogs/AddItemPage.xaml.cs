// Copyright (c) 2024 Files Community
// Licensed under the MIT License. See the LICENSE.

using Files.App.ViewModels.Dialogs;
using Files.App.ViewModels.Dialogs.AddItemDialog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Files.App.Views.Dialogs
{
	public sealed partial class AddItemPage : Page
	{
		private FrameworkElement RootAppElement
			=> (FrameworkElement)MainWindow.Instance.Content;

		public AddItemDialogViewModel ViewModel { get; set; } = new();

		public AddItemPage()
		{
			InitializeComponent();

			Loaded += Page_Loaded;
		}

		private void ListView_ItemClick(object sender, ItemClickEventArgs e)
		{
			if (e.ClickedItem is AddItemDialogListItemViewModel item)
				ViewModel.ResultType = item.ItemResult;
		}

		private async void Page_Loaded(object sender, RoutedEventArgs e)
		{
			// Get all entries
			var windowsShellService = Ioc.Default.GetRequiredService<IAddItemService>();
			var itemTypes = windowsShellService.GetEntries();
			await ViewModel.AddItemsToListAsync(itemTypes);

			// Focus on the list view so users can use keyboard navigation
			AddItemsListView.Focus(FocusState.Programmatic);
		}
	}
}
