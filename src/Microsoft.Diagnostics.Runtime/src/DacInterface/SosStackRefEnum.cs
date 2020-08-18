// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    public sealed unsafe class SOSStackRefEnum : CallableCOMWrapper
    {
        private static readonly Guid IID_ISOSStackRefEnum = new Guid("8FA642BD-9F10-4799-9AA3-512AE78C77EE");


        public SOSStackRefEnum(DacLibrary library, IntPtr pUnk)
            : base(library?.OwningLibrary, IID_ISOSStackRefEnum, pUnk)
        {
            ref readonly ISOSStackRefEnumVTable vtable = ref Unsafe.AsRef<ISOSStackRefEnumVTable>(_vtable);
            InitDelegate(ref _next, vtable.Next);
        }

        public int ReadStackReferences(Span<StackRefData> stackRefs)
        {
            if (stackRefs.IsEmpty)
                throw new ArgumentException(null, nameof(stackRefs));

            fixed (StackRefData* ptr = stackRefs)
            {
                HResult hr = _next(Self, stackRefs.Length, ptr, out int read);
                return hr ? read : 0;
            }
        }

        private readonly Next _next;
        private delegate int Next(IntPtr self, int count, StackRefData* stackRefs, out int pNeeded);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct ISOSStackRefEnumVTable
    {
        private readonly IntPtr Skip;
        private readonly IntPtr Reset;
        private readonly IntPtr GetCount;
        public readonly IntPtr Next;
    }
}