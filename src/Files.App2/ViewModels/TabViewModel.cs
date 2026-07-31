// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using Files.App2.Commands;
using Files.Core.AppModels;
using Files.Core.Data;
using Microsoft.UI.Dispatching;

namespace Files.App2.ViewModels;

public sealed class TabViewModel : ObservableObject, IDisposable
{
	private readonly TabModel tab;
	private readonly IFilesDataRoot dataRoot;
	private readonly DispatcherQueue dispatcherQueue;
	private readonly WindowCommandManager commandManager;
	private readonly Dictionary<Guid, PaneViewModel> paneViewModels = [];
	private int isDisposed;
	private string? operationError;
	private bool isRefreshing;

	public TabViewModel(
		TabModel tab,
		IFilesDataRoot dataRoot,
		DispatcherQueue dispatcherQueue,
		WindowCommandManager commandManager)
	{
		ArgumentNullException.ThrowIfNull(tab);
		ArgumentNullException.ThrowIfNull(dataRoot);
		ArgumentNullException.ThrowIfNull(dispatcherQueue);
		ArgumentNullException.ThrowIfNull(commandManager);

		this.tab = tab;
		this.dataRoot = dataRoot;
		this.dispatcherQueue = dispatcherQueue;
		this.commandManager = commandManager;
		Panes = [];

		tab.StateChanged += Tab_StateChanged;
		RefreshFromCore();
	}

	public Guid Id => tab.Id;

	public ObservableCollection<PaneViewModel> Panes { get; }

	public PaneViewModel? ActivePane =>
		Panes.FirstOrDefault(static pane => pane.IsActive);

	public PaneSplitOrientation SplitOrientation => tab.SplitOrientation;

	public string Title => ActivePane?.Title ?? "New tab";

	public string StatusText =>
		operationError
		?? ActivePane?.FolderBrowser.StatusText
		?? "No pane";

	public bool CanClosePane => Panes.Count > 1;

	public async Task OpenPaneAsync(
		PaneSplitOrientation orientation,
		CancellationToken cancellationToken = default)
	{
		EnsureActive();
		await tab.OpenSplitAsync(
				orientation,
				cancellationToken: cancellationToken)
			.ConfigureAwait(false);
	}

	public async Task CloseActivePaneAsync(
		CancellationToken cancellationToken = default)
	{
		EnsureActive();
		if (ActivePane is not { } activePane)
		{
			return;
		}

		await tab.ClosePaneAsync(activePane.Id, cancellationToken)
			.ConfigureAwait(false);
	}

	public bool SetActivePane(Guid paneId)
	{
		EnsureActive();
		return tab.SetActivePane(paneId);
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

		tab.StateChanged -= Tab_StateChanged;
		foreach (var pane in paneViewModels.Values)
		{
			pane.PropertyChanged -= PaneViewModel_PropertyChanged;
			pane.Dispose();
		}

		paneViewModels.Clear();
		Panes.Clear();
	}

	private void Tab_StateChanged(object? sender, EventArgs args)
	{
		if (!dispatcherQueue.TryEnqueue(RefreshFromCore))
		{
			if (Volatile.Read(ref isDisposed) is 0)
			{
				throw new InvalidOperationException(
					"The Files.App2 UI dispatcher rejected a tab update.");
			}
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
			var corePanes = tab.Panes;
			var corePaneIds = corePanes
				.Select(static pane => pane.Id)
				.ToHashSet();

			foreach (var removedId in paneViewModels.Keys
				.Where(id => !corePaneIds.Contains(id))
				.ToArray())
			{
				var removedPane = paneViewModels[removedId];
				removedPane.PropertyChanged -= PaneViewModel_PropertyChanged;
				removedPane.Dispose();
				paneViewModels.Remove(removedId);
			}

			foreach (var corePane in corePanes)
			{
				if (!paneViewModels.ContainsKey(corePane.Id))
				{
					var paneViewModel = new PaneViewModel(
						corePane,
						dataRoot,
						dispatcherQueue,
						commandManager);
					paneViewModel.PropertyChanged += PaneViewModel_PropertyChanged;
					paneViewModels[corePane.Id] = paneViewModel;
				}
			}

			Panes.Clear();
			foreach (var corePane in corePanes)
			{
				var pane = paneViewModels[corePane.Id];
				pane.SetActive(
					tab.ActivePane?.Id == corePane.Id);
				Panes.Add(pane);
			}

			operationError = null;
			OnPropertyChanged(nameof(ActivePane));
			OnPropertyChanged(nameof(SplitOrientation));
			OnPropertyChanged(nameof(Title));
			OnPropertyChanged(nameof(StatusText));
			OnPropertyChanged(nameof(CanClosePane));
		}
		finally
		{
			isRefreshing = false;
		}
	}

	private void PaneViewModel_PropertyChanged(
		object? sender,
		PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(PaneViewModel.StatusText)
			or nameof(PaneViewModel.Title))
		{
			OnPropertyChanged(nameof(Title));
			OnPropertyChanged(nameof(StatusText));
		}
	}

	private void EnsureActive() =>
		ObjectDisposedException.ThrowIf(
			Volatile.Read(ref isDisposed) is not 0,
			this);
}
