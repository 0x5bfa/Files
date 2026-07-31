// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;
using Windows.Devices.Portable;
using Windows.Storage;

namespace Files.App.Utils
{
	public sealed class WindowsStorageDeviceWatcher : IStorageDeviceWatcher
	{
		public event EventHandler<IFolder> DeviceAdded;
		public event EventHandler<string> DeviceRemoved;
		public event EventHandler EnumerationCompleted;
		public event EventHandler<string> DeviceModified;

		private DeviceWatcher watcher;
		private readonly object driveMonitorLock = new();
		private HashSet<string> knownDriveIds = new(StringComparer.OrdinalIgnoreCase);
		private CancellationTokenSource? driveMonitorCancellation;
		private Task? driveMonitorTask;

		public bool CanBeStarted => watcher.Status is DeviceWatcherStatus.Created or DeviceWatcherStatus.Stopped or DeviceWatcherStatus.Aborted;

		public WindowsStorageDeviceWatcher()
		{
			watcher = DeviceInformation.CreateWatcher(StorageDevice.GetDeviceSelector());
			watcher.Added += Watcher_Added;
			watcher.Removed += Watcher_Removed;
			watcher.EnumerationCompleted += Watcher_EnumerationCompleted;

		}

		private async Task AddDriveAsync(string driveId)
		{
			var driveAdded = new DriveInfo(driveId);
			if (!driveAdded.IsReady && !IsUnauthorizedDrive(driveAdded))
				return;

			var rootAdded = await FilesystemTasks.Wrap(
				() => StorageFolder.GetFolderFromPathAsync(driveAdded.RootDirectory.FullName).AsTask());
			if (!rootAdded)
			{
				App.Logger.LogWarning($"{rootAdded.ErrorCode}: Attempting to add the device, {driveId},"
					+ " failed at the StorageFolder initialization step. This device will be ignored.");
				return;
			}

			var type = DriveHelpers.GetDriveType(driveAdded);
			var label = DriveHelpers.GetExtendedDriveLabel(driveAdded);
			DriveItem driveItem = await DriveItem.CreateFromPropertiesAsync(rootAdded, driveId, label, type);

			DeviceAdded?.Invoke(this, driveItem);
		}

		private void Watcher_EnumerationCompleted(DeviceWatcher sender, object args)
		{
			EnumerationCompleted?.Invoke(this, EventArgs.Empty);
		}

		private void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
		{
			DeviceRemoved?.Invoke(this, args.Id);
		}

		private async void Watcher_Added(DeviceWatcher sender, DeviceInformation args)
		{
			string deviceId = args.Id;
			StorageFolder root;
			try
			{
				root = StorageDevice.FromId(deviceId);
			}
			catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException or COMException)
			{
				App.Logger.LogWarning($"{ex.GetType()}: Attempting to add the device, {args.Name},"
					+ $" failed at the StorageFolder initialization step. This device will be ignored. Device ID: {deviceId}");
				return;
			}

			Data.Items.DriveType type;
			string label;
			try
			{
				// Check if this drive is associated with a drive letter
				var driveAdded = new DriveInfo(root.Path);
				if (!driveAdded.IsReady && !IsUnauthorizedDrive(driveAdded))
					return;

				type = DriveHelpers.GetDriveType(driveAdded);
				label = DriveHelpers.GetExtendedDriveLabel(driveAdded);
			}
			catch (ArgumentException)
			{
				type = Data.Items.DriveType.Removable;
				label = string.Empty;
			}

			var driveItem = await DriveItem.CreateFromPropertiesAsync(root, deviceId, label, type);

			DeviceAdded?.Invoke(this, driveItem);
		}

		public void Start()
		{
			lock (driveMonitorLock)
			{
				if (driveMonitorTask is null)
				{
					knownDriveIds = GetDriveIds();
					driveMonitorCancellation = new CancellationTokenSource();
					driveMonitorTask = MonitorDrivesAsync(driveMonitorCancellation.Token);
				}
			}

			watcher.Start();
		}

		public void Stop()
		{
			if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
			{
				watcher.Stop();
			}

			watcher.Added -= Watcher_Added;
			watcher.Removed -= Watcher_Removed;
			watcher.EnumerationCompleted -= Watcher_EnumerationCompleted;

			lock (driveMonitorLock)
			{
				driveMonitorCancellation?.Cancel();
				driveMonitorCancellation?.Dispose();
				driveMonitorCancellation = null;
				driveMonitorTask = null;
			}
		}

		private async Task MonitorDrivesAsync(CancellationToken cancellationToken)
		{
			try
			{
				using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
				while (await timer.WaitForNextTickAsync(cancellationToken))
				{
					var currentDriveIds = GetDriveIds();
					string[] added;
					string[] removed;
					lock (driveMonitorLock)
					{
						added = currentDriveIds.Except(knownDriveIds).ToArray();
						removed = knownDriveIds.Except(currentDriveIds).ToArray();
						knownDriveIds = currentDriveIds;
					}

					foreach (var driveId in removed)
						DeviceRemoved?.Invoke(this, driveId);

					foreach (var driveId in added)
					{
						await AddDriveAsync(driveId);
						DeviceModified?.Invoke(this, driveId);
					}
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
			catch (Exception exception)
			{
				App.Logger.LogWarning(exception, "Drive monitoring stopped unexpectedly.");
			}
		}

		private static HashSet<string> GetDriveIds()
		{
			return DriveInfo.GetDrives()
				.Select(drive => drive.Name.TrimEnd(Path.DirectorySeparatorChar))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
		}

		private bool IsUnauthorizedDrive(DriveInfo driveInfo)
		{
			try
			{
				_ = Directory.EnumerateFileSystemEntries(driveInfo.Name).FirstOrDefault();
				return false;
			}
			catch (UnauthorizedAccessException)
			{
				// probably BitLocker locked drive.
				return true;
			}
			catch (IOException ex) when (ex.HResult == unchecked((int)0x80310000)) // FVE_E_LOCKED_VOLUME
			{
				// BitLocker locked drive.
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
