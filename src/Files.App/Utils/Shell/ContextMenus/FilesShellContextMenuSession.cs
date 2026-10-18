// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using CommunityToolkit.Mvvm.Input;
using Files.App.Data.Models;
using Files.App.Utils.Shell;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Com;
using Windows.Win32.System.Ole;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.UI.ViewManagement;

namespace Files.App.Utils.Shell.ContextMenus;

/// <summary>Owns the app-extension and Cloud Files commands for one file selection on a Shell STA.</summary>
internal sealed class FilesShellContextMenuSession : IAsyncDisposable
{
	private const uint FirstCommandId = 1;
	private const uint LastCommandId = 0x7FFF;
	private const uint ItemMenuFlag = 0x00000080;
	private const uint ExtendedVerbsFlag = 0x00000100;
	private const uint CanonicalVerbUnicode = 0x00000004;
	private const uint UnicodeInvokeFlag = 0x00004000;
	private const uint InvokePointFlag = 0x20000000;
	private const int CanonicalVerbBufferLength = 128;

	private readonly object _lifetimeLock = new();
	private readonly ContextMenuWorkerPool.Worker _worker;
	private readonly string[] _paths;
	private readonly HWND _owner;
	private readonly System.Drawing.Point _invocationPoint;
	private readonly bool _locationBackground;
	private readonly Dictionary<FilesShellMenuEntry, uint> _nativeCommands = [];
	private readonly Dictionary<FilesShellMenuEntry, IExplorerCommand> _explorerCommands = [];
	private readonly Dictionary<FilesShellMenuEntry, Task<byte[]?>> _iconLoads = [];
	private readonly Dictionary<WindowsShellExplorerCommand, IExplorerCommand> _describedCommands = [];
	private readonly HashSet<FilesShellMenuEntry> _entries = [];
	private IContextMenu? _contextMenu;
	private IShellItemArray? _selection;
	private FilesShellContextMenuSite? _site;
	private HMENU _menu;
	private Task? _disposeTask;
	private bool _isDisposed;
	private int _workerReturned;

	public IReadOnlyList<FilesShellMenuEntry> Entries { get; private set; } = [];

	private FilesShellContextMenuSession(ContextMenuWorkerPool.Worker worker, string[] paths, HWND owner, System.Drawing.Point invocationPoint, bool locationBackground)
	{
		_worker = worker;
		_paths = paths;
		_owner = owner;
		_invocationPoint = invocationPoint;
		_locationBackground = locationBackground;
	}

	public static async Task<FilesShellContextMenuSession?> CreateAsync(IReadOnlyList<string> paths, bool extendedVerbs, bool locationBackground = false, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(paths);
		cancellationToken.ThrowIfCancellationRequested();
		if (paths.Count is 0 || paths.Any(string.IsNullOrWhiteSpace))
			return null;

		var worker = ContextMenuWorkerPool.Rent();
		PInvoke.GetCursorPos(out var point);
		var session = new FilesShellContextMenuSession(worker, paths.ToArray(), (HWND)MainWindow.Instance.WindowHandle, new(point.X, point.Y), locationBackground);
		var initialized = await worker.Thread.PostMethod(() =>
		{
			try
			{
				session.Initialize(extendedVerbs, cancellationToken);
				return true;
			}
			catch (Exception exception)
			{
				Debug.WriteLine(exception);
				session.ReleaseOnSta();
				return false;
			}
		}).ConfigureAwait(false);

		if (!initialized || session.Entries.Count is 0)
		{
			await session.DisposeAsync().ConfigureAwait(false);
			return null;
		}

		return session;
	}

	public List<ContextMenuFlyoutItemViewModel> CreateMenuItems()
	{
		var scale = MainWindow.Instance.Content.XamlRoot?.RasterizationScale ?? 1;
		var pixelSize = Math.Clamp((int)Math.Ceiling(16 * scale), 16, 256);
		return CreateMenuItems(Entries, GetIconTheme(), pixelSize);
	}

