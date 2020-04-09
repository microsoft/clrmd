// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DbgEng
{
    internal unsafe sealed class DebugDataSpaces : CallableCOMWrapper
    {
        internal static readonly Guid IID_IDebugDataSpaces2 = new Guid("7a5e852f-96e9-468f-ac1b-0b3addc4a049");

        public DebugDataSpaces(RefCountedFreeLibrary library, IntPtr pUnk, DebugSystemObjects sys)
            : base(library, IID_IDebugDataSpaces2, pUnk)
        {
            _sys = sys;
            SuppressRelease();
        }

        private ref readonly IDebugDataSpacesVTable VTable => ref Unsafe.AsRef<IDebugDataSpacesVTable>(_vtable);

        public int ReadVirtual(ulong address, Span<byte> buffer)
        {
            InitDelegate(ref _readVirtual, VTable.ReadVirtual);
            using IDisposable holder = _sys.Enter();
            fixed (byte* ptr = buffer)
            {
                HResult hr = _readVirtual(Self, address, ptr, buffer.Length, out int read);
                return read;
            }
        }

        public HResult QueryVirtual(ulong address, out MEMORY_BASIC_INFORMATION64 info)
        {
            InitDelegate(ref _queryVirtual, VTable.QueryVirtual);
            using IDisposable holder = _sys.Enter();
            return _queryVirtual(Self, address, out info);
        }

        private ReadVirtualDelegate? _readVirtual;
        private QueryVirtualDelegate? _queryVirtual;
        private readonly DebugSystemObjects _sys;

        private delegate HResult ReadVirtualDelegate(IntPtr self, ulong address, byte* buffer, int size, out int read);
        private delegate HResult QueryVirtualDelegate(IntPtr self, ulong address, out MEMORY_BASIC_INFORMATION64 info);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct IDebugDataSpacesVTable
    {
        public readonly IntPtr ReadVirtual;
        public readonly IntPtr WriteVirtual;
        public readonly IntPtr SearchVirtual;
        public readonly IntPtr ReadVirtualUncached;
        public readonly IntPtr WriteVirtualUncached;
        public readonly IntPtr ReadPointersVirtual;
        public readonly IntPtr WritePointersVirtual;
        public readonly IntPtr ReadPhysical;
        public readonly IntPtr WritePhysical;
        public readonly IntPtr ReadControl;
        public readonly IntPtr WriteControl;
        public readonly IntPtr ReadIo;
        public readonly IntPtr WriteIo;
        public readonly IntPtr ReadMsr;
        public readonly IntPtr WriteMsr;
        public readonly IntPtr ReadBusData;
        public readonly IntPtr WriteBusData;
        public readonly IntPtr CheckLowMemory;
        public readonly IntPtr ReadDebuggerData;
        public readonly IntPtr ReadProcessorSystemData;
        public readonly IntPtr VirtualToPhysical;
        public readonly IntPtr GetVirtualTranslationPhysicalOffsets;
        public readonly IntPtr ReadHandleData;
        public readonly IntPtr FillVirtual;
        public readonly IntPtr FillPhysical;
        public readonly IntPtr QueryVirtual;
    }
}