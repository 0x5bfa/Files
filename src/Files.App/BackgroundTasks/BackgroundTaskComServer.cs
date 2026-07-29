// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.ApplicationModel.Background;
using WinRT;

namespace Files.App.BackgroundTasks;

internal static partial class BackgroundTaskComServer
{
	private const uint ClsctxLocalServer = 4;
	private const uint RegclsMultipleUse = 1;
	private const uint SOk = 0;
	private const uint ClassENoAggregation = 0x80040110;
	private const uint NoInterface = 0x80004002;
	private const string IidIUnknown = "00000000-0000-0000-C000-000000000046";
	private const string IidIClassFactory = "00000001-0000-0000-C000-000000000046";

	private static BackgroundTaskFactory? factory;
	private static uint registrationToken;

	[LibraryImport("ole32.dll")]
	private static partial int CoRegisterClassObject(
		ref Guid classId,
		[MarshalAs(UnmanagedType.Interface)] IClassFactory objectAsUnknown,
		uint executionContext,
		uint flags,
		out uint registrationToken);

	[LibraryImport("ole32.dll")]
	private static partial int CoRevokeClassObject(uint registrationToken);

	[GeneratedComInterface]
	[Guid(IidIClassFactory)]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal partial interface IClassFactory
	{
		[PreserveSig]
		uint CreateInstance(IntPtr objectAsUnknown, in Guid interfaceId, out IntPtr objectPointer);

		[PreserveSig]
		uint LockServer([MarshalAs(UnmanagedType.Bool)] bool lockServer);
	}

	[GeneratedComClass]
	internal sealed partial class BackgroundTaskFactory : IClassFactory
	{
		public uint CreateInstance(IntPtr objectAsUnknown, in Guid interfaceId, out IntPtr objectPointer)
		{
			if (objectAsUnknown != IntPtr.Zero)
			{
				objectPointer = IntPtr.Zero;
				return ClassENoAggregation;
			}

			if (interfaceId != typeof(UpdateTask).GUID && interfaceId != new Guid(IidIUnknown))
			{
				objectPointer = IntPtr.Zero;
				return NoInterface;
			}

			objectPointer = MarshalInterface<IBackgroundTask>.FromManaged(new UpdateTask());
			return SOk;
		}

		public uint LockServer(bool lockServer) => SOk;
	}

	public static void Register()
	{
		if (registrationToken != 0)
			return;

		factory = new BackgroundTaskFactory();
		var classId = typeof(UpdateTask).GUID;
		var hr = CoRegisterClassObject(ref classId, factory, ClsctxLocalServer, RegclsMultipleUse, out registrationToken);
		if (hr != SOk)
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

		_ = CoRevokeClassObject(registrationToken);
		registrationToken = 0;
		factory = null;
	}
}