	private List<ContextMenuFlyoutItemViewModel> CreateMenuItems(IReadOnlyList<FilesShellMenuEntry> entries, FilesShellContextMenuIconTheme iconTheme, int pixelSize)
	{
		var models = new List<ContextMenuFlyoutItemViewModel>(entries.Count);
		foreach (var entry in entries)
		{
			if (entry.IsSeparator)
			{
				models.Add(new() { ItemType = ContextMenuFlyoutItemType.Separator, Tag = entry });
				continue;
			}

			var model = new ContextMenuFlyoutItemViewModel
			{
				Text = entry.Title,
				AccessKey = entry.AccessKey ?? string.Empty,
				Tag = entry,
				ID = entry.Id,
				IsEnabled = entry.IsEnabled,
				IsChecked = entry.IsChecked,
				ItemType = entry.IsChecked || entry.IsRadio ? ContextMenuFlyoutItemType.Toggle : ContextMenuFlyoutItemType.Item,
				ShowInRecycleBin = true,
				ShowInSearchPage = true,
				ShowInFtpPage = true,
				ShowInZipPage = true,
			};
			if (entry.Icon is not null || !entry.NativeIcon.IsNull || _nativeCommands.ContainsKey(entry))
			{
				var icon = new ImageIcon { Width = 16, Height = 16 };
				model.IconElement = icon;
				_ = LoadIconAsync(entry, icon, iconTheme, pixelSize);
			}

			if (entry.Children.Count is not 0)
			{
				model.Items = CreateMenuItems(entry.Children, iconTheme, pixelSize);
				model.IsEnabled = entry.Children.Any(static child => !child.IsSeparator && child.IsEnabled);
			}
			else if (entry.IsEnabled && (_nativeCommands.ContainsKey(entry) || _explorerCommands.ContainsKey(entry)))
			{
				model.Command = new AsyncRelayCommand(async () => await InvokeAsync(entry).ConfigureAwait(true));
			}

			models.Add(model);
		}

		return models;
	}

	private async Task LoadIconAsync(FilesShellMenuEntry entry, ImageIcon icon, FilesShellContextMenuIconTheme iconTheme, int pixelSize)
	{
		try
		{
			var iconData = await GetIconAsync(entry, iconTheme, pixelSize).ConfigureAwait(false);
			if (iconData is not { Length: > 0 })
				return;

			await MainWindow.Instance.DispatcherQueue.EnqueueOrInvokeAsync(async () =>
			{
				lock (_lifetimeLock)
				{
					if (_isDisposed)
						return;
				}

				using var stream = new MemoryStream(iconData, writable: false);
				if (iconData.AsSpan(0, Math.Min(iconData.Length, 256)).IndexOf("<svg"u8) >= 0)
				{
					var svg = new SvgImageSource();
					await svg.SetSourceAsync(stream.AsRandomAccessStream());
					lock (_lifetimeLock)
					{
						if (!_isDisposed)
							icon.Source = svg;
					}
				}
				else
				{
					var bitmap = await iconData.ToBitmapAsync(pixelSize);
					lock (_lifetimeLock)
					{
						if (!_isDisposed)
							icon.Source = bitmap;
					}
				}
			});
		}
		catch (Exception exception)
		{
			Debug.WriteLine(exception);
		}
	}

	private Task<byte[]?> GetIconAsync(FilesShellMenuEntry entry, FilesShellContextMenuIconTheme iconTheme, int pixelSize)
	{
		lock (_lifetimeLock)
		{
			if (_isDisposed || !_entries.Contains(entry))
				return Task.FromResult<byte[]?>(null);

			if (!_iconLoads.TryGetValue(entry, out var iconLoad))
			{
				iconLoad = _worker.Thread.PostMethod(() => FilesShellContextMenuIconSource.Read(entry.Id, entry.Icon, entry.NativeIcon, pixelSize, iconTheme));
				_iconLoads.Add(entry, iconLoad);
			}

			return iconLoad;
		}
	}

