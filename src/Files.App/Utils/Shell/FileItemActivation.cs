// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Files.App.Utils.Shell
{
	internal static class FileItemActivation
	{
		public static unsafe void Activate(string path, HWND ownerHwnd)
		{
			STATask.Run(() =>
			{
				HRESULT hr = PInvoke.SHCreateItemFromParsingName(path, null, out IShellItem shellItem);
				if (hr.ThrowIfFailedOnDebug().Failed)
					return;

				uint messagePosition = PInvoke.GetMessagePos();

				hr = shellItem.BindToHandler<IContextMenu>(null, PInvoke.BHID_SFUIObject, out IContextMenu contextMenu);
				if (hr.ThrowIfFailedOnDebug().Failed)
					return;

				HMENU menu = PInvoke.CreatePopupMenu();
				if (menu.IsNull)
					Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

				var workingDirW = Path.GetDirectoryName(path);
				var workingDirA = workingDirW is null ? nint.Zero : Marshal.StringToCoTaskMemAnsi(workingDirW);

				try
				{
					hr = contextMenu.QueryContextMenu(menu, 0, 1, 0x7FFF, PInvoke.CMF_DEFAULTONLY | PInvoke.CMF_EXPLORE | PInvoke.CMF_OPTIMIZEFORINVOKE);
					if (hr.ThrowIfFailedOnDebug().Failed)
						return;

					uint commandId = PInvoke.GetMenuDefaultItem(menu, 0, 0);
					if (commandId == 0xFFFFFFFF)
						hr = HRESULT.E_FAIL;
					if (hr.ThrowIfFailedOnDebug().Failed)
						return;

					fixed (char* workingDirWPtr = workingDirW)
					{
						CMINVOKECOMMANDINFOEX invoke = default;
						invoke.cbSize = (uint)sizeof(CMINVOKECOMMANDINFOEX);
						invoke.fMask =
							0x00000100 | // CMIC_MASK_NOASYNC
							0x00004000 | // CMIC_MASK_UNICODE
							0x04000000 | // CMIC_MASK_FLAG_LOG_USAGE
							0x20000000;  // CMIC_MASK_PTINVOKE
						invoke.hwnd = ownerHwnd;
						invoke.lpVerb = (PCSTR)(byte*)(commandId - 1);
						invoke.lpDirectory = (PCSTR)(byte*)workingDirA;
						invoke.nShow = (int)SHOW_WINDOW_CMD.SW_SHOWNORMAL;
						invoke.lpDirectoryW = workingDirWPtr;
						invoke.ptInvoke = new(unchecked((short)(messagePosition & 0xFFFF)), unchecked((short)(messagePosition >> 16)));

						ref CMINVOKECOMMANDINFO baseInvoke = ref Unsafe.As<CMINVOKECOMMANDINFOEX, CMINVOKECOMMANDINFO>(ref invoke);

						hr = contextMenu.InvokeCommand(baseInvoke);
						if (hr.ThrowIfFailedOnDebug().Failed)
							return;
					}
				}
				finally
				{
					if (workingDirA != nint.Zero)
						Marshal.FreeCoTaskMem(workingDirA);

					if (menu != HMENU.Null)
						PInvoke.DestroyMenu(menu);
				}
			},
			null);
		}
	}
}
