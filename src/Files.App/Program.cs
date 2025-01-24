// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Shared.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.IO;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using static Files.App.Helpers.Win32PInvoke;

namespace Files.App
{
	/// <summary>
	/// Represents the base entry point of the Files app.
	/// </summary>
	/// <remarks>
	/// Gets called at the first time when the app launched or activated.
	/// </remarks>
	internal sealed class Program
	{
		private static Program()
		{
			AppLifecycleHelper.EarlyRedirectToExistingProcess();
		}

		/// <summary>
		/// Initializes the process; the entry point of the process.
		/// </summary>
		[STAThread]
		private static void Main()
		{
			WinRT.ComWrappersSupport.InitializeComWrappers();

			// Call the first WinRT server for the first time (Has been commented out as our OOP WinRT server seems not to support elevation yet)
			//AppLifecycleHelper.KillWinRTServerIfAny();
			//Server.AppInstanceMonitor.StartMonitor(Environment.ProcessId);

			AppLifecycleHelper.RedirectActivationToExistingProcess();

			if (AppInstance.FindOrRegisterForKey((-Environment.ProcessId).ToString()) is { } currentInstance &&
				currentInstance.IsCurrent)
				currentInstance.Activated += OnActivated;

			ApplicationData.Current.LocalSettings.Values["INSTANCE_ACTIVE"] = -Environment.ProcessId;

			Application.Start((p) =>
			{
				var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
				SynchronizationContext.SetSynchronizationContext(context);

				_ = new App();
			});
		}

		/// <summary>
		/// Gets invoked when the application is activated.
		/// </summary>
		private static async void OnActivated(object? sender, AppActivationArguments args)
		{
			// WINUI3: Verify if needed or OnLaunched is called
			if (App.Current is App thisApp)
				await thisApp.OnActivatedAsync(args);
		}
	}
}