	private static FilesShellContextMenuIconTheme GetIconTheme()
	{
		if (new AccessibilitySettings().HighContrast)
		{
			var background = new UISettings().GetColorValue(UIColorType.Background);
			return 299 * background.R + 587 * background.G + 114 * background.B >= 128000
				? FilesShellContextMenuIconTheme.HighContrastWhite : FilesShellContextMenuIconTheme.HighContrastBlack;
		}

		return MainWindow.Instance.Content is FrameworkElement content && content.ActualTheme is ElementTheme.Dark
			? FilesShellContextMenuIconTheme.Dark : FilesShellContextMenuIconTheme.Light;
	}

	public Task<bool> InvokeAsync(FilesShellMenuEntry entry, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(entry);
		lock (_lifetimeLock)
		{
			if (_isDisposed || !_entries.Contains(entry))
				return Task.FromResult(false);

			return _worker.Thread.PostMethod(() => InvokeOnSta(entry, cancellationToken));
		}
	}

	public ValueTask DisposeAsync()
	{
		lock (_lifetimeLock)
		{
			if (_disposeTask is null)
			{
				_isDisposed = true;
				_disposeTask = _worker.Thread.PostMethod(ReleaseOnSta);
			}
		}

		return new(CompleteDisposeAsync());
	}

	private async Task CompleteDisposeAsync()
	{
		Task disposeTask;
		lock (_lifetimeLock)
			disposeTask = _disposeTask!;

		await disposeTask.ConfigureAwait(false);
		if (Interlocked.Exchange(ref _workerReturned, 1) is 0)
			ContextMenuWorkerPool.Return(_worker);
	}

	private void Initialize(bool extendedVerbs, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		_selection = CreateShellItemArray(_paths);
		_site = new(_owner);
		IReadOnlyList<WindowsShellNativeMenuEntry> cloudVerbs = [];
		try
		{
			if (_locationBackground)
			{
				_selection.GetItemAt(0, out var folderItem).ThrowOnFailure();
				folderItem.BindToHandler<IShellFolder>(null, PInvoke.BHID_SFObject, out var folder).ThrowOnFailure();
				folder.CreateViewObject(_owner, out IContextMenu contextMenu).ThrowOnFailure();
				_contextMenu = contextMenu;
			}
			else
			{
				_selection.BindToHandler<IContextMenu>(null, PInvoke.BHID_SFUIObject, out var contextMenu).ThrowOnFailure();
				_contextMenu = contextMenu;
			}

			if (_contextMenu is IObjectWithSite objectWithSite)
				objectWithSite.SetSite(_site).ThrowOnFailure();
			_menu = PInvoke.CreatePopupMenu();
			if (_menu.IsNull)
				throw new Win32Exception(Marshal.GetLastPInvokeError());

			var flags = ItemMenuFlag | (extendedVerbs ? ExtendedVerbsFlag : 0u);
			_contextMenu.QueryContextMenu(_menu, 0, FirstCommandId, LastCommandId, flags).ThrowOnFailure();
			cloudVerbs = ReadCloudVerbs(_menu, _contextMenu);
		}
		catch (Exception exception) when (exception is COMException or Win32Exception)
		{
			Debug.WriteLine(exception);
		}

		var provider = _locationBackground ? null : WindowsShellCloudProviderSource.GetProviderForSelection(_paths);
		var extensions = _locationBackground
			? WindowsFileExplorerAppExtensionCatalog.GetExtensions(["Directory\\Background"])
			: GetCommonExtensions(_paths);
		var groups = ReadExtensionGroups(extensions, provider, cancellationToken);
		Entries = BuildEntries(cloudVerbs, groups);
		RegisterEntries(Entries);
		cancellationToken.ThrowIfCancellationRequested();
	}

