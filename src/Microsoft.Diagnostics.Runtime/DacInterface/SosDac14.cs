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
        private readonly DacLibrary _library;

        internal static readonly Guid IID_ISOSDac14 = new("9aa22aca-6dc6-4a0c-b4e0-70d2416b9837");

        public SosDac14(DacLibrary library, IntPtr ptr)
            : base(library?.OwningLibrary, IID_ISOSDac14, ptr)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
        }

        private ref readonly ISOSDac14VTable VTable => ref Unsafe.AsRef<ISOSDac14VTable>(_vtable);

        public (ulong NonGCStaticsBase, ulong GCStaticsBase) GetStaticBaseAddress(ClrDataAddress methodTable)
        {
            HResult hr = VTable.GetStaticBaseAddress(Self, methodTable, out ClrDataAddress nonGCStaticsBase, out ClrDataAddress gcStaticsBase);
            return hr ? (nonGCStaticsBase, gcStaticsBase) : (0, 0);
        }

        public (ulong NonGCThreadStaticsBase, ulong GCThreadStaticsBase) GetThreadStaticBaseAddress(ClrDataAddress methodTable, ClrDataAddress thread)
        {
            HResult hr = VTable.GetThreadStaticBaseAddress(Self, methodTable, thread, out ClrDataAddress nonGCThreadStaticsBase, out ClrDataAddress gcThreadStaticsBase);
            return hr ? (nonGCThreadStaticsBase, gcThreadStaticsBase) : (0, 0);
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly unsafe struct ISOSDac14VTable
        {
            public readonly delegate* unmanaged[Stdcall]<nint, ClrDataAddress, out ClrDataAddress, out ClrDataAddress, int> GetStaticBaseAddress;
            public readonly delegate* unmanaged[Stdcall]<nint, ClrDataAddress, ClrDataAddress, out ClrDataAddress, out ClrDataAddress, int> GetThreadStaticBaseAddress;
            public readonly delegate* unmanaged[Stdcall]<nint, ClrDataAddress, out MethodTableInitializationFlags, int> GetMethodTableInitializationFlags;
        }
    }

    internal enum MethodTableInitializationFlags : int
    {
        Unknown,
        MethodTableInitialized,
        MethodTableInitializationFailed,
    }
}
