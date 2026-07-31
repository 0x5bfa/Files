// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App2.ViewModels;
using Microsoft.UI.Xaml;

namespace Files.App2.Views;

public sealed partial class FolderBrowser : Microsoft.UI.Xaml.Controls.UserControl
{
	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(
			nameof(ViewModel),
			typeof(FolderBrowserViewModel),
			typeof(FolderBrowser),
			new PropertyMetadata(null, ViewModelChanged));

	public FolderBrowser()
	{
		InitializeComponent();
	}

	public FolderBrowserViewModel? ViewModel
	{
		get => (FolderBrowserViewModel?)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}

	private static void ViewModelChanged(
		DependencyObject sender,
		DependencyPropertyChangedEventArgs args)
	{
		if (sender is not FolderBrowser folderBrowser)
		{
			return;
		}

		if (args.OldValue is FolderBrowserViewModel oldViewModel)
		{
			oldViewModel.PropertyChanged -= folderBrowser.ViewModel_PropertyChanged;
		}

		if (args.NewValue is FolderBrowserViewModel newViewModel)
		{
			newViewModel.PropertyChanged += folderBrowser.ViewModel_PropertyChanged;
		}

		folderBrowser.UpdateFolderView();
	}

	private void ViewModel_PropertyChanged(
		object? sender,
		PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(FolderBrowserViewModel.ViewMode))
		{
			UpdateFolderView();
		}
	}

	private void UpdateFolderView()
	{
		if (ViewModel is not { } viewModel)
		{
			FolderViewPresenter.Content = null;
			FolderViewPresenter.ContentTemplate = null;
			return;
		}

		FolderViewPresenter.Content = viewModel;
		FolderViewPresenter.ContentTemplate = viewModel.ViewMode switch
		{
			FolderViewMode.Details =>
				(DataTemplate)FolderViewPresenter.Resources["DetailsTemplate"],
			FolderViewMode.Grid =>
				(DataTemplate)FolderViewPresenter.Resources["GridTemplate"],
			FolderViewMode.List =>
				(DataTemplate)FolderViewPresenter.Resources["ListTemplate"],
			_ => throw new InvalidOperationException(
				$"Unsupported folder view mode: {viewModel.ViewMode}."),
		};
	}
}
