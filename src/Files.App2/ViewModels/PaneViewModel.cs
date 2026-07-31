// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using Files.App2.Commands;
using Files.Core.AppModels;
using Files.Core.Data;
using Microsoft.UI.Dispatching;

namespace Files.App2.ViewModels;

public enum PaneContentKind
{
	FolderBrowser,
	Settings,
	Web,
}

public sealed class PaneViewModel : ObservableObject, IDisposable
{
	private readonly PaneModel pane;
	private bool isActive;
	private int isDisposed;
	private PaneContentKind contentKind = PaneContentKind.FolderBrowser;

	public PaneViewModel(
		PaneModel pane,
		IFilesDataRoot dataRoot,
		DispatcherQueue dispatcherQueue,
		WindowCommandManager commandManager)
	{
		ArgumentNullException.ThrowIfNull(pane);
		this.pane = pane;
		FolderBrowser = new FolderBrowserViewModel(
			pane,
			dataRoot,
			dispatcherQueue,
			commandManager);
		FolderBrowser.PropertyChanged += FolderBrowser_PropertyChanged;
	}

	public Guid Id => pane.Id;

	public FolderBrowserViewModel FolderBrowser { get; }

	public PaneContentKind ContentKind
	{
		get => contentKind;
		private set => SetProperty(ref contentKind, value);
	}

	public bool IsActive
	{
		get => isActive;
		private set => SetProperty(ref isActive, value);
	}

	public string Title => FolderBrowser.LocationText;

	public string StatusText => FolderBrowser.StatusText;

	public void SetActive(bool value)
	{
		if (IsActive != value)
		{
			IsActive = value;
			OnPropertyChanged(nameof(Title));
		}
	}

	public void SetContentKind(PaneContentKind kind)
	{
		if (!Enum.IsDefined(kind))
		{
			throw new ArgumentOutOfRangeException(nameof(kind));
		}

		ContentKind = kind;
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref isDisposed, 1) is not 0)
		{
			return;
		}

		FolderBrowser.PropertyChanged -= FolderBrowser_PropertyChanged;
		FolderBrowser.Dispose();
	}

	private void FolderBrowser_PropertyChanged(
		object? sender,
		PropertyChangedEventArgs e)
	{
		OnPropertyChanged(nameof(Title));
		OnPropertyChanged(nameof(StatusText));
	}
}
