// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage.Windows;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Security.Principal;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Networking.ActiveDirectory;
using Windows.Win32.System.Com;
using Windows.Win32.System.Variant;

namespace Files.App.Adapters.Legacy
{
	internal static class WindowsObjectPicker
	{
		private const string ObjectSidAttributeName = "objectSid";

		public static async Task<string?> OpenObjectPickerAsync(
			nint ownerWindow,
			ILogger? logger)
		{
			try
			{
				var source = App.CoreHost.Runtime.DataRoot.Sources
					.OfType<WindowsStorageSource>()
					.Single();
				return await source.Scheduler.InvokeAsync(
					() => OpenObjectPicker((HWND)ownerWindow));
			}
			catch (Exception exception)
			{
				logger?.LogWarning(exception, "The Windows object picker failed.");
				return null;
			}
		}

		private static unsafe string? OpenObjectPicker(HWND ownerWindow)
		{
			var result = PInvoke.CoCreateInstance(
				PInvoke.CLSID_DsObjectPicker,
				null,
				CLSCTX.CLSCTX_INPROC_SERVER,
				out IDsObjectPicker? picker);
			if (result.Failed || picker is null)
				return null;

			DSOP_SCOPE_INIT_INFO* scopes = stackalloc DSOP_SCOPE_INIT_INFO[2];
			scopes[0] = CreateScopeInitInfo(PInvoke.DSOP_SCOPE_TYPE_TARGET_COMPUTER, true);
			scopes[1] = CreateScopeInitInfo(
				PInvoke.DSOP_SCOPE_TYPE_UPLEVEL_JOINED_DOMAIN
					| PInvoke.DSOP_SCOPE_TYPE_DOWNLEVEL_JOINED_DOMAIN
					| PInvoke.DSOP_SCOPE_TYPE_ENTERPRISE_DOMAIN
					| PInvoke.DSOP_SCOPE_TYPE_GLOBAL_CATALOG
					| PInvoke.DSOP_SCOPE_TYPE_EXTERNAL_UPLEVEL_DOMAIN
					| PInvoke.DSOP_SCOPE_TYPE_EXTERNAL_DOWNLEVEL_DOMAIN
					| PInvoke.DSOP_SCOPE_TYPE_WORKGROUP
					| PInvoke.DSOP_SCOPE_TYPE_USER_ENTERED_UPLEVEL_SCOPE
					| PInvoke.DSOP_SCOPE_TYPE_USER_ENTERED_DOWNLEVEL_SCOPE,
				false);

			fixed (char* attributeName = ObjectSidAttributeName)
			{
				PCWSTR* attributeNames = stackalloc PCWSTR[1];
				attributeNames[0] = attributeName;

				var initialization = new DSOP_INIT_INFO
				{
					cbSize = (uint)sizeof(DSOP_INIT_INFO),
					cDsScopeInfos = 2,
					aDsScopeInfos = scopes,
					cAttributesToFetch = 1,
					apwzAttributeNames = attributeNames,
				};
				if (picker.Initialize(ref initialization).Failed)
					return null;
			}

			result = picker.InvokeDialog(ownerWindow, out IDataObject selections);
			return result == HRESULT.S_FALSE || result.Failed
				? null
				: GetSelectedSid(selections);
		}

