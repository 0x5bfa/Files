// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App2.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Files.App2.Views;

public sealed partial class PaneContentView : UserControl
{
	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(
			nameof(ViewModel),
			typeof(PaneViewModel),
			typeof(PaneContentView),
			new PropertyMetadata(null, ViewModelChanged));

	public PaneContentView()
	{
		InitializeComponent();
	}

	public PaneViewModel? ViewModel
	{
		get => (PaneViewModel?)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}

	private static void ViewModelChanged(
		DependencyObject sender,
		DependencyPropertyChangedEventArgs args)
	{
		if (sender is not PaneContentView view)
		{
			return;
		}

		if (args.OldValue is PaneViewModel oldViewModel)
		{
			oldViewModel.PropertyChanged -= view.ViewModel_PropertyChanged;
		}

		if (args.NewValue is PaneViewModel newViewModel)
		{
			newViewModel.PropertyChanged += view.ViewModel_PropertyChanged;
		}

		view.UpdateContent();
	}

	private void ViewModel_PropertyChanged(
		object? sender,
		PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(PaneViewModel.ContentKind))
		{
			UpdateContent();
		}
	}

	private void UpdateContent()
	{
		if (ViewModel is not { } viewModel)
		{
			PaneContentPresenter.Content = null;
			PaneContentPresenter.ContentTemplate = null;
			return;
		}

		PaneContentPresenter.Content = viewModel;
		PaneContentPresenter.ContentTemplate = viewModel.ContentKind switch
		{
			PaneContentKind.FolderBrowser =>
				(DataTemplate)PaneContentPresenter.Resources["FolderBrowserTemplate"],
			PaneContentKind.Settings =>
				(DataTemplate)PaneContentPresenter.Resources["SettingsTemplate"],
			PaneContentKind.Web =>
				(DataTemplate)PaneContentPresenter.Resources["WebTemplate"],
			_ => throw new InvalidOperationException(
				$"Unsupported pane content kind: {viewModel.ContentKind}."),
		};
	}
}
