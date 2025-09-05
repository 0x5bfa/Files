// Copyright (c) Files Community
// Licensed under the MIT License.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using static Windows.Win32.ManualMacros;

namespace Files.App.Storage
{
	/// <summary>
	/// Represents a manager for Windows Jump Lists, allowing interaction with recent and pinned items.
	/// </summary>
	/// <remarks>
	/// See <a href="https://github.com/0x5bfa/JumpListManager/blob/HEAD/JumpListManager/JumpList.cs" />
	/// </remarks>
	public unsafe class JumpListManager : IDisposable
	{
		private IAutomaticDestinationList* _pAutomaticDestinationList;
		private IInternalCustomDestinationList* _pInternalCustomDestinationList;
		private ICustomDestinationList* _pCustomDestinationList;

		public required string AppId { get; init; }

		public static JumpListManager? Create(string appId)
		{
			if (string.IsNullOrEmpty(appId))
				throw new ArgumentException("AMUID cannot be null or empty.", nameof(appId));

			IAutomaticDestinationList* pAutomaticDestinationList = default;
			IInternalCustomDestinationList* pInternalCustomDestinationList = default;
			ICustomDestinationList* pCustomDestinationList = default;

			HRESULT hr = default;

			hr = PInvoke.CoCreateInstance(CLSID.CLSID_AutomaticDestinationList, null, CLSCTX.CLSCTX_INPROC_SERVER, IID.IID_IAutomaticDestinationList, (void**)&pAutomaticDestinationList);
			if (FAILED(hr)) return null;

			hr = PInvoke.CoCreateInstance(CLSID.CLSID_DestinationList, null, CLSCTX.CLSCTX_INPROC_SERVER, IID.IID_ICustomDestinationList, (void**)&pInternalCustomDestinationList);
			if (FAILED(hr)) return null;

			hr = PInvoke.CoCreateInstance(CLSID.CLSID_DestinationList, null, CLSCTX.CLSCTX_INPROC_SERVER, IID.IID_IInternalCustomDestinationList, (void**)&pCustomDestinationList);
			if (FAILED(hr)) return null;

			// These internally convert the passed AMUID string to the corresponding CRC hash and initialize the path to the destination lists with FOLDERID_Recent.
			fixed (char* pwszAppId = appId)
			{
				hr = pAutomaticDestinationList->Initialize(pwszAppId, default, default).ThrowOnFailure();
				if (FAILED(hr)) return null;

				hr = pInternalCustomDestinationList->SetApplicationID(pwszAppId).ThrowOnFailure();
				if (FAILED(hr)) return null;

				hr = pCustomDestinationList->SetAppID(pwszAppId).ThrowOnFailure();
				if (FAILED(hr)) return null;
			}

			return new() { AppId = appId, _pAutomaticDestinationList = pAutomaticDestinationList, _pInternalCustomDestinationList = pInternalCustomDestinationList, _pCustomDestinationList = pCustomDestinationList };
		}

		public bool ClearCustomDestinations()
		{
			uint count = 0U;
			HRESULT hr = _pInternalCustomDestinationList->GetCategoryCount(&count);

			for (uint index = 0U; index < count; index++)
			{
				APPDESTCATEGORY category = default;

				try
				{
					hr = _pInternalCustomDestinationList->GetCategory(index, GETCATFLAG.DEFAULT, &category);
					if (FAILED(hr) || category.Type is not APPDESTCATEGORYTYPE.CUSTOM)
						continue;

					_pInternalCustomDestinationList->DeleteCategory(index, true);
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
			_pInternalCustomDestinationList->ClearRemovedDestinations();

			return false;
		}

		public IEnumerable<JumpListItem> GetRecentItems(int maxCount)
		{
			return [];
		}

		public bool ClearAndAddRecentItems(IEnumerable<JumpListItem> newItems)
		{
			_pAutomaticDestinationList->ClearList(true);

			return false;
		}

		public IEnumerable<JumpListItem> GetPinnedItems(int maxCount)
		{
			return [];
		}

		public bool ClearAndAddPinnedItems(IEnumerable<JumpListItem> newItems)
		{
			return false;
		}

		public void Dispose()
		{
		}
	}
}