		private static unsafe DSOP_SCOPE_INIT_INFO CreateScopeInitInfo(
			uint scopeType,
			bool startingScope)
		{
			const uint defaultFilter =
				PInvoke.DSOP_SCOPE_FLAG_DEFAULT_FILTER_USERS
					| PInvoke.DSOP_SCOPE_FLAG_DEFAULT_FILTER_GROUPS;
			const uint upLevelFilter =
				PInvoke.DSOP_FILTER_INCLUDE_ADVANCED_VIEW
					| PInvoke.DSOP_FILTER_USERS
					| PInvoke.DSOP_FILTER_BUILTIN_GROUPS
					| PInvoke.DSOP_FILTER_WELL_KNOWN_PRINCIPALS
					| PInvoke.DSOP_FILTER_UNIVERSAL_GROUPS_DL
					| PInvoke.DSOP_FILTER_UNIVERSAL_GROUPS_SE
					| PInvoke.DSOP_FILTER_GLOBAL_GROUPS_DL
					| PInvoke.DSOP_FILTER_GLOBAL_GROUPS_SE
					| PInvoke.DSOP_FILTER_DOMAIN_LOCAL_GROUPS_DL
					| PInvoke.DSOP_FILTER_DOMAIN_LOCAL_GROUPS_SE
					| PInvoke.DSOP_FILTER_CONTACTS
					| PInvoke.DSOP_FILTER_COMPUTERS
					| PInvoke.DSOP_FILTER_SERVICE_ACCOUNTS;
			const uint downLevelFilter =
				PInvoke.DSOP_DOWNLEVEL_FILTER_USERS
					| PInvoke.DSOP_DOWNLEVEL_FILTER_LOCAL_GROUPS
					| PInvoke.DSOP_DOWNLEVEL_FILTER_GLOBAL_GROUPS
					| PInvoke.DSOP_DOWNLEVEL_FILTER_COMPUTERS
					| PInvoke.DSOP_DOWNLEVEL_FILTER_ALL_WELLKNOWN_SIDS;

			var info = new DSOP_SCOPE_INIT_INFO
			{
				cbSize = (uint)sizeof(DSOP_SCOPE_INIT_INFO),
				flType = scopeType,
				flScope = startingScope
					? PInvoke.DSOP_SCOPE_FLAG_STARTING_SCOPE | defaultFilter
					: defaultFilter,
			};
			info.FilterFlags.Uplevel.flBothModes = upLevelFilter;
			info.FilterFlags.flDownlevel = downLevelFilter;
			return info;
		}

		private static unsafe string? GetSelectedSid(IDataObject selections)
		{
			var clipboardFormat = PInvoke.RegisterClipboardFormat(
				PInvoke.CFSTR_DSOP_DS_SELECTION_LIST);
			if (clipboardFormat is 0 || clipboardFormat > ushort.MaxValue)
				return null;

			var format = new FORMATETC
			{
				cfFormat = (ushort)clipboardFormat,
				dwAspect = (uint)DVASPECT.DVASPECT_CONTENT,
				lindex = -1,
				tymed = (uint)TYMED.TYMED_HGLOBAL,
			};
			var medium = new STGMEDIUM { tymed = TYMED.TYMED_HGLOBAL };
			if (selections.GetData(format, out medium).Failed)
				return null;

			var selectionListMemory = PInvoke.GlobalLock(medium.u.hGlobal);
			if (selectionListMemory is null)
			{
				PInvoke.ReleaseStgMedium(ref medium);
				return null;
			}

			try
			{
				var selectionList = (DS_SELECTION_LIST*)selectionListMemory;
				if (selectionList->cItems is 0 || selectionList->cFetchedAttributes is 0)
					return null;

				var selected = selectionList->aDsSelection
					.AsSpan((int)selectionList->cItems);
				fixed (DS_SELECTION_unmanaged* selection = selected)
				{
					return selection->pvarFetchedAttributes is null
						? null
						: GetSidString(selection->pvarFetchedAttributes);
				}
			}
			finally
			{
				PInvoke.GlobalUnlock(medium.u.hGlobal);
				PInvoke.ReleaseStgMedium(ref medium);
			}
		}

		private static unsafe string? GetSidString(ComVariant* objectSidVariant)
		{
			var variantType = objectSidVariant->VarType;
			if ((variantType & VarEnum.VT_ARRAY) is 0
				|| (variantType & (VarEnum)0x0FFF) is not VarEnum.VT_UI1)
			{
				return null;
			}

			ref var rawValue = ref objectSidVariant->GetRawDataRef<nint>();
			var safeArray = (variantType & VarEnum.VT_BYREF) is not 0
				? *(SAFEARRAY**)rawValue
				: (SAFEARRAY*)rawValue;
			if (safeArray is null || PInvoke.SafeArrayGetDim(safeArray) is not 1)
				return null;

			if (PInvoke.SafeArrayGetLBound(safeArray, 1, out var lowerBound).Failed
				|| PInvoke.SafeArrayGetUBound(safeArray, 1, out var upperBound).Failed
				|| upperBound < lowerBound
				|| PInvoke.SafeArrayAccessData(safeArray, out void* sidBytes).Failed
				|| sidBytes is null)
			{
				return null;
			}

			try
			{
				var length = upperBound - lowerBound + 1;
				return new SecurityIdentifier(
					new ReadOnlySpan<byte>(sidBytes, length).ToArray(),
					0).Value;
			}
			finally
			{
				PInvoke.SafeArrayUnaccessData(safeArray);
			}
		}
	}
}
