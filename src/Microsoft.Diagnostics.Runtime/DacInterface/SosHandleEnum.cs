// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    public sealed unsafe class SOSHandleEnum : CallableCOMWrapper
    {
        private static readonly Guid IID_ISOSHandleEnum = new("3E269830-4A2B-4301-8EE2-D6805B29B2FA");

        public SOSHandleEnum(DacLibrary library, IntPtr pUnk)
            : base(library?.OwningLibrary, IID_ISOSHandleEnum, pUnk)
        {
        }

        private ref readonly ISOSHandleEnumVTable VTable => ref Unsafe.AsRef<ISOSHandleEnumVTable>(_vtable);

        public int ReadHandles(Span<HandleData> handles)
        {
            if (handles.IsEmpty)
                throw new ArgumentException(null, nameof(handles));

            fixed (HandleData* ptr = handles)
            {
                HResult hr = VTable.Next(Self, handles.Length, ptr, out int read);
                return hr ? read : 0;
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