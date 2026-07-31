// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App2.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Files.App2.Views;

public sealed partial class ToolbarView : UserControl
{
	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(
			nameof(ViewModel),
			typeof(RootViewModel),
			typeof(ToolbarView),
			new PropertyMetadata(null));

	public ToolbarView()
	{
		InitializeComponent();
	}

	public RootViewModel? ViewModel
	{
		get => (RootViewModel?)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}
}
