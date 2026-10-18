// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;
using Files.App.Helpers.ContextFlyouts;
using Files.App.Utils.Shell.ContextMenus;
using Files.Shared.Helpers;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;
using System.Text;
using Windows.System;
using Windows.UI.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Files.App.Helpers
{
	public static class ShellContextFlyoutFactory
	{
		public static IUserSettingsService UserSettingsService { get; } = Ioc.Default.GetRequiredService<IUserSettingsService>();

		public static bool ShowClassicContextMenu(IReadOnlyList<string> paths, bool isLocationBackground, System.Drawing.Point invocationPoint)
			=> FilesShellClassicContextMenuSession.Show(paths, isLocationBackground, (HWND)MainWindow.Instance.WindowHandle, invocationPoint);

		private static (string Label, string AccessKey) ExtractLabelAndAccessKey(string rawLabel)
		{
			if (string.IsNullOrEmpty(rawLabel))
				return (string.Empty, string.Empty);

			string accessKey = string.Empty;
			var labelBuilder = new StringBuilder(rawLabel.Length);

			for (int i = 0; i < rawLabel.Length; i++)
			{
				char current = rawLabel[i];
				if (current != '&')
				{
					labelBuilder.Append(current);
					continue;
				}

				if (i + 1 >= rawLabel.Length)
				{
					labelBuilder.Append('&');
					continue;
				}

				char next = rawLabel[++i];
				if (next == '&')
				{
					labelBuilder.Append('&');
					continue;
				}

				if (string.IsNullOrEmpty(accessKey) && !char.IsWhiteSpace(next))
					accessKey = char.ToUpperInvariant(next).ToString();

				labelBuilder.Append(next);
			}

			return (labelBuilder.ToString(), accessKey);
		}

		private static ContextMenuFlyoutItemViewModel CreateShellMenuItem(Win32ContextMenuItem menuFlyoutItem, BitmapImage? image)
		{
			var (label, accessKey) = ExtractLabelAndAccessKey(menuFlyoutItem.Label ?? string.Empty);
			return new ContextMenuFlyoutItemViewModel
			{
				Text = label,
				AccessKey = accessKey,
				Tag = menuFlyoutItem,
				BitmapIcon = image,
			};
		}

		private static async Task<List<ContextMenuFlyoutItemViewModel>> GetPreservedShellMenuItemsAsync(IReadOnlyList<string> paths, FastContextFlyout flyout,
			bool showOpenWith, bool showSendTo, bool showBitLocker, bool shiftPressed, CancellationToken cancellationToken)
		{
			var menuItemsList = new List<ContextMenuFlyoutItemViewModel>();
			var contextMenu = await ContextMenu.GetContextMenuForFiles(paths.ToArray(), shiftPressed ? PInvoke.CMF_EXTENDEDVERBS : PInvoke.CMF_NORMAL);
			if (contextMenu is null)
				return menuItemsList;

			var retained = false;
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				var sendToLabel = showSendTo ? Win32Helper.ExtractStringFromDLL("shell32.dll", 30312) : null;
				var preserved = contextMenu.Items!.Where(item =>
					showOpenWith && string.Equals(item.CommandString, "openas", StringComparison.OrdinalIgnoreCase)
					|| showSendTo && (string.Equals(item.CommandString, "sendto", StringComparison.OrdinalIgnoreCase) || item.Label == sendToLabel)
					|| showBitLocker && (item.CommandString?.StartsWith("encrypt-bde", StringComparison.OrdinalIgnoreCase) is true || item.CommandString == "manage-bde"))
					.ToArray();
				await LoadMenuFlyoutItemAsync(menuItemsList, contextMenu, preserved, cancellationToken);
				foreach (var item in menuItemsList)
				{
					if (item.LoadSubMenuAction is { } loadSubMenu)
					{
						await loadSubMenu();
						item.LoadSubMenuAction = null;
					}
				}

				cancellationToken.ThrowIfCancellationRequested();
				if (menuItemsList.Count is not 0)
				{
					flyout.AttachPreservedContextMenu(contextMenu);
					retained = true;
				}
			}
			finally
			{
				if (!retained)
					contextMenu.Dispose();
			}

			return menuItemsList;
		}

		private static async Task LoadMenuFlyoutItemAsync(
			List<ContextMenuFlyoutItemViewModel> menuItemsListLocal,
			ContextMenu contextMenu,
			IEnumerable<Win32ContextMenuItem> menuFlyoutItems,
			CancellationToken cancellationToken,
			bool showIcons = true)
		{
			if (cancellationToken.IsCancellationRequested)
				return;

			foreach (var menuFlyoutItem in menuFlyoutItems
				.SkipWhile(x => x.Type == MENU_ITEM_TYPE.MFT_SEPARATOR) // Remove leading separators
				.Reverse()
				.SkipWhile(x => x.Type == MENU_ITEM_TYPE.MFT_SEPARATOR)) // Remove trailing separators
			{
				if (cancellationToken.IsCancellationRequested)
					break;

				// Avoid duplicate separators
				if ((menuFlyoutItem.Type == MENU_ITEM_TYPE.MFT_SEPARATOR) && (menuItemsListLocal.FirstOrDefault()?.ItemType == ContextMenuFlyoutItemType.Separator))
					continue;

				BitmapImage? image = null;
				if (showIcons && menuFlyoutItem.Icon is { Length: > 0 } iconData)
				{
					image = new BitmapImage();
					_ = LoadIconAsync(image, iconData);
				}

				if (menuFlyoutItem.Type is MENU_ITEM_TYPE.MFT_SEPARATOR)
				{
					var menuLayoutItem = new ContextMenuFlyoutItemViewModel()
					{
						ItemType = ContextMenuFlyoutItemType.Separator,
						Tag = menuFlyoutItem
					};
					menuItemsListLocal.Insert(0, menuLayoutItem);
				}
				else if (!string.IsNullOrEmpty(menuFlyoutItem.Label) && menuFlyoutItem.SubItems is not null)
				{
					if (string.Equals(menuFlyoutItem.Label, Win32Helper.ExtractStringFromDLL("shell32.dll", 30312)))
						menuFlyoutItem.CommandString = "sendto";

					var menuLayoutSubItem = CreateShellMenuItem(menuFlyoutItem, image);
					menuLayoutSubItem.Items = [];

					if (menuFlyoutItem.SubItems.Any())
					{
						await LoadMenuFlyoutItemAsync(menuLayoutSubItem.Items, contextMenu, menuFlyoutItem.SubItems, cancellationToken, showIcons);
					}
					else
					{
						menuLayoutSubItem.LoadSubMenuAction = async () =>
						{
							if (await contextMenu.LoadSubMenu(menuFlyoutItem.SubItems))
								await LoadMenuFlyoutItemAsync(menuLayoutSubItem.Items, contextMenu, menuFlyoutItem.SubItems, cancellationToken, showIcons);
						};
					}

					menuItemsListLocal.Insert(0, menuLayoutSubItem);
				}
				else if (!string.IsNullOrEmpty(menuFlyoutItem.Label))
				{
					var menuLayoutItem = CreateShellMenuItem(menuFlyoutItem, image);
					menuLayoutItem.Command = new AsyncRelayCommand<object>(x => InvokeShellMenuItemAsync(contextMenu, x));
					menuLayoutItem.CommandParameter = menuFlyoutItem;
					menuItemsListLocal.Insert(0, menuLayoutItem);
				}
			}

			async Task InvokeShellMenuItemAsync(ContextMenu contextMenu, object? tag)
			{
				if (tag is not Win32ContextMenuItem menuItem)
					return;

				var menuId = menuItem.ID;
				var isFont = FileExtensionHelpers.IsFontFile(contextMenu.ItemsPath[0]);
				var verb = menuItem.CommandString;
				switch (verb)
				{
					case "install" when isFont:
						await Win32Helper.InstallFontsAsync([.. contextMenu.ItemsPath], false);
						break;

					case "installAllUsers" when isFont:
						await Win32Helper.InstallFontsAsync([.. contextMenu.ItemsPath], true);
						break;

					case "mount":
						var vhdPath = contextMenu.ItemsPath[0];
						await Win32Helper.MountVhdDisk(vhdPath);
						break;

					case "format":
						var drivePath = contextMenu.ItemsPath[0];
						await Win32Helper.OpenFormatDriveDialog(drivePath);
						break;

					case "Windows.PowerShell.Run":
						await contextMenu.InvokeItem(
							menuId,
							contextMenu.ItemsPath[0].EndsWith(".ps1") ? Path.GetDirectoryName(contextMenu.ItemsPath[0]) : null
						);
						break;

					default:
						await contextMenu.InvokeItem(menuId);
						break;
				}

				//contextMenu.Dispose(); // Prevents some menu items from working (TBC)
			}
		}

		private static async Task LoadIconAsync(BitmapImage image, byte[] iconData)
		{
			try
			{
				using var ms = new MemoryStream(iconData, writable: false);
				await image.SetSourceAsync(ms.AsRandomAccessStream());
			}
			catch (Exception exception)
			{
				Debug.WriteLine(exception);
			}
		}

		public static List<ContextMenuFlyoutItemViewModel>? GetOpenWithItems(List<ContextMenuFlyoutItemViewModel> flyout)
		{
			var item = flyout.FirstOrDefault(x => x.Tag is Win32ContextMenuItem { CommandString: "openas" });
			if (item is not null)
				flyout.Remove(item);

			return item?.Items;
		}

		public static List<ContextMenuFlyoutItemViewModel>? GetSendToItems(List<ContextMenuFlyoutItemViewModel> flyout)
		{
			var item = flyout.FirstOrDefault(x => x.Tag is Win32ContextMenuItem { CommandString: "sendto" });
			if (item is not null)
				flyout.Remove(item);

			return item?.Items;
		}

		/// <summary>
		/// Loads Explorer app-extension and Cloud Files commands, plus the Shell commands retained by
		/// Files-owned Open with / Send to / BitLocker menu entries.
		/// </summary>
		public static async Task LoadShellMenuItemsAsync(
			IReadOnlyList<string> paths,
			FastContextFlyout flyout,
			ContextMenuOptions? options = null,
			MenuFlyoutItem? showMoreOptions = null,
			bool showOpenWithMenu = false,
			bool showSendToMenu = false,
			(MenuFlyoutItemBase Leaf, MenuFlyoutSubItem SubMenu)? openWithSwap = null,
			(MenuFlyoutItemBase Leaf, MenuFlyoutSubItem SubMenu)? sendToSwap = null,
			bool isLocationBackground = false,
			CancellationToken cancellationToken = default)
		{
			using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, flyout.MenuLifetimeToken);
			var loadToken = linkedCancellationTokenSource.Token;
			try
			{
				if (paths.Count is 0 || options is not null && !options.IsLocationItem)
					return;

				var shiftPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
				loadToken.ThrowIfCancellationRequested();
				var session = await FilesShellContextMenuSession.CreateAsync(paths, shiftPressed, isLocationBackground, loadToken);
				if (loadToken.IsCancellationRequested)
				{
					if (session is not null)
						await session.DisposeAsync();
					return;
				}

				if (session is not null)
				{
					flyout.AttachShellContextMenuSession(session);
					flyout.AddShellModels(session.CreateMenuItems(), showMoreOptions);
				}

				var sendToEnabled = showSendToMenu && UserSettingsService.GeneralSettingsService.ShowSendToMenu;
				var showBitLocker = !isLocationBackground && paths.Count is 1 && IsDriveRoot(paths[0]);
				if (!showOpenWithMenu && !sendToEnabled && !showBitLocker)
				{
					flyout.FinalizePrimaryRowPosition();
					return;
				}

				var shellMenuItems = await GetPreservedShellMenuItemsAsync(
					paths, flyout, showOpenWithMenu, sendToEnabled, showBitLocker, shiftPressed, loadToken);
				if (loadToken.IsCancellationRequested)
					return;

				var openWithItem = showOpenWithMenu ? shellMenuItems.FirstOrDefault(x => x.Tag is Win32ContextMenuItem { CommandString: "openas" }) : null;
				if (openWithItem is not null)
					shellMenuItems.Remove(openWithItem);

				var sendToItem = sendToEnabled ? shellMenuItems.FirstOrDefault(x => x.Tag is Win32ContextMenuItem { CommandString: "sendto" }) : null;
				if (sendToItem is not null)
					shellMenuItems.Remove(sendToItem);

				flyout.ApplyBitLockerModels(shellMenuItems, showMoreOptions);

				// Open with / Send to: the placeholders were converted to their submenu form before the menu was
				// shown (stable heights); fill their contents now, or drop them if the shell has no such entries.
				void FillOrRemove(MenuFlyoutSubItem? subMenu, ContextMenuFlyoutItemViewModel? item, Func<List<ContextMenuFlyoutItemViewModel>, List<ContextMenuFlyoutItemViewModel>?> getter)
				{
					if (subMenu is null)
						return;

					if (item is not null)
						FastContextFlyout.PopulateShellSubMenu(subMenu, item, getter, () => flyout.Items.Remove(subMenu));
					else
						flyout.Items.Remove(subMenu);
				}

				if (openWithSwap is { } openWithTarget)
					FastContextFlyout.FillOrRevert(openWithTarget, openWithItem, GetOpenWithItems);
				else if (showOpenWithMenu)
					FillOrRemove(flyout.ConvertPlaceholderToSubMenu("OpenWithPlaceholder", Strings.OpenWith.GetLocalizedResource(), "App.ThemedIcons.OpenWith"), openWithItem, GetOpenWithItems);
				if (sendToSwap is { } sendToTarget)
					FastContextFlyout.FillOrRevert(sendToTarget, sendToItem, GetSendToItems);
				else if (showSendToMenu)
					FillOrRemove(flyout.ConvertPlaceholderToSubMenu("SendToPlaceholder", Strings.SendTo.GetLocalizedResource(), null), sendToItem, GetSendToItems);

				flyout.FinalizePrimaryRowPosition();
			}
			catch (OperationCanceledException) when (loadToken.IsCancellationRequested)
			{
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}
		}

		private static bool IsDriveRoot(string path)
		{
			var root = Path.GetPathRoot(path);
			return !string.IsNullOrEmpty(root) && string.Equals(path.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
		}
	}
}
