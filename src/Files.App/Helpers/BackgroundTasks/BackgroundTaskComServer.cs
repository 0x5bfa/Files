// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.ApplicationModel.Background;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using WinRT;

namespace Files.App.Helpers.BackgroundTasks;

internal static partial class BackgroundTaskComServer
{
	private static BackgroundTaskFactory? factory;
	private static uint registrationToken;

	[GeneratedComClass]
	internal sealed partial class BackgroundTaskFactory : IClassFactory
	{
		[return: MarshalAs(UnmanagedType.Error)]
		public unsafe HRESULT CreateInstance([MarshalAs(UnmanagedType.Interface), Optional] object pUnkOuter, Guid* riid, out void* ppvObject)
		{
			if (pUnkOuter != null)
			{
				ppvObject = null;
				return HRESULT.CLASS_E_NOAGGREGATION;
			}

			if (*riid != typeof(UpdateTask).GUID && *riid != typeof(IUnknown).GUID)
			{
				ppvObject = null;
				return HRESULT.E_NOINTERFACE;
			}

			ppvObject = (void*)MarshalInterface<IBackgroundTask>.FromManaged(new UpdateTask());
			return HRESULT.S_OK;
		}

		[return: MarshalAs(UnmanagedType.Error)]
		public HRESULT LockServer(BOOL fLock)
		{
			return HRESULT.S_OK;
		}
	}

	public static void Register()
	{
		if (registrationToken != 0)
			return;

		factory = new BackgroundTaskFactory();
		var hr = PInvoke.CoRegisterClassObject(typeof(UpdateTask).GUID, factory, CLSCTX.CLSCTX_LOCAL_SERVER, REGCLS.REGCLS_MULTIPLEUSE, out registrationToken);
		if (hr != HRESULT.S_OK)
		{
			registrationToken = 0;
			factory = null;
			Marshal.ThrowExceptionForHR(hr);
		}

		AppDomain.CurrentDomain.ProcessExit += (_, _) => Unregister();
	}

	private static void Unregister()
	{
		if (registrationToken == 0)
			return;

		_ = PInvoke.CoRevokeClassObject(registrationToken);
		registrationToken = 0;
		factory = null;
	}
}
