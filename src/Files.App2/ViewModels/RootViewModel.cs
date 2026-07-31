// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using Files.App2.Commands;
using Files.Core.AppModels;
using Files.Core.Browsing;
using Files.Core.Data;
using Microsoft.UI.Dispatching;

namespace Files.App2.ViewModels;

public sealed class RootViewModel : ObservableObject, IDisposable
{
	private readonly WindowModel window;
	private readonly IFilesDataRoot dataRoot;
	private readonly DispatcherQueue dispatcherQueue;
	private readonly WindowCommandManager commandManager;
	private readonly Dictionary<Guid, TabViewModel> tabViewModels = [];
	private string? operationError;
	private int isDisposed;
	private bool isRefreshing;
	private int activeTabIndex = -1;

	public RootViewModel(
		WindowModel window,
		IFilesDataRoot dataRoot,
		DispatcherQueue dispatcherQueue,
		CommandRegistry commandRegistry)
	{
		ArgumentNullException.ThrowIfNull(window);
		ArgumentNullException.ThrowIfNull(dataRoot);
		ArgumentNullException.ThrowIfNull(dispatcherQueue);
		ArgumentNullException.ThrowIfNull(commandRegistry);

		this.window = window;
		this.dataRoot = dataRoot;
		this.dispatcherQueue = dispatcherQueue;
		Tabs = [];
		commandManager = new WindowCommandManager(
			this,
			commandRegistry,
			dispatcherQueue);

		window.StateChanged += Window_StateChanged;
		RefreshFromCore();
	}

	public ObservableCollection<TabViewModel> Tabs { get; }

	public WindowCommandManager Commands => commandManager;

	public CommandBindingViewModel BackCommand =>
		commandManager.GetBinding(CommandIds.NavigateBack);

	public CommandBindingViewModel ForwardCommand =>
		commandManager.GetBinding(CommandIds.NavigateForward);

	public CommandBindingViewModel UpCommand =>
		commandManager.GetBinding(CommandIds.NavigateUp);

	public CommandBindingViewModel HomeCommand =>
		commandManager.GetBinding(CommandIds.NavigateHome);

	public CommandBindingViewModel NavigatePathCommand =>
		commandManager.GetBinding(CommandIds.NavigatePath);

	public CommandBindingViewModel RefreshCommand =>
		commandManager.GetBinding(CommandIds.Refresh);

	public CommandBindingViewModel NewTabCommand =>
		commandManager.GetBinding(CommandIds.NewTab);

	public CommandBindingViewModel CloseTabCommand =>
		commandManager.GetBinding(CommandIds.CloseTab);

	public CommandBindingViewModel NewPaneCommand =>
		commandManager.GetBinding(CommandIds.NewPane);

	public CommandBindingViewModel ClosePaneCommand =>
		commandManager.GetBinding(CommandIds.ClosePane);

	internal DispatcherQueue DispatcherQueue => dispatcherQueue;

	public TabViewModel? ActiveTab =>
		Tabs.FirstOrDefault(tab => tab.Id == window.ActiveTab?.Id);

	public FolderBrowserViewModel? ActiveFolderBrowser =>
		ActiveTab?.ActivePane?.FolderBrowser;

	public int ActiveTabIndex
	{
		get => activeTabIndex;
		private set => SetProperty(ref activeTabIndex, value);
	}

	public string StatusText =>
		operationError
		?? ActiveTab?.StatusText
		?? "No tabs";

	public async Task InitializeAsync()
	{
		EnsureActive();
		if (ActiveTab?.ActivePane is { } pane)
		{
			await pane.FolderBrowser.InitializeAsync().ConfigureAwait(false);
		}
	}

	public async Task OpenTabAsync(
		CancellationToken cancellationToken = default)
	{
		EnsureActive();
		await window.OpenTabAsync(
				HomeLocation.Instance,
				cancellationToken)
			.ConfigureAwait(false);
	}

