// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App2.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Files.App2.Views;

public sealed partial class ListFolderView : UserControl
{
	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(
			nameof(ViewModel),
			typeof(FolderBrowserViewModel),
			typeof(ListFolderView),
			new PropertyMetadata(null, ViewModelChanged));

	private FolderViewInteraction? interaction;

	public ListFolderView()
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
		if (sender is not ListFolderView view)
		{
			return;
		}

		view.interaction?.Dispose();
		view.interaction = args.NewValue is FolderBrowserViewModel newViewModel
			? new FolderViewInteraction(view.ItemList, newViewModel)
			: null;
	}
}
