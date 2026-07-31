// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App2.ViewModels;
using Files.App2.Commands;
using Files.Core.Browsing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Files.App2.Views;

internal sealed class FolderViewInteraction : IDisposable
{
	private readonly ListViewBase listView;
	private readonly FolderBrowserViewModel viewModel;
	private bool synchronizingSelection;

	public FolderViewInteraction(
		ListViewBase listView,
		FolderBrowserViewModel viewModel)
	{
		this.listView = listView;
		this.viewModel = viewModel;

		listView.DoubleTapped += ListView_DoubleTapped;
		listView.SelectionChanged += ListView_SelectionChanged;
		viewModel.PropertyChanged += ViewModel_PropertyChanged;
		SynchronizeSelection();
	}

	public void Dispose()
	{
		listView.DoubleTapped -= ListView_DoubleTapped;
		listView.SelectionChanged -= ListView_SelectionChanged;
		viewModel.PropertyChanged -= ViewModel_PropertyChanged;
	}

	private async void ListView_DoubleTapped(
		object sender,
		DoubleTappedRoutedEventArgs e)
	{
		if (listView.SelectedItem is not BrowseItemViewModel item)
		{
			return;
		}

		await viewModel.CommandManager.ExecuteAsync(
			CommandIds.OpenItem,
			item);
	}

	private void ListView_SelectionChanged(
		object sender,
		SelectionChangedEventArgs e)
	{
		if (!synchronizingSelection && !viewModel.IsApplyingUpdate)
		{
			viewModel.SetSelection(
				listView.SelectedItems.OfType<BrowseItemViewModel>());
		}
	}

	private void ViewModel_PropertyChanged(
		object? sender,
		PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(FolderBrowserViewModel.SelectedKeys))
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
			listView.SelectedItems.Clear();
			foreach (var item in viewModel.Items)
			{
				if (selectedKeys.Contains(item.Reference.GetKey()))
				{
					listView.SelectedItems.Add(item);
				}
			}
		}
		finally
		{
			synchronizingSelection = false;
		}
	}
}