	private IReadOnlyList<FilesShellMenuGroup> ReadExtensionGroups(IReadOnlyList<WindowsFileExplorerAppExtensionGroup> extensions,
		WindowsShellCloudProviderInfo? provider, CancellationToken cancellationToken)
	{
		var groups = new List<FilesShellMenuGroup>();
		var providerAdded = false;
		foreach (var extension in extensions)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var isProvider = provider is not null && !string.IsNullOrWhiteSpace(provider.PackageFullName)
				&& string.Equals(extension.PackageFullName, provider.PackageFullName, StringComparison.OrdinalIgnoreCase);
			var registrations = extension.Registrations.ToList();
			if (isProvider)
			{
				providerAdded = true;
				AppendProviderRegistrations(registrations, provider!);
			}

			var entries = ReadExplorerCommands(registrations, cancellationToken);
			if (entries.Count is not 0)
				groups.Add(new(isProvider ? provider!.DisplayName ?? extension.DisplayName : extension.DisplayName, isProvider, entries,
					CreateIcon(isProvider ? provider!.LogoPath ?? extension.LogoPath : extension.LogoPath, extension.PackageFullName,
						isProvider ? provider!.IconPath : null, isProvider ? provider!.IconIndex : 0)));
		}

		if (provider is not null && !providerAdded && !string.IsNullOrWhiteSpace(provider.PackageFullName))
		{
			var registrations = new List<WindowsFileExplorerAppExtensionRegistration>();
			AppendProviderRegistrations(registrations, provider);
			var entries = ReadExplorerCommands(registrations, cancellationToken);
			if (entries.Count is not 0)
				groups.Add(new(provider.DisplayName ?? string.Empty, true, entries,
					CreateIcon(provider.LogoPath, provider.PackageFullName, provider.IconPath, provider.IconIndex)));
		}

		return groups;
	}

	private IReadOnlyList<FilesShellMenuEntry> ReadExplorerCommands(IReadOnlyList<WindowsFileExplorerAppExtensionRegistration> registrations, CancellationToken cancellationToken)
	{
		var entries = new List<FilesShellMenuEntry>(registrations.Count);
		foreach (var registration in registrations)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				if (FilesShellExplorerCommandSource.TryCreateExplorerCommand(registration, _site) is not { } command)
					continue;

				var description = FilesShellExplorerCommandSource.TryCreateDescription(
					command,
					_selection!,
					new(registration.ClassId, registration.VerbId, []),
					registration.DisplayName,
					0,
					cancellationToken,
					registration.StaticDisplayName,
					registration.StaticIconPath,
					iconPackageFullName: registration.PackageFullName,
					retain: (entry, backend) => _describedCommands.Add(entry, backend));
				if (description is not null)
					entries.Add(ConvertExplorerCommand(description));
			}
			catch (COMException exception)
			{
				Debug.WriteLine(exception);
			}
		}

		return entries;
	}

	private FilesShellMenuEntry ConvertExplorerCommand(WindowsShellExplorerCommand command)
	{
		var children = command.Children.Select(ConvertExplorerCommand).ToArray();
		var icon = CreateIcon(command.IconPath, command.IconPackageFullName, command.FallbackIconPath, command.FallbackIconIndex, command.FallbackIconPackageFullName, command.IconIndex);
		icon = WithFallbackIcon(icon, FindFirstIcon(children));
		var entry = new FilesShellMenuEntry(command.Id, command.Title, command.IsEnabled, command.IsChecked, command.IsRadio,
			command.IsSeparator, children, icon);
		if (_describedCommands.TryGetValue(command, out var backend))
			_explorerCommands.Add(entry, backend);

		return entry;
	}

	private IReadOnlyList<FilesShellMenuEntry> BuildEntries(IReadOnlyList<WindowsShellNativeMenuEntry> cloudVerbs, IReadOnlyList<FilesShellMenuGroup> groups)
	{
		var entries = new List<FilesShellMenuEntry>();
		var cloudEntries = new List<FilesShellMenuEntry>();
		foreach (var verb in cloudVerbs)
		{
			var entry = new FilesShellMenuEntry(verb.Verb, verb.Title, verb.IsEnabled, verb.IsChecked, verb.IsRadio, nativeIcon: verb.Bitmap);
			cloudEntries.Add(entry);
			_nativeCommands[entry] = verb.Ordinal;
		}

		AppendSection(entries, cloudEntries.Concat(groups.Where(static group => group.IsCloudProvider)
			.OrderBy(static group => group.Title, StringComparer.OrdinalIgnoreCase).SelectMany(CreateGroupEntries)));
		AppendSection(entries, groups.Where(static group => !group.IsCloudProvider)
			.OrderBy(static group => group.Title, StringComparer.OrdinalIgnoreCase).SelectMany(CreateGroupEntries));
		RemoveTrailingSeparator(entries);

		return entries;
	}

	private static IEnumerable<FilesShellMenuEntry> CreateGroupEntries(FilesShellMenuGroup group)
	{
		var entries = new List<FilesShellMenuEntry>();
		Append(entries, group.Entries);
		RemoveTrailingSeparator(entries);
		if (entries.Count is 0)
			return [];

		if (!group.IsCloudProvider && entries.Count(static entry => !entry.IsSeparator) is 1)
			return entries;

		if (string.IsNullOrWhiteSpace(group.Title))
			return entries;

		return [new FilesShellMenuEntry(group.Title, group.Title, group.Entries.Any(static entry => !entry.IsSeparator && entry.IsEnabled),
			children: entries, icon: WithFallbackIcon(group.Icon, FindFirstIcon(entries)))];
	}

	private static FilesShellMenuIcon? FindFirstIcon(IReadOnlyList<FilesShellMenuEntry> entries)
	{
		foreach (var entry in entries)
		{
			if (entry.IsSeparator)
				continue;

			if (entry.Icon is { } icon)
				return icon;

			if (FindFirstIcon(entry.Children) is { } childIcon)
				return childIcon;
		}

		return null;
	}

	private static FilesShellMenuIcon? WithFallbackIcon(FilesShellMenuIcon? icon, FilesShellMenuIcon? fallbackIcon)
	{
		if (icon is null || fallbackIcon is null)
			return icon ?? fallbackIcon;

		if (!string.IsNullOrWhiteSpace(icon.FallbackPath) || icon == fallbackIcon)
			return icon;

		return icon with
		{
			FallbackPath = fallbackIcon.Path,
			FallbackIndex = fallbackIcon.Index,
			FallbackPackageFullName = fallbackIcon.PackageFullName,
		};
	}

	private static FilesShellMenuIcon? CreateIcon(string? path, string? packageFullName, string? fallbackPath = null, int fallbackIndex = 0,
		string? fallbackPackageFullName = null, int? resourceIndex = null)
	{
		var (parsedPath, parsedIndex) = FilesShellExplorerCommandSource.ParseCommandIcon(path);
		if (parsedPath is null)
			return string.IsNullOrWhiteSpace(fallbackPath) ? null : new(fallbackPath, fallbackIndex, fallbackPackageFullName ?? packageFullName);

		return new(parsedPath, resourceIndex ?? parsedIndex, packageFullName, fallbackPath, fallbackIndex, fallbackPackageFullName ?? packageFullName);
	}

	private static void AppendSection(List<FilesShellMenuEntry> destination, IEnumerable<FilesShellMenuEntry> source)
	{
		var entries = source.ToArray();
		if (!entries.Any(static entry => !entry.IsSeparator))
			return;

		if (destination.Count is not 0)
			Append(destination, [FilesShellMenuEntry.Separator]);
		Append(destination, entries);
	}

	private static void Append(List<FilesShellMenuEntry> destination, IEnumerable<FilesShellMenuEntry> source)
	{
		foreach (var entry in source)
		{
			if (!entry.IsSeparator || destination.Count is not 0 && !destination[^1].IsSeparator)
				destination.Add(entry);
		}
	}

	private static void RemoveTrailingSeparator(List<FilesShellMenuEntry> entries)
	{
		if (entries.Count is not 0 && entries[^1].IsSeparator)
			entries.RemoveAt(entries.Count - 1);
	}

	private void RegisterEntries(IReadOnlyList<FilesShellMenuEntry> entries)
	{
		foreach (var entry in entries)
		{
			_entries.Add(entry);
			RegisterEntries(entry.Children);
		}
	}

	private bool InvokeOnSta(FilesShellMenuEntry entry, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		try
		{
			if (!entry.IsEnabled || entry.IsSeparator || entry.Children.Count is not 0)
				return false;

			if (_nativeCommands.TryGetValue(entry, out var ordinal) && _contextMenu is not null)
			{
				InvokeNativeCommand(_contextMenu, ordinal);
				return true;
			}

			var workingDirectory = _locationBackground ? _paths[0] : Path.GetDirectoryName(_paths[0]);
			return _explorerCommands.TryGetValue(entry, out var command) && _selection is not null
				&& FilesShellExplorerCommandSource.Invoke(command, _selection, workingDirectory, _invocationPoint);
		}
		catch (Exception exception) when (exception is COMException or Win32Exception)
		{
			Debug.WriteLine(exception);
			return false;
		}
	}

	private unsafe void InvokeNativeCommand(IContextMenu contextMenu, uint ordinal)
	{
		var invoke = default(CMINVOKECOMMANDINFOEX);
		invoke.cbSize = (uint)sizeof(CMINVOKECOMMANDINFOEX);
		invoke.fMask = UnicodeInvokeFlag | InvokePointFlag;
		invoke.hwnd = _owner;
		invoke.lpVerb = (PCSTR)(byte*)(nuint)ordinal;
		invoke.lpVerbW = (PCWSTR)(char*)(nuint)ordinal;
		invoke.nShow = (int)SHOW_WINDOW_CMD.SW_SHOWNORMAL;
		invoke.ptInvoke = _invocationPoint;
		ref var baseInvoke = ref Unsafe.As<CMINVOKECOMMANDINFOEX, CMINVOKECOMMANDINFO>(ref invoke);
		contextMenu.InvokeCommand(baseInvoke).ThrowOnFailure();
	}

	private void ReleaseOnSta()
	{
		foreach (var command in _describedCommands.Values)
			DetachSite(command);
		if (_contextMenu is not null)
			DetachSite(_contextMenu);
		if (!_menu.IsNull)
			PInvoke.DestroyMenu(_menu);
		_menu = default;
		_nativeCommands.Clear();
		_explorerCommands.Clear();
		_describedCommands.Clear();
		_entries.Clear();
		lock (_lifetimeLock)
			_iconLoads.Clear();
		_contextMenu = null;
		_selection = null;
		_site = null;
	}

	private static void DetachSite(object command)
	{
		if (command is IObjectWithSite objectWithSite)
		{
			try
			{
				objectWithSite.SetSite(null);
			}
			catch (COMException exception)
			{
				Debug.WriteLine(exception);
			}
		}
	}

	internal static unsafe IShellItemArray CreateShellItemArray(IReadOnlyList<string> paths)
	{
		var shellItems = new List<ShellItem>(paths.Count);
		ITEMIDLIST** pidls = null;
		try
		{
			foreach (var path in paths)
				shellItems.Add(ShellFolderExtensions.GetShellItemFromPathOrPIDL(path));

			pidls = (ITEMIDLIST**)NativeMemory.AllocZeroed((nuint)shellItems.Count, (nuint)sizeof(ITEMIDLIST*));
			for (var index = 0; index < shellItems.Count; index++)
				PInvoke.SHGetIDListFromObject(shellItems[index].IShellItem, out pidls[index]).ThrowOnFailure();

			PInvoke.SHCreateShellItemArrayFromIDLists((uint)shellItems.Count, pidls, out IShellItemArray selection).ThrowOnFailure();
			return selection;
		}
		finally
		{
			if (pidls is not null)
			{
				for (var index = 0; index < shellItems.Count; index++)
					PInvoke.CoTaskMemFree(pidls[index]);
				NativeMemory.Free(pidls);
			}

			foreach (var shellItem in shellItems)
				shellItem.Dispose();
		}
	}

	private static IReadOnlyList<WindowsFileExplorerAppExtensionGroup> GetCommonExtensions(IReadOnlyList<string> paths)
	{
		var allTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var queriedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<(Guid ClassId, string VerbId)>? commonRegistrations = null;
		foreach (var path in paths)
		{
			var itemTypes = Directory.Exists(path)
				? new[] { "Directory" }
				: new[] { Path.GetExtension(path).ToLowerInvariant(), "*" };
			if (string.IsNullOrEmpty(itemTypes[0]))
				itemTypes = ["*"];

			allTypes.UnionWith(itemTypes);
			if (!queriedTypes.Add(itemTypes[0]))
				continue;

			var registrations = WindowsFileExplorerAppExtensionCatalog.GetExtensions(itemTypes)
				.SelectMany(static extension => extension.Registrations).Select(static registration => (registration.ClassId, registration.VerbId));
			if (commonRegistrations is null)
				commonRegistrations = registrations.ToHashSet();
			else
				commonRegistrations.IntersectWith(registrations);
		}

		if (commonRegistrations is not { Count: > 0 })
			return [];

		return WindowsFileExplorerAppExtensionCatalog.GetExtensions(allTypes)
			.Select(extension => extension with { Registrations = extension.Registrations.Where(registration => commonRegistrations.Contains((registration.ClassId, registration.VerbId))).ToArray() })
			.Where(static extension => extension.Registrations.Count is not 0).ToArray();
	}

	private static unsafe IReadOnlyList<WindowsShellNativeMenuEntry> ReadCloudVerbs(HMENU menu, IContextMenu contextMenu)
	{
		var entries = new List<WindowsShellNativeMenuEntry>(2);
		var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Span<char> buffer = stackalloc char[1024];
		var itemCount = PInvoke.GetMenuItemCount(menu);
		for (var index = itemCount - 1; index >= 0 && found.Count < 2; index--)
		{
			var info = default(MENUITEMINFOW);
			info.cbSize = (uint)sizeof(MENUITEMINFOW);
			info.fMask = MENU_ITEM_MASK.MIIM_ID | MENU_ITEM_MASK.MIIM_STATE | MENU_ITEM_MASK.MIIM_FTYPE | MENU_ITEM_MASK.MIIM_STRING
				| MENU_ITEM_MASK.MIIM_BITMAP | MENU_ITEM_MASK.MIIM_CHECKMARKS;
			buffer.Clear();
			fixed (char* title = buffer)
			{
				info.dwTypeData = title;
				info.cch = (uint)buffer.Length;
				if (!PInvoke.GetMenuItemInfo(menu, (uint)index, true, &info) || info.wID < FirstCommandId || info.wID > LastCommandId)
					continue;

				var ordinal = info.wID - FirstCommandId;
				var verb = GetCanonicalVerb(contextMenu, ordinal);
				if (verb is null || !IsCloudVerb(verb) || !found.Add(verb))
					continue;

				var bitmap = info.hbmpItem;
				if (bitmap.IsNull || (nint)bitmap.Value <= 12)
					bitmap = info.fState.HasFlag(MENU_ITEM_STATE.MFS_CHECKED) ? info.hbmpChecked : info.hbmpUnchecked;

				entries.Insert(0, new(verb, new string(buffer[..Math.Min((int)info.cch, buffer.Length)]), ordinal,
					!info.fState.HasFlag(MENU_ITEM_STATE.MFS_DISABLED), info.fState.HasFlag(MENU_ITEM_STATE.MFS_CHECKED), info.fType.HasFlag(MENU_ITEM_TYPE.MFT_RADIOCHECK),
					(nint)bitmap.Value > 12 ? bitmap : default));
			}
		}

		return entries;
	}

	private static bool IsCloudVerb(string verb)
		=> string.Equals(verb, "MakeAvailableOffline", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(verb, "MakeAvailableOnline", StringComparison.OrdinalIgnoreCase);

	private static unsafe string? GetCanonicalVerb(IContextMenu contextMenu, uint ordinal)
	{
		Span<char> buffer = stackalloc char[CanonicalVerbBufferLength];
		buffer.Clear();
		fixed (char* pointer = buffer)
		{
			if (contextMenu.GetCommandString(ordinal, CanonicalVerbUnicode, (PSTR)(byte*)pointer, (uint)buffer.Length).Failed)
				return null;
		}

		var bytes = MemoryMarshal.AsBytes(buffer);
		var byteLength = bytes.IndexOf((byte)0);
		if (byteLength > 1 && bytes[..byteLength].IndexOfAnyExceptInRange((byte)0x20, (byte)0x7e) < 0)
			return Encoding.ASCII.GetString(bytes[..byteLength]);

		var terminator = buffer.IndexOf('\0');
		var value = new string(terminator < 0 ? buffer : buffer[..terminator]);
		return string.IsNullOrWhiteSpace(value) ? null : value;
	}

	private static void AppendProviderRegistrations(List<WindowsFileExplorerAppExtensionRegistration> registrations, WindowsShellCloudProviderInfo provider)
	{
		foreach (var classId in provider.CloudFileVerbClassIds)
		{
			if (!registrations.Any(registration => registration.ClassId == classId))
				registrations.Add(new(classId, classId.ToString("B"), provider.DisplayName ?? string.Empty, provider.PackageFullName, IsCloudProviderVerb: true));
		}
	}
}

internal sealed class FilesShellMenuEntry
{
	internal FilesShellMenuEntry(string id, string title, bool enabled, bool isChecked = false, bool isRadio = false, bool isSeparator = false,
		IReadOnlyList<FilesShellMenuEntry>? children = null, FilesShellMenuIcon? icon = null, HBITMAP nativeIcon = default)
	{
		Id = id;
		var (cleanTitle, accessKey) = ParseLabel(title);
		Title = cleanTitle;
		AccessKey = accessKey;
		IsEnabled = enabled;
		IsChecked = isChecked;
		IsRadio = isRadio;
		IsSeparator = isSeparator;
		Children = children ?? [];
		Icon = icon;
		NativeIcon = nativeIcon;
	}

	internal string Id { get; }
	internal string Title { get; }
	internal string? AccessKey { get; }
	internal bool IsEnabled { get; }
	internal bool IsChecked { get; }
	internal bool IsRadio { get; }
	internal bool IsSeparator { get; }
	internal IReadOnlyList<FilesShellMenuEntry> Children { get; }
	internal FilesShellMenuIcon? Icon { get; }
	internal HBITMAP NativeIcon { get; }
	internal static FilesShellMenuEntry Separator => new(string.Empty, string.Empty, false, isSeparator: true);

	private static (string Title, string? AccessKey) ParseLabel(string title)
	{
		var shortcutIndex = title.IndexOf('\t');
		var label = shortcutIndex < 0 ? title : title[..shortcutIndex];
		var result = new StringBuilder(label.Length);
		string? accessKey = null;
		for (var index = 0; index < label.Length; index++)
		{
			var character = label[index];
			if (character is not '&' || index + 1 >= label.Length)
			{
				result.Append(character);
				continue;
			}

			character = label[++index];
			if (character is '&')
				result.Append(character);
			else
			{
				accessKey ??= character.ToString();
				if (index >= 2 && label[index - 2] is '(' && index + 1 < label.Length && label[index + 1] is ')')
				{
					if (result.Length > 0 && result[^1] is '(')
					{
						result.Length--;
						while (result.Length > 0 && char.IsWhiteSpace(result[^1]))
							result.Length--;
					}

					index++;
					continue;
				}

				result.Append(character);
			}
		}

		return (result.ToString(), accessKey);
	}
}

internal sealed record FilesShellMenuGroup(string Title, bool IsCloudProvider, IReadOnlyList<FilesShellMenuEntry> Entries, FilesShellMenuIcon? Icon = null);
internal sealed record FilesShellMenuIcon(string Path, int Index, string? PackageFullName, string? FallbackPath = null, int FallbackIndex = 0, string? FallbackPackageFullName = null);
internal sealed record WindowsShellNativeMenuEntry(string Verb, string Title, uint Ordinal, bool IsEnabled, bool IsChecked, bool IsRadio, HBITMAP Bitmap);
