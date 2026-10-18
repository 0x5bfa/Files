// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Files.App.Utils.Shell.ContextMenus;

/// <summary>Shows a classic Shell context menu on the Files window's UI STA.</summary>
internal sealed class FilesShellClassicContextMenuSession
{
	private const uint ExtendedVerbsFlag = 0x00000100;
	private const uint FirstCommandId = 1;
	private const uint LastCommandId = 0x7FFF;
	private const uint InvokePointFlag = 0x20000000;
	private const uint UnicodeInvokeFlag = 0x00004000;
	private const uint SubclassId = 0x46434D55;
	private const int VirtualKeyShift = 0x10;

	private IContextMenu2? _contextMenu2;
	private IContextMenu3? _contextMenu3;

	internal static unsafe bool Show(IReadOnlyList<string> paths, bool locationBackground, HWND owner, Point invocationPoint)
	{
		if (paths.Count is 0 || owner.IsNull)
			return false;

		try
		{
			IContextMenu contextMenu;
			if (locationBackground)
			{
				using var folderItem = ShellFolderExtensions.GetShellItemFromPathOrPIDL(paths[0]);
				folderItem.IShellItem.BindToHandler<IShellFolder>(null, PInvoke.BHID_SFObject, out var folder).ThrowOnFailure();
				folder.CreateViewObject(owner, out contextMenu).ThrowOnFailure();
			}
			else
			{
				var selection = FilesShellContextMenuSession.CreateShellItemArray(paths);
				selection.BindToHandler<IContextMenu>(null, PInvoke.BHID_SFUIObject, out contextMenu).ThrowOnFailure();
			}

			using var menu = PInvoke.CreatePopupMenu_SafeHandle();
			if (menu.IsInvalid)
				throw new Win32Exception(Marshal.GetLastPInvokeError());

			var queryFlags = PInvoke.GetKeyState(VirtualKeyShift) < 0 ? ExtendedVerbsFlag : 0u;
			contextMenu.QueryContextMenu(menu, 0, FirstCommandId, LastCommandId, queryFlags).ThrowOnFailure();
			var session = new FilesShellClassicContextMenuSession
			{
				_contextMenu2 = contextMenu as IContextMenu2,
				_contextMenu3 = contextMenu as IContextMenu3,
			};
			var sessionHandle = GCHandle.Alloc(session);
			var subclassInstalled = false;
			try
			{
				delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, nuint, nuint, LRESULT> subclassProcedure = &WindowSubclassProcedure;
				if (PInvoke.SetWindowSubclass(owner, subclassProcedure, SubclassId, (nuint)GCHandle.ToIntPtr(sessionHandle)).Value is 0)
					throw new Win32Exception(Marshal.GetLastPInvokeError());

				subclassInstalled = true;
				PInvoke.SetForegroundWindow(owner);
				var commandId = unchecked((uint)PInvoke.TrackPopupMenuEx(menu,
					(uint)(TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD | TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON),
					invocationPoint.X, invocationPoint.Y, owner, null).Value);
				PInvoke.PostMessage(owner, PInvoke.WM_NULL, default, default);
				if (commandId is 0)
					return false;

				InvokeCommand(contextMenu, commandId - FirstCommandId, owner, invocationPoint);
				return true;
			}
			finally
			{
				if (subclassInstalled)
				{
					delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, nuint, nuint, LRESULT> subclassProcedure = &WindowSubclassProcedure;
					PInvoke.RemoveWindowSubclass(owner, subclassProcedure, SubclassId);
				}

				sessionHandle.Free();
				session._contextMenu2 = null;
				session._contextMenu3 = null;
			}
		}
		catch (Exception exception)
		{
			Debug.WriteLine(exception);
			return false;
		}
	}

	private static unsafe void InvokeCommand(IContextMenu contextMenu, uint commandOrdinal, HWND owner, Point invocationPoint)
	{
		var invoke = default(CMINVOKECOMMANDINFOEX);
		invoke.cbSize = (uint)sizeof(CMINVOKECOMMANDINFOEX);
		invoke.fMask = UnicodeInvokeFlag | InvokePointFlag;
		invoke.hwnd = owner;
		invoke.lpVerb = (PCSTR)(byte*)(nuint)commandOrdinal;
		invoke.lpVerbW = (PCWSTR)(char*)(nuint)commandOrdinal;
		invoke.nShow = (int)SHOW_WINDOW_CMD.SW_SHOWNORMAL;
		invoke.ptInvoke = new(invocationPoint.X, invocationPoint.Y);
		ref var baseInvoke = ref Unsafe.As<CMINVOKECOMMANDINFOEX, CMINVOKECOMMANDINFO>(ref invoke);
		contextMenu.InvokeCommand(baseInvoke).ThrowOnFailure();
	}

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	private static LRESULT WindowSubclassProcedure(HWND window, uint message, WPARAM wParam, LPARAM lParam, nuint subclassId, nuint referenceData)
	{
		try
		{
			var sessionHandle = GCHandle.FromIntPtr((nint)referenceData);
			if (sessionHandle.Target is FilesShellClassicContextMenuSession session)
				return session.HandleWindowMessage(window, message, wParam, lParam);
		}
		catch (Exception exception)
		{
			Debug.WriteLine(exception);
		}

		return PInvoke.DefSubclassProc(window, message, wParam, lParam);
	}

	private unsafe LRESULT HandleWindowMessage(HWND window, uint message, WPARAM wParam, LPARAM lParam)
	{
		if (message is PInvoke.WM_INITMENUPOPUP or PInvoke.WM_DRAWITEM or PInvoke.WM_MEASUREITEM or PInvoke.WM_MENUCHAR)
		{
			if (_contextMenu3 is not null)
			{
				var result = default(LRESULT);
				if (_contextMenu3.HandleMenuMsg2(message, wParam, lParam, &result).Succeeded)
					return result;
			}

			if (_contextMenu2 is not null && _contextMenu2.HandleMenuMsg(message, wParam, lParam).Succeeded)
				return default;
		}

		return PInvoke.DefSubclassProc(window, message, wParam, lParam);
	}
}
