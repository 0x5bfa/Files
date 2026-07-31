// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App2.ViewModels;
using Files.Core.AppModels;
using Files.Core.Browsing;
using Files.Core.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using UiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace Files.App2.Views;

public sealed partial class MainPage : Page, IDisposable
{
	private readonly MainPageViewModel viewModel;
	private bool hasInitialized;
	private int isDisposed;
	private bool synchronizingSelection;

	public MainPage(
		PaneModel pane,
		IFilesDataRoot dataRoot,
		UiDispatcherQueue dispatcherQueue)
	{
		InitializeComponent();
		viewModel = new MainPageViewModel(pane, dataRoot, dispatcherQueue);
		viewModel.PropertyChanged += ViewModel_PropertyChanged;
	}

	public MainPageViewModel ViewModel => viewModel;

	private async void Page_Loaded(object sender, RoutedEventArgs e)
	{
		if (hasInitialized)
		{
			return;
		}

		hasInitialized = true;
		await ExecuteAsync(viewModel.InitializeAsync);
	}

	private async void BackButton_Click(object sender, RoutedEventArgs e) =>
		await ExecuteAsync(viewModel.GoBackAsync);

	private async void ForwardButton_Click(object sender, RoutedEventArgs e) =>
		await ExecuteAsync(viewModel.GoForwardAsync);

	private async void UpButton_Click(object sender, RoutedEventArgs e) =>
		await ExecuteAsync(viewModel.GoUpAsync);

	private async void HomeButton_Click(object sender, RoutedEventArgs e) =>
		await ExecuteAsync(() => viewModel.NavigateToPathAsync("Home"));

	private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
		await ExecuteAsync(viewModel.RefreshAsync);

	private async void GoButton_Click(object sender, RoutedEventArgs e) =>
		await NavigateToPathAsync();

	private async void PathTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (e.Key is not VirtualKey.Enter)
		{
			return;
		}

		e.Handled = true;
		await NavigateToPathAsync();
	}

	private async void ItemList_DoubleTapped(
		object sender,
		DoubleTappedRoutedEventArgs e)
	{
		if (ItemList.SelectedItem is BrowseItemViewModel item)
		{
			await ExecuteAsync(() => viewModel.NavigateToItemAsync(item));
		}
	}

	private void ItemList_SelectionChanged(
		object sender,
		SelectionChangedEventArgs e)
	{
		if (!synchronizingSelection && !viewModel.IsApplyingUpdate)
		{
			viewModel.SetSelection(
				ItemList.SelectedItems.OfType<BrowseItemViewModel>());
		}
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref isDisposed, 1) is not 0)
		{
			return;
		}

		viewModel.PropertyChanged -= ViewModel_PropertyChanged;
		viewModel.Dispose();
	}

	private async Task NavigateToPathAsync() =>
		await ExecuteAsync(() => viewModel.NavigateToPathAsync(PathTextBox.Text));

	private async Task ExecuteAsync(Func<Task> operation)
	{
		try
		{
			await operation();
		}
		catch (OperationCanceledException)
		{
			viewModel.ReportOperationCanceled();
		}
		catch (Exception exception)
		{
			viewModel.ReportOperationError(exception);
		}
	}

	private void ViewModel_PropertyChanged(
		object? sender,
		System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(MainPageViewModel.SelectedKeys))
		{
			SynchronizeSelection();
		}
	}

	private void SynchronizeSelection()
	{
		synchronizingSelection = true;
		try
		{
			var selectedKeys = viewModel.SelectedKeys;
			ItemList.SelectedItems.Clear();
			foreach (var item in viewModel.Items)
			{
				if (selectedKeys.Contains(item.Reference.GetKey()))
				{
					ItemList.SelectedItems.Add(item);
				}
			}
		}
		finally
		{
			synchronizingSelection = false;
		}
	}
}
