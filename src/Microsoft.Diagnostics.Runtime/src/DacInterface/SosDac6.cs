// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    /// <summary>
    /// This is an undocumented, untested, and unsupported interface.  Do not use.
    /// </summary>
    public sealed unsafe class SOSDac6 : CallableCOMWrapper
    {
        internal static Guid IID_ISOSDac6 = new Guid("11206399-4B66-4EDB-98EA-85654E59AD45");
        private ISOSDac6VTable* VTable => (ISOSDac6VTable*)_vtable;

        public SOSDac6(DacLibrary library, IntPtr ptr)
            : base(library?.OwningLibrary, ref IID_ISOSDac6, ptr)
        {
        }

        public SOSDac6(CallableCOMWrapper toClone) : base(toClone)
        {
        }

        private DacGetMethodTableCollectibleData _getMethodTableCollectibleData;

        public bool GetMethodTableCollectibleData(ulong mt, out MethodTableCollectibleData data)
        {
            InitDelegate(ref _getMethodTableCollectibleData, VTable->GetMethodTableCollectibleData);
            int hr = _getMethodTableCollectibleData(Self, mt, out data);
            return SUCCEEDED(hr);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DacGetMethodTableCollectibleData(IntPtr self, ulong addr, out MethodTableCollectibleData data);
    }

    internal struct ISOSDac6VTable
    {
        public readonly IntPtr GetMethodTableCollectibleData;
    }
}
