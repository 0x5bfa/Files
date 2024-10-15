// Copyright (c) 2024 Files Community
// Licensed under the MIT License. See the LICENSE.

using Files.App.Utils.Shell;
using Files.App.UserControls.Widgets;

namespace Files.App.Services
{
	internal sealed class QuickAccessService : IQuickAccessService
	{
		private FileSystemWatcher _watcher;

		private readonly List<INavigationControlItem> _PinnedFolders = [];
		/// <inheritdoc/>
		public IReadOnlyList<INavigationControlItem> PinnedFolders
		{
			get
			{
				lock (_PinnedFolders)
					return _PinnedFolders.ToList().AsReadOnly();
			}
		}

		/// <inheritdoc/>
		public event EventHandler<NotifyCollectionChangedEventArgs>? PinnedFoldersChanged;

  		/// <inheritdoc/>
		public async Task InitializeAsync()
		{
			_watcher = new()
			{
				Path = SystemIO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Recent), "AutomaticDestinations"),
				Filter = "f01b4d95cf55d32a.automaticDestinations-ms",
				NotifyFilter = SystemIO.NotifyFilters.DirectoryName | SystemIO.NotifyFilters.FileName | SystemIO.NotifyFilters.LastWrite,
			};

			_watcher.Changed += Watcher_Changed;
			_watcher.Deleted += Watcher_Changed;
			_watcher.EnableRaisingEvents = true;
		}

		/// <inheritdoc/>
		public async Task<bool> UpdatePinnedFoldersAsync()
		{
			return await Task.Run(() =>
			{
				return UpdatePinnedFolders();
			});
		}

		public async Task<bool> PinFolder(string folder)
		{
			// TODO: Invoke "pintohome" verb

			return true;
		}

		public async Task<bool> UnpinFolder(INavigationControlItem folder)
		{
			// TODO: Invoke "unpinfromhome" verb

			return true;
		}

		private unsafe bool UpdatePinnedFolders()
		{
			try
			{
				HRESULT hr = default;

				// Get IShellItem of the shell folder
				var shellItemIid = typeof(IShellItem).GUID;
				using ComPtr<IShellItem> pFolderShellItem = default;
				fixed (char* pszFolderShellPath = "Shell:::{3936e9e4-d92c-4eee-a85a-bc16d5ea0819}")
					hr = PInvoke.SHCreateItemFromParsingName(pszFolderShellPath, null, &shellItemIid, (void**)pFolderShellItem.GetAddressOf());

				// Get IEnumShellItems of the quick access shell folder
				var enumItemsBHID = PInvoke.BHID_EnumItems;
				Guid enumShellItemIid = typeof(IEnumShellItems).GUID;
				using ComPtr<IEnumShellItems> pEnumShellItems = default;
				hr = pFolderShellItem.Get()->BindToHandler(null, &enumItemsBHID, &enumShellItemIid, (void**)pEnumShellItems.GetAddressOf());

				// Enumerate recent items and populate the list
				int index = 0;
				List<INavigationControlItem> pinnedItems = [];
				ComPtr<IShellItem> pShellItem = default; // Do not dispose in this method to use later to prepare for its deletion
				while (pEnumShellItems.Get()->Next(1, pShellItem.GetAddressOf()) == HRESULT.S_OK)
				{
					// Get top 20 items
					if (index is 20)
						break;

					// TODO: Add code here

					index++;
				}

				if (pinnedItems.Count is 0)
					return false;

				var snapshot = PinnedFolders;

				lock (_RecentFolders)
				{
					_PinnedFolders.Clear();
					_PinnedFolders.AddRange(pinnedItems);
				}

				var eventArgs = GetChangedActionEventArgs(snapshot, pinnedItems);

 				PinnedFoldersChanged?.Invoke(this, eventArgs);

				return true;
			}
			catch
			{
				return false;
			}
		}

		private void Watcher_Changed(object sender, SystemIO.FileSystemEventArgs e)
		{
			_ = UpdatePinnedFoldersAsync();
		}
	}
}
