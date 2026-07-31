// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App2.Views;
using Files.App2.Commands;
using Files.Core.AppModels;
using Files.Core.Data;
using Microsoft.UI.Xaml;

namespace Files.App2;

public sealed partial class MainWindow : Window
{
	private readonly RootView rootView;

	public MainWindow(
		WindowModel coreWindow,
		IFilesDataRoot dataRoot,
		CommandRegistry commandRegistry)
	{
		ArgumentNullException.ThrowIfNull(coreWindow);
		ArgumentNullException.ThrowIfNull(dataRoot);
		ArgumentNullException.ThrowIfNull(commandRegistry);

		InitializeComponent();
		rootView = new RootView(
			coreWindow,
			dataRoot,
			DispatcherQueue,
			commandRegistry);
		RootFrame.Content = rootView;
		rootView.AttachWindow(this);
	}

	public void Dispose() => rootView.Dispose();
}
