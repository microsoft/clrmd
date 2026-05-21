// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{

    /// <summary>
    /// This is an undocumented, untested, and unsupported interface.  Do not use directly.
    /// </summary>
    internal sealed unsafe class SosDac14 : CallableCOMWrapper
    {
        internal static readonly Guid IID_ISOSDac14 = new("9aa22aca-6dc6-4a0c-b4e0-70d2416b9837");

        public SosDac14(DacLibrary library, IntPtr ptr)
            : base(library.OwningLibrary, IID_ISOSDac14, ptr)
        {
        }

        private ref readonly ISOSDac14VTable VTable => ref Unsafe.AsRef<ISOSDac14VTable>(_vtable);

        public (ClrDataAddress NonGCStaticsBase, ClrDataAddress GCStaticsBase) GetStaticBaseAddress(ClrDataAddress methodTable)
        {
            HResult hr = VTable.GetStaticBaseAddress(Self, methodTable.ToInteropAddress(), out ClrDataAddress nonGCStaticsBase, out ClrDataAddress gcStaticsBase);
            return hr ? (nonGCStaticsBase, gcStaticsBase) : (ClrDataAddress.Null, ClrDataAddress.Null);
        }

        public (ClrDataAddress NonGCThreadStaticsBase, ClrDataAddress GCThreadStaticsBase) GetThreadStaticBaseAddress(ClrDataAddress methodTable, ClrDataAddress thread)
        {
            HResult hr = VTable.GetThreadStaticBaseAddress(Self, methodTable.ToInteropAddress(), thread.ToInteropAddress(), out ClrDataAddress nonGCThreadStaticsBase, out ClrDataAddress gcThreadStaticsBase);
            return hr ? (nonGCThreadStaticsBase, gcThreadStaticsBase) : (ClrDataAddress.Null, ClrDataAddress.Null);
        }

        public MethodTableInitializationFlags GetMethodTableInitializationFlags(ClrDataAddress methodTable)
        {
            HResult hr = VTable.GetMethodTableInitializationFlags(Self, methodTable.ToInteropAddress(), out MethodTableInitializationFlags flags);
            return hr ? flags : MethodTableInitializationFlags.Unknown;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly unsafe struct ISOSDac14VTable
        {
            public readonly delegate* unmanaged[Stdcall]<nint, ulong /*ClrDataAddress*/, out ClrDataAddress, out ClrDataAddress, int> GetStaticBaseAddress;
            public readonly delegate* unmanaged[Stdcall]<nint, ulong /*ClrDataAddress*/, ulong /*ClrDataAddress*/, out ClrDataAddress, out ClrDataAddress, int> GetThreadStaticBaseAddress;
            public readonly delegate* unmanaged[Stdcall]<nint, ulong /*ClrDataAddress*/, out MethodTableInitializationFlags, int> GetMethodTableInitializationFlags;
        }
    }

    internal enum MethodTableInitializationFlags : int
    {
        Unknown,
        MethodTableInitialized,
        MethodTableInitializationFailed,
    }
}
