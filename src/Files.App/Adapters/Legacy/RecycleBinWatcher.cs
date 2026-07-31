// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace Files.App.Adapters.Legacy
{
	/// <summary>
	/// Compatibility watcher for the legacy Recycle Bin surface.
	/// </summary>
	public sealed class RecycleBinWatcher : IDisposable
	{
		private readonly List<FileSystemWatcher> watchers = [];

		public event EventHandler<FileSystemEventArgs>? ItemAdded;

		public event EventHandler<FileSystemEventArgs>? ItemDeleted;

		public event EventHandler<FileSystemEventArgs>? RefreshRequested;

		public RecycleBinWatcher()
		{
			StartWatcher();
		}

		public void StartWatcher()
		{
			var sid = WindowsIdentity.GetCurrent().User?.ToString();
			if (string.IsNullOrEmpty(sid))
				return;

			foreach (var drive in DriveInfo.GetDrives())
			{
				var recyclePath = Path.Combine(drive.Name, "$RECYCLE.BIN", sid);
				if (drive.DriveType is System.IO.DriveType.Network || !Directory.Exists(recyclePath))
					continue;

				try
				{
					var watcher = new FileSystemWatcher(recyclePath, "*.*")
					{
						NotifyFilter = NotifyFilters.LastWrite
							| NotifyFilters.FileName
							| NotifyFilters.DirectoryName,
					};
					watcher.Created += Watcher_Changed;
					watcher.Deleted += Watcher_Changed;
					watcher.Error += Watcher_Error;
					watcher.EnableRaisingEvents = true;
					watchers.Add(watcher);
				}
				catch (Exception exception) when (
					exception is IOException or UnauthorizedAccessException)
				{
					App.Logger.LogDebug(exception, "Recycle Bin watcher could not monitor {Path}.", recyclePath);
				}
			}
		}

		public void Dispose()
		{
			foreach (var watcher in watchers)
			{
				watcher.Created -= Watcher_Changed;
				watcher.Deleted -= Watcher_Changed;
				watcher.Error -= Watcher_Error;
				watcher.Dispose();
			}

			watchers.Clear();
		}

		private void Watcher_Changed(object sender, FileSystemEventArgs args)
		{
			if (string.IsNullOrEmpty(args.Name)
				|| args.Name.StartsWith("$I", StringComparison.Ordinal))
			{
				return;
			}

			if (args.ChangeType is WatcherChangeTypes.Created)
				ItemAdded?.Invoke(this, args);
			else if (args.ChangeType is WatcherChangeTypes.Deleted)
				ItemDeleted?.Invoke(this, args);
			else
				RefreshRequested?.Invoke(this, args);
		}

		private void Watcher_Error(object sender, ErrorEventArgs args)
		{
			RefreshRequested?.Invoke(
				this,
				new FileSystemEventArgs(WatcherChangeTypes.All, string.Empty, string.Empty));
		}
	}
}
