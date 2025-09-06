// Copyright (c) Files Community
// Licensed under the MIT License.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;
using static Windows.Win32.ManualMacros;

namespace Files.App.Storage
{
	/// <summary>
	/// Represents a manager for Windows Jump Lists, allowing interaction with recent and pinned items.
	/// </summary>
	/// <remarks>
	/// See <a href="https://github.com/0x5bfa/JumpListManager/blob/HEAD/JumpListManager/JumpList.cs" />
	/// </remarks>
	public unsafe static class JumpListManager
	{
		public static bool SyncWithExplorerJumpList(int maxCount)
		{
			ClearAutomaticDestinations();
			//ClearCustomDestinations();

			HRESULT hr = default;

			using ComPtr<IAutomaticDestinationList> pExplorerAutoDestList = default;
			hr = PInvoke.CoCreateInstance(CLSID.CLSID_AutomaticDestinationList, null, CLSCTX.CLSCTX_INPROC_SERVER, IID.IID_IAutomaticDestinationList, (void**)pExplorerAutoDestList.GetAddressOf());
			if (FAILED(hr)) return false;

			fixed (char* pwszExplorerAppId = "Microsoft.Windows.Explorer")
				hr = pExplorerAutoDestList.Get()->Initialize(pwszExplorerAppId, default, default);
			if (FAILED(hr)) return false;

			BOOL hasList = false;
			hr = pExplorerAutoDestList.Get()->HasList(&hasList);
			if (hr.Failed || hasList == false) return false;

			// Duplicate recent items

			using ComPtr<IObjectCollection> pRecentItemsObjectCollection = default;
			hr = pExplorerAutoDestList.Get()->GetList(DESTLISTTYPE.RECENT, maxCount, GETDESTLISTFLAGS.NONE, IID.IID_IObjectCollection, (void**)pRecentItemsObjectCollection.GetAddressOf());
			if (FAILED(hr)) return false;

			using ComHeapPtr<char> pwszAppId = default;
			hr = PInvoke.GetCurrentProcessExplicitAppUserModelID((PWSTR*)pwszAppId.GetAddressOf());
			if (FAILED(hr)) return false;

			using ComPtr<IAutomaticDestinationList> pFilesAutoDestList = default;
			hr = PInvoke.CoCreateInstance(CLSID.CLSID_AutomaticDestinationList, null, CLSCTX.CLSCTX_INPROC_SERVER, IID.IID_IAutomaticDestinationList, (void**)pFilesAutoDestList.GetAddressOf());
			if (FAILED(hr)) return false;

			hr = pFilesAutoDestList.Get()->Initialize(pwszAppId.Get(), default, default);
			if (FAILED(hr)) return false;

			uint cRecentItems = 0U;
			hr = pRecentItemsObjectCollection.Get()->GetCount(&cRecentItems);

			for (uint dwIndex = 0U; dwIndex < cRecentItems; dwIndex++)
			{
				using ComPtr<IShellItem> psi = default;
				hr = pRecentItemsObjectCollection.Get()->GetAt(dwIndex, IID.IID_IShellItem, (void**)psi.GetAddressOf());
				if (hr.Failed) return false;

				pFilesAutoDestList.Get()->AddUsagePoint((IUnknown*)psi.Get());
			}

			// Duplicate pinned items

			using ComPtr<IObjectCollection> pPinnedItemsObjectCollection = default;
			hr = pExplorerAutoDestList.Get()->GetList(DESTLISTTYPE.PINNED, maxCount, GETDESTLISTFLAGS.NONE, IID.IID_IObjectCollection, (void**)pPinnedItemsObjectCollection.GetAddressOf());

			uint cPinnedItems = 0U;
			hr = pPinnedItemsObjectCollection.Get()->GetCount(&cPinnedItems);

			for (uint dwIndex = 0U; dwIndex < cRecentItems; dwIndex++)
			{
				using ComPtr<IShellItem> psi = default;
				hr = pPinnedItemsObjectCollection.Get()->GetAt(dwIndex, IID.IID_IShellItem, (void**)psi.GetAddressOf());
				if (hr.Failed) return false;

				pFilesAutoDestList.Get()->PinItem((IUnknown*)psi.Get(), -1);
			}

			return false;
		}

		private static bool ClearAutomaticDestinations()
		{
			HRESULT hr = default;

			using ComHeapPtr<char> pwszAppId = default;
			PInvoke.GetCurrentProcessExplicitAppUserModelID((PWSTR*)pwszAppId.GetAddressOf());

			using ComPtr<IAutomaticDestinationList> pAutomaticDestinationList = default;
			hr = PInvoke.CoCreateInstance(CLSID.CLSID_AutomaticDestinationList, null, CLSCTX.CLSCTX_INPROC_SERVER, IID.IID_IAutomaticDestinationList, (void**)pAutomaticDestinationList.GetAddressOf());
			if (FAILED(hr)) return false;

			hr = pAutomaticDestinationList.Get()->Initialize(pwszAppId.Get(), default, default);
			if (FAILED(hr)) return false;

			hr = pAutomaticDestinationList.Get()->ClearList(true);
			if (FAILED(hr)) return false;

			return true;
		}

		private static bool ClearCustomDestinations()
		{
			using ComHeapPtr<char> pwszAppId = default;
			PInvoke.GetCurrentProcessExplicitAppUserModelID((PWSTR*)pwszAppId.GetAddressOf());

			using ComPtr<IInternalCustomDestinationList> pInternalCustomDestinationList = default;
			HRESULT hr = PInvoke.CoCreateInstance(CLSID.CLSID_DestinationList, null, CLSCTX.CLSCTX_INPROC_SERVER, IID.IID_ICustomDestinationList, (void**)pInternalCustomDestinationList.GetAddressOf());
			if (FAILED(hr)) return false;

			hr = pInternalCustomDestinationList.Get()->SetApplicationID(pwszAppId.Get()).ThrowOnFailure();
			if (FAILED(hr)) return false;

			uint count = 0U;
			pInternalCustomDestinationList.Get()->GetCategoryCount(&count);

			for (uint index = 0U; index < count; index++)
			{
				APPDESTCATEGORY category = default;

				try
				{
					hr = pInternalCustomDestinationList.Get()->GetCategory(index, GETCATFLAG.DEFAULT, &category);
					if (FAILED(hr) || category.Type is not APPDESTCATEGORYTYPE.CUSTOM)
						continue;

					pInternalCustomDestinationList.Get()->DeleteCategory(index, true);
					if (FAILED(hr))
						continue;
				}
				finally
				{
					// The memory layout at Name can be either PWSTR or int depending on the category type
					if (category.Anonymous.Name.Value is not null && category.Type is APPDESTCATEGORYTYPE.CUSTOM) PInvoke.CoTaskMemFree(category.Anonymous.Name);
				}
			}

			// Delete the removed destinations too
			pInternalCustomDestinationList.Get()->ClearRemovedDestinations();

			return false;
		}
	}
}
