// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App2.ViewModels;
using Files.Core.AppModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Files.App2.Views;

public sealed partial class PaneHost : UserControl
{
	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(
			nameof(ViewModel),
			typeof(TabViewModel),
			typeof(PaneHost),
			new PropertyMetadata(null, ViewModelChanged));

	public PaneHost()
	{
		InitializeComponent();
	}

	public TabViewModel? ViewModel
	{
		get => (TabViewModel?)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}

	private static void ViewModelChanged(
		DependencyObject sender,
		DependencyPropertyChangedEventArgs args)
	{
		if (sender is not PaneHost paneHost)
		{
			return;
		}

		if (args.OldValue is TabViewModel oldViewModel)
		{
			oldViewModel.PropertyChanged -= paneHost.ViewModel_PropertyChanged;
		}

		if (args.NewValue is TabViewModel newViewModel)
		{
			newViewModel.PropertyChanged += paneHost.ViewModel_PropertyChanged;
		}

		paneHost.UpdateLayoutOrientation();
	}

	private void ViewModel_PropertyChanged(
		object? sender,
		PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(TabViewModel.SplitOrientation))
		{
			UpdateLayoutOrientation();
		}
	}

	private void UpdateLayoutOrientation()
	{
		if (ViewModel is { } viewModel)
		{
			PaneLayout.Orientation =
				viewModel.SplitOrientation is PaneSplitOrientation.Vertical
					? Orientation.Horizontal
					: Orientation.Vertical;
		}
	}

	private void PaneView_Activated(object sender, EventArgs e)
	{
		if (ViewModel is { } viewModel
			&& sender is PaneView { ViewModel: { } pane })
		{
			viewModel.SetActivePane(pane.Id);
		}
	}
}
