// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    /// <summary>
    /// This is an undocumented, untested, and unsupported interface.  Do not use.
    /// </summary>
    internal sealed unsafe class SosDac9 : CallableCOMWrapper
    {
        internal static readonly Guid IID_ISOSDac9 = new("4eca42d8-7e7b-4c8a-a116-7bfbf6929267");

        public SosDac9(DacLibrary library, IntPtr ptr)
            : base(library?.OwningLibrary, IID_ISOSDac9, ptr)
        {
        }

        private ref readonly ISOSDac9VTable VTable => ref Unsafe.AsRef<ISOSDac9VTable>(_vtable);

        public int GetBreakingChangeVersion()
        {
            HResult hr = VTable.GetBreakingChangeVersion(Self, out int version);
            return hr ? version : 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct ISOSDac9VTable
    {
        public readonly delegate* unmanaged[Stdcall]<IntPtr, out int, int> GetBreakingChangeVersion;
    }
}
