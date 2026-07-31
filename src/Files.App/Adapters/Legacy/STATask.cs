// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage.Windows;
using Microsoft.Extensions.Logging;
using Windows.Win32;

namespace Files.App.Adapters.Legacy
{
	/// <summary>
	/// Preserves the STA boundary for Shell callbacks that have not moved to Core operations yet.
	/// </summary>
	internal static class STATask
	{
		public static async Task Run(Action action, ILogger? logger = null)
		{
			ArgumentNullException.ThrowIfNull(action);
			await GetScheduler().InvokeOperationAsync(() =>
			{
				action();
				return true;
			}).ConfigureAwait(false);
		}

		public static Task<T> Run<T>(Func<T> action, ILogger? logger = null)
		{
			ArgumentNullException.ThrowIfNull(action);
			return GetScheduler().InvokeOperationAsync(action);
		}

		public static async Task Run(Func<Task> action, ILogger? logger = null)
		{
			ArgumentNullException.ThrowIfNull(action);
			await RunOnMessagePumpedStaAsync(async () =>
			{
				await action();
				return true;
			}).ConfigureAwait(false);
		}

		public static async Task<T?> Run<T>(Func<Task<T>> action, ILogger? logger = null)
		{
			ArgumentNullException.ThrowIfNull(action);
			return await RunOnMessagePumpedStaAsync(action).ConfigureAwait(false);
		}

		private static Task<T> RunOnMessagePumpedStaAsync<T>(Func<Task<T>> action)
		{
			var completion = new TaskCompletionSource<T>(
				TaskCreationOptions.RunContinuationsAsynchronously);
			var thread = new Thread(() =>
			{
				PInvoke.OleInitialize();
				try
				{
					using var synchronizationContext =
						new System.Windows.Forms.WindowsFormsSynchronizationContext();
					SynchronizationContext.SetSynchronizationContext(synchronizationContext);
					using var applicationContext =
						new System.Windows.Forms.ApplicationContext();
					var task = action();
					if (!task.IsCompleted)
					{
						task.GetAwaiter().OnCompleted(applicationContext.ExitThread);
						System.Windows.Forms.Application.Run(applicationContext);
					}

					completion.TrySetResult(task.GetAwaiter().GetResult());
				}
				catch (Exception exception)
				{
					completion.TrySetException(exception);
				}
				finally
				{
					SynchronizationContext.SetSynchronizationContext(null);
					PInvoke.OleUninitialize();
				}
			})
			{
				IsBackground = true,
				Name = "Files legacy Shell STA",
			};
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			return completion.Task;
		}

		private static IWindowsShellScheduler GetScheduler()
		{
			return App.CoreHost.Runtime.DataRoot.Sources
				.OfType<WindowsStorageSource>()
				.Single()
				.Scheduler;
		}
	}
}
