// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.AppModels;
using Files.Core.Composition;
using Files.App2.Views;
using Microsoft.UI.Xaml;

namespace Files.App2;

public partial class App : Application
{
	private FilesCoreRuntime? runtime;
	private MainWindow? mainWindow;
	private int isClosing;

	public App()
	{
		InitializeComponent();
	}

	protected override async void OnLaunched(LaunchActivatedEventArgs args)
	{
		runtime = new FilesCoreBuilder()
			.AddWindowsStorage()
			.Build();

		var coreWindow = await runtime.Application
			.CreateWindowAsync()
			.ConfigureAwait(true);
		var pane = coreWindow.ActiveTab?.ActivePane
			?? throw new InvalidOperationException(
				"Files.Core did not create an active pane.");

		mainWindow = new MainWindow(pane, runtime.DataRoot);
		mainWindow.Closed += MainWindow_Closed;
		mainWindow.Activate();
	}

	private async void MainWindow_Closed(object sender, WindowEventArgs args)
	{
		if (Interlocked.Exchange(ref isClosing, 1) is not 0)
		{
			return;
		}

		mainWindow?.Dispose();
		mainWindow = null;

		if (Interlocked.Exchange(ref runtime, null) is { } currentRuntime)
		{
			await currentRuntime.DisposeAsync();
		}
	}
}