	public async Task CloseTabAsync(
		Guid tabId,
		CancellationToken cancellationToken = default)
	{
		EnsureActive();
		if (Tabs.Count <= 1)
		{
			return;
		}

		await window.CloseTabAsync(tabId, cancellationToken)
			.ConfigureAwait(false);
	}

	public bool SetActiveTab(Guid tabId)
	{
		EnsureActive();
		return window.SetActiveTab(tabId);
	}

	public void SetActiveTabAt(int index)
	{
		if (index >= 0 && index < Tabs.Count)
		{
			SetActiveTab(Tabs[index].Id);
		}
	}

	public void ReportOperationError(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		operationError = exception.Message;
		OnPropertyChanged(nameof(StatusText));
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref isDisposed, 1) is not 0)
		{
			return;
		}

		window.StateChanged -= Window_StateChanged;
		commandManager.Dispose();
		foreach (var tab in tabViewModels.Values)
		{
			tab.PropertyChanged -= TabViewModel_PropertyChanged;
			tab.Dispose();
		}

		tabViewModels.Clear();
		Tabs.Clear();
	}

	private void Window_StateChanged(object? sender, EventArgs args)
	{
		if (!dispatcherQueue.TryEnqueue(RefreshFromCore))
		{
			if (Volatile.Read(ref isDisposed) is 0)
			{
				throw new InvalidOperationException(
					"The Files.App2 UI dispatcher rejected a window update.");
			}
		}
	}

	private void TabViewModel_PropertyChanged(
		object? sender,
		PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(TabViewModel.StatusText)
			or nameof(TabViewModel.ActivePane)
			or nameof(TabViewModel.Title)
			or nameof(TabViewModel.CanClosePane))
		{
			OnPropertyChanged(nameof(StatusText));
			OnPropertyChanged(nameof(ActiveFolderBrowser));
			commandManager.RefreshStates();
		}
	}

	private void RefreshFromCore()
	{
		if (Volatile.Read(ref isDisposed) is not 0 || isRefreshing)
		{
			return;
		}

		isRefreshing = true;
		try
		{
			var coreTabs = window.Tabs;
			var coreTabIds = coreTabs
				.Select(static tab => tab.Id)
				.ToHashSet();

			foreach (var removedId in tabViewModels.Keys
				.Where(id => !coreTabIds.Contains(id))
				.ToArray())
			{
				var removedTab = tabViewModels[removedId];
				removedTab.PropertyChanged -= TabViewModel_PropertyChanged;
				removedTab.Dispose();
				tabViewModels.Remove(removedId);
			}

			foreach (var coreTab in coreTabs)
			{
				if (!tabViewModels.ContainsKey(coreTab.Id))
				{
					var tabViewModel = new TabViewModel(
						coreTab,
						dataRoot,
						dispatcherQueue,
						commandManager);
					tabViewModel.PropertyChanged += TabViewModel_PropertyChanged;
					tabViewModels[coreTab.Id] = tabViewModel;
				}
			}

			var orderedTabs = coreTabs
				.Select(coreTab => tabViewModels[coreTab.Id])
				.ToArray();
			if (!Tabs.SequenceEqual(orderedTabs))
			{
				Tabs.Clear();
				foreach (var tab in orderedTabs)
				{
					Tabs.Add(tab);
				}
			}

			var activeTabId = window.ActiveTab?.Id;
			ActiveTabIndex = activeTabId is { } id
				? Tabs
					.Select((tab, index) => (tab, index))
					.FirstOrDefault(value => value.tab.Id == id)
					.index
				: -1;

			operationError = null;
			OnPropertyChanged(nameof(ActiveTab));
			OnPropertyChanged(nameof(ActiveFolderBrowser));
			OnPropertyChanged(nameof(StatusText));
			commandManager.RefreshStates();
		}
		finally
		{
			isRefreshing = false;
		}
	}

	private void EnsureActive() =>
		ObjectDisposedException.ThrowIf(
			Volatile.Read(ref isDisposed) is not 0,
			this);
}
