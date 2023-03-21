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

        public StackRefData[] GetStackRefs()
        {
            HResult hr = VTable.GetCount(Self, out int count);
            if (!hr)
                return Array.Empty<StackRefData>();

            StackRefData[] refs;
            try
            {
                // We seem to be getting some weird results from StackRefEnum, ensure we have more than
                // needed.
                refs = new StackRefData[(int)(count * 1.5)];
            }
            catch (OutOfMemoryException)
            {
                return Array.Empty<StackRefData>();
            }

            int read = 0;
            fixed (StackRefData *ptr = refs)
                hr = VTable.Next(Self, refs.Length, ptr, out read);

            if (!hr)
            {
                return Array.Empty<StackRefData>();
            }

            if (read != refs.Length)
                Array.Resize(ref refs, read);

            return refs;
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