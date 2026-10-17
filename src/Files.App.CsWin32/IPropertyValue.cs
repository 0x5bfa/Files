// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

#pragma warning disable IDE0130 // Native declarations preserve Windows SDK namespaces.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Win32.Foundation;

namespace Windows.Win32.System.WinRT;

/// <summary>Reads string and GUID values from an Explorer extension property set.</summary>
[GeneratedComInterface(Options = ComInterfaceOptions.ComObjectWrapper)]
[Guid("4BD682DD-7554-40E9-9A9B-82654EDE7E62")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public partial interface IPropertyValue : IInspectable
{
	[PreserveSig] HRESULT GetPropertyType(out int value);
	[PreserveSig] HRESULT GetIsNumericScalar(out byte value);
	[PreserveSig] HRESULT GetUInt8(out byte value);
	[PreserveSig] HRESULT GetInt16(out short value);
	[PreserveSig] HRESULT GetUInt16(out ushort value);
	[PreserveSig] HRESULT GetInt32(out int value);
	[PreserveSig] HRESULT GetUInt32(out uint value);
	[PreserveSig] HRESULT GetInt64(out long value);
	[PreserveSig] HRESULT GetUInt64(out ulong value);
	[PreserveSig] HRESULT GetSingle(out float value);
	[PreserveSig] HRESULT GetDouble(out double value);
	[PreserveSig] HRESULT GetChar16(out ushort value);
	[PreserveSig] HRESULT GetBoolean(out byte value);
	[PreserveSig] HRESULT GetString([MarshalUsing(typeof(global::Windows.Win32.HStringStringMarshaller))] out string? value);
	[PreserveSig] HRESULT GetGuid(out Guid value);
}
