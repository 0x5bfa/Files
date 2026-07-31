// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using Files.App2.Adapters;
using Files.App2.Commands;
using Files.Core.AppModels;
using Files.Core.Browsing;
using Files.Core.Data;
using Microsoft.UI.Dispatching;

namespace Files.App2.ViewModels;

public enum FolderViewMode
{
	Details,
	Grid,
	List,
}

public sealed class FolderBrowserViewModel : ObservableObject, IDisposable
{
	private readonly CoreBrowseAdapter browseAdapter;
	private string? operationError;
	private IReadOnlyList<BrowseItemViewModel>? appliedItems;
	private bool isApplyingUpdate;
	private int isDisposed;
	private FolderViewMode viewMode = FolderViewMode.Details;

	public FolderBrowserViewModel(
		PaneModel pane,
		IFilesDataRoot dataRoot,
		DispatcherQueue dispatcherQueue,
		WindowCommandManager commandManager)
	{
		ArgumentNullException.ThrowIfNull(commandManager);
		CommandManager = commandManager;
		browseAdapter = new CoreBrowseAdapter(pane, dataRoot, dispatcherQueue);
		browseAdapter.Updated += BrowseAdapter_Updated;
	}

	internal WindowCommandManager CommandManager { get; }

	public ObservableCollection<BrowseItemViewModel> Items { get; } = [];

	public FolderViewMode ViewMode
	{
		get => viewMode;
		private set => SetProperty(ref viewMode, value);
	}

	public bool IsApplyingUpdate => isApplyingUpdate;

	public IReadOnlyList<StorableKey> SelectedKeys => browseAdapter.SelectedKeys;

	public string LocationText => browseAdapter.LocationText;

	public bool IsLoading => browseAdapter.IsLoading;

	public bool CanGoBack => browseAdapter.CanGoBack;

	public bool CanGoForward => browseAdapter.CanGoForward;

	public bool CanGoUp => browseAdapter.CanGoUp;

	public bool CanRefresh => !IsLoading;

	public string StatusText =>
		operationError
		?? browseAdapter.ErrorMessage
		?? browseAdapter.StatusText;

	public Task InitializeAsync(CancellationToken cancellationToken = default) =>
		browseAdapter.InitializeAsync(cancellationToken);

	public Task NavigateToPathAsync(
		string path,
		CancellationToken cancellationToken = default) =>
		browseAdapter.NavigateToPathAsync(path, cancellationToken);

	public Task NavigateToItemAsync(
		BrowseItemViewModel item,
		CancellationToken cancellationToken = default) =>
		browseAdapter.NavigateToItemAsync(item, cancellationToken);

	public Task GoBackAsync(CancellationToken cancellationToken = default) =>
		browseAdapter.GoBackAsync(cancellationToken);

	public Task GoForwardAsync(CancellationToken cancellationToken = default) =>
		browseAdapter.GoForwardAsync(cancellationToken);

	public Task GoUpAsync(CancellationToken cancellationToken = default) =>
		browseAdapter.GoUpAsync(cancellationToken);

	public Task RefreshAsync(CancellationToken cancellationToken = default) =>
		browseAdapter.RefreshAsync(cancellationToken);

	public void SetSelection(IEnumerable<BrowseItemViewModel> selectedItems) =>
		browseAdapter.SetSelection(selectedItems);

	public void SetViewMode(FolderViewMode mode)
	{
		if (!Enum.IsDefined(mode))
		{
			throw new ArgumentOutOfRangeException(nameof(mode));
		}

		ViewMode = mode;
	}

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
			OnPropertyChanged(nameof(CanRefresh));
			OnPropertyChanged(nameof(StatusText));
		}
		finally
		{
			isApplyingUpdate = false;
		}
	}
}
