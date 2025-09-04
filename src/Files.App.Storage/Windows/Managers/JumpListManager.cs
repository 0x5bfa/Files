// Copyright (c) Files Community
// Licensed under the MIT License.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using static Windows.Win32.ManualMacros;

namespace Files.App.Storage
{
	public unsafe class JumpListManager : IDisposable
	{
		private void* _pAutomaticDestinationList;

		public required string AppId { get; init; }

		public static JumpListManager? Create(string appId)
		{
			if (string.IsNullOrEmpty(appId))
				throw new ArgumentException("AMUID cannot be null or empty.", nameof(appId));

			IAutomaticDestinationList* pAutoDestList = default;

			HRESULT hr = default;

			hr = PInvoke.CoCreateInstance(CLSID.CLSID_AutomaticDestinationList, null, CLSCTX.CLSCTX_INPROC_SERVER, IID.IID_IAutomaticDestinationList, (void**)&pAutoDestList).ThrowOnFailure();
			if (FAILED(hr))
				return null;

			// These internally calculate CRC hash from the passed AMUID string and initialize the path to the destination lists with it in FOLDERID_Recent.
			fixed (char* pwszAppId = appId)
			{
				hr = pAutoDestList->Initialize(pwszAppId, default, default).ThrowOnFailure();
				if (FAILED(hr))
					return null;
			}

			return new() { AppId = appId, _pAutomaticDestinationList = pAutoDestList };
		}

		public void Dispose()
		{
		}
	}
}
