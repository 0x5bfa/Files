// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App2.Views;
using Files.App2.Commands;
using Files.Core.Composition;
using Microsoft.UI.Xaml;

namespace Files.App2;

public partial class App : Application
{
	private FilesCoreRuntime? runtime;
	private MainWindow? mainWindow;
	private readonly CommandRegistry commandRegistry =
		App2CommandRegistration.Build();
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
		if (coreWindow.ActiveTab?.ActivePane is null)
		{
			throw new InvalidOperationException(
				"Files.Core did not create an active pane.");
		}

		mainWindow = new MainWindow(
			coreWindow,
			runtime.DataRoot,
			commandRegistry);
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
