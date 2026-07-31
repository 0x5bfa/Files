// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App2.Views;
using Files.Core.AppModels;
using Files.Core.Data;
using Microsoft.UI.Xaml;

namespace Files.App2;

public sealed partial class MainWindow : Window
{
	private readonly MainPage mainPage;

	public MainWindow(
		PaneModel pane,
		IFilesDataRoot dataRoot)
	{
		ArgumentNullException.ThrowIfNull(pane);
		ArgumentNullException.ThrowIfNull(dataRoot);

		InitializeComponent();
		mainPage = new MainPage(pane, dataRoot, DispatcherQueue);
		RootFrame.Content = mainPage;
	}

	public void Dispose() => mainPage.Dispose();
}
