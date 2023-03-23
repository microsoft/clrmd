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
    public sealed unsafe class SOSHandleEnum : CallableCOMWrapper
    {
        private readonly List<GCHandle> _handles = new();

        private static readonly Guid IID_ISOSHandleEnum = new("3E269830-4A2B-4301-8EE2-D6805B29B2FA");

        public SOSHandleEnum(DacLibrary library, IntPtr pUnk)
            : base(library?.OwningLibrary, IID_ISOSHandleEnum, pUnk)
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

        private ref readonly ISOSHandleEnumVTable VTable => ref Unsafe.AsRef<ISOSHandleEnumVTable>(_vtable);

        public IEnumerable<HandleData> ReadHandles()
        {
            HandleData[] handles = new HandleData[0x18000];
            _handles.Add(GCHandle.Alloc(handles, GCHandleType.Pinned));

            List<HandleData> result = new();
            fixed (HandleData* ptr = handles)
            {
                HResult hr = VTable.Next(Self, handles.Length, ptr, out int read);
                if (!hr)
                    return Enumerable.Empty<HandleData>();

                while (hr && read > 0)
                {
                    result.AddRange(handles.Take(read));
                    hr = VTable.Next(Self, handles.Length, ptr, out read);
                }

                return result;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct ISOSHandleEnumVTable
    {
        private readonly IntPtr Skip;
        private readonly IntPtr Reset;
        private readonly IntPtr GetCount;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, int, HandleData*, out int, int> Next;
    }
}