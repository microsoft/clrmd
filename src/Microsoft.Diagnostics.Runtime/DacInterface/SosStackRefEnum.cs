// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    public sealed unsafe class SOSStackRefEnum : CallableCOMWrapper
    {
        private readonly List<GCHandle> _handles = new();
        private static readonly Guid IID_ISOSStackRefEnum = new("8FA642BD-9F10-4799-9AA3-512AE78C77EE");

        public SOSStackRefEnum(DacLibrary library, IntPtr pUnk)
            : base(library?.OwningLibrary, IID_ISOSStackRefEnum, pUnk)
        {
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            foreach (GCHandle handle in _handles)
            {
                handle.Free();
            }

            _handles.Clear();
        }

        private ref readonly ISOSStackRefEnumVTable VTable => ref Unsafe.AsRef<ISOSStackRefEnumVTable>(_vtable);

        public IEnumerable<StackRefData> GetStackRefs()
        {
            StackRefData[] data = new StackRefData[0x1000];
            _handles.Add(GCHandle.Alloc(data, GCHandleType.Pinned));

            List<StackRefData> result = new();
            fixed (StackRefData* ptr = data)
            {
                HResult hr = VTable.Next(Self, data.Length, ptr, out int read);
                while (hr && read > 0)
                {
                    result.AddRange(data.Take(read));
                    hr = VTable.Next(Self, data.Length, ptr, out read);
                }
            }

            return result;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct ISOSStackRefEnumVTable
    {
        private readonly IntPtr Skip;
        private readonly IntPtr Reset;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, out int, int> GetCount;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, int, StackRefData*, out int, int> Next;
    }
}