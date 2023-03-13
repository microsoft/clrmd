// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    public sealed unsafe class SOSStackRefEnum : CallableCOMWrapper
    {
        private static readonly Guid IID_ISOSStackRefEnum = new("8FA642BD-9F10-4799-9AA3-512AE78C77EE");

        public SOSStackRefEnum(DacLibrary library, IntPtr pUnk)
            : base(library?.OwningLibrary, IID_ISOSStackRefEnum, pUnk)
        {
        }

        private ref readonly ISOSStackRefEnumVTable VTable => ref Unsafe.AsRef<ISOSStackRefEnumVTable>(_vtable);

        public int ReadStackReferences(Span<StackRefData> stackRefs)
        {
            if (stackRefs.IsEmpty)
                throw new ArgumentException(null, nameof(stackRefs));

            fixed (StackRefData* ptr = stackRefs)
            {
                HResult hr = VTable.Next(Self, stackRefs.Length, ptr, out int read);
                return hr ? read : 0;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct ISOSStackRefEnumVTable
    {
        private readonly IntPtr Skip;
        private readonly IntPtr Reset;
        private readonly IntPtr GetCount;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, int, StackRefData*, out int, int> Next;
    }
}