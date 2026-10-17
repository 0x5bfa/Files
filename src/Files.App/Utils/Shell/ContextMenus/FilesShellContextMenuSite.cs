// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices.Marshalling;
using Windows.Win32.Foundation;
using Windows.Win32.System.Ole;

namespace Files.App.Utils.Shell.ContextMenus;

[GeneratedComClass]
internal sealed unsafe partial class FilesShellContextMenuSite : IOleWindow
{
	private readonly HWND _owner;

	internal FilesShellContextMenuSite(HWND owner)
	{
		_owner = owner;
	}

	public HRESULT GetWindow(HWND* window)
	{
		if (window is null)
			return HRESULT.E_POINTER;

		*window = _owner;
		return HRESULT.S_OK;
	}

	public HRESULT ContextSensitiveHelp(BOOL enterMode)
		=> HRESULT.E_NOTIMPL;
}
