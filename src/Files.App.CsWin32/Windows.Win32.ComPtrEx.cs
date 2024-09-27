// Copyright (c) 2024 Files Community
// Licensed under the MIT License. See the LICENSE.

using System;
using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.System.Com;

namespace Windows.Win32
{
	/// <summary>
	/// Contains a COM pointer and a set of methods to work with the pointer safely.
	/// </summary>
	public unsafe class ComPtrEx<T> : IDynamicInterfaceCastable, IDisposable where T : unmanaged, IComIID
	{
		private T* _ptr;

		public bool IsNull
			=> _ptr == default;

		public ComPtrEx(T* ptr)
		{
			_ptr = ptr;
			if (ptr is not null)
				((IUnknown*)ptr)->AddRef();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T* Get()
		{
			return _ptr;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T** GetAddressOf()
		{
			return (T**)Unsafe.AsPointer(ref Unsafe.AsRef(in this));
		}

		public Guid* GetUuidOf()
		{
			return (Guid*)Unsafe.AsPointer(ref Unsafe.AsRef(in _ptr->Guid));
		}

		public bool IsInterfaceImplemented(RuntimeTypeHandle interfaceType, bool throwIfNotImplemented)
        {
			return false;
        }

		public RuntimeTypeHandle GetInterfaceImplementation(RuntimeTypeHandle interfaceType)
        {
            return RuntimeTypeHandle.FromIntPtr(0);
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Dispose()
		{
			T* ptr = _ptr;
			if (ptr is not null)
			{
				_ptr = null;
				((IUnknown*)ptr)->Release();
			}
		}
	}
}
