// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using Files.App2.Adapters;
using Files.Core.AppModels;
using Files.Core.Browsing;
using Files.Core.Data;
using Microsoft.UI.Dispatching;

namespace Files.App2.ViewModels;

public sealed class MainPageViewModel : ObservableObject, IDisposable
{
	private readonly CoreBrowseAdapter browseAdapter;
	private string? operationError;
	private IReadOnlyList<BrowseItemViewModel>? appliedItems;
	private bool isApplyingUpdate;
	private int isDisposed;

	public MainPageViewModel(
		PaneModel pane,
		IFilesDataRoot dataRoot,
		DispatcherQueue dispatcherQueue)
	{
		browseAdapter = new CoreBrowseAdapter(pane, dataRoot, dispatcherQueue);
		browseAdapter.Updated += BrowseAdapter_Updated;
	}

	public ObservableCollection<BrowseItemViewModel> Items { get; } = [];

	public bool IsApplyingUpdate => isApplyingUpdate;

	public IReadOnlyList<StorableKey> SelectedKeys => browseAdapter.SelectedKeys;

	public string LocationText => browseAdapter.LocationText;

	public bool IsLoading => browseAdapter.IsLoading;

	public bool CanGoBack => browseAdapter.CanGoBack;

	public bool CanGoForward => browseAdapter.CanGoForward;

	public bool CanGoUp => browseAdapter.CanGoUp;

	public string StatusText =>
		operationError
		?? browseAdapter.ErrorMessage
		?? browseAdapter.StatusText;

	public Task InitializeAsync() => browseAdapter.InitializeAsync();

	public Task NavigateToPathAsync(string path) =>
		browseAdapter.NavigateToPathAsync(path);

	public Task NavigateToItemAsync(BrowseItemViewModel item) =>
		browseAdapter.NavigateToItemAsync(item);

	public Task GoBackAsync() => browseAdapter.GoBackAsync();

	public Task GoForwardAsync() => browseAdapter.GoForwardAsync();

	public Task GoUpAsync() => browseAdapter.GoUpAsync();

	public Task RefreshAsync() => browseAdapter.RefreshAsync();

	public void SetSelection(IEnumerable<BrowseItemViewModel> selectedItems) =>
		browseAdapter.SetSelection(selectedItems);

	public void ReportOperationError(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		operationError = exception.Message;
		OnPropertyChanged(nameof(StatusText));
	}

	public void ReportOperationCanceled()
	{
		operationError = "The operation was canceled.";
		OnPropertyChanged(nameof(StatusText));
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref isDisposed, 1) is not 0)
		{
			return;
		}

		browseAdapter.Updated -= BrowseAdapter_Updated;
		browseAdapter.Dispose();
	}

	private void BrowseAdapter_Updated(object? sender, EventArgs args)
	{
		isApplyingUpdate = true;
		try
		{
			if (!ReferenceEquals(appliedItems, browseAdapter.Items))
			{
				Items.Clear();
				foreach (var item in browseAdapter.Items)
				{
					Items.Add(item);
				}

				appliedItems = browseAdapter.Items;
				OnPropertyChanged(nameof(Items));
			}

			operationError = null;
			OnPropertyChanged(nameof(SelectedKeys));
			OnPropertyChanged(nameof(LocationText));
			OnPropertyChanged(nameof(IsLoading));
			OnPropertyChanged(nameof(CanGoBack));
			OnPropertyChanged(nameof(CanGoForward));
			OnPropertyChanged(nameof(CanGoUp));
			OnPropertyChanged(nameof(StatusText));
		}
		finally
		{
			isApplyingUpdate = false;
		}
	}
}
