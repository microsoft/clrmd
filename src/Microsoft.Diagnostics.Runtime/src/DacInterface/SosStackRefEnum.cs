// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    public sealed unsafe class SOSStackRefEnum : CallableCOMWrapper
    {
        private static Guid IID_ISOSStackRefEnum = new Guid("8FA642BD-9F10-4799-9AA3-512AE78C77EE");

        private readonly Next _next;

        public SOSStackRefEnum(DacLibrary library, IntPtr pUnk)
            : base(library.OwningLibrary, ref IID_ISOSStackRefEnum, pUnk)
        {
            ISOSStackRefEnumVTable* vtable = (ISOSStackRefEnumVTable*)_vtable;
            InitDelegate(ref _next, vtable->Next);
        }

        public int ReadStackReferences(StackRefData[] stackRefs)
        {
            if (stackRefs == null)
                throw new ArgumentNullException(nameof(stackRefs));

            int hr = _next(Self, stackRefs.Length, stackRefs, out int read);
            return hr >= S_OK ? read : 0;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int Next(
            IntPtr self,
            int count,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            StackRefData[] stackRefs,
            out int pNeeded);
    }

#pragma warning disable CS0169
#pragma warning disable CS0649

    internal struct ISOSStackRefEnumVTable
    {
        private readonly IntPtr Skip;
        private readonly IntPtr Reset;
        private readonly IntPtr GetCount;
        public readonly IntPtr Next;
    }
}