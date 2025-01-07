// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;
using static Microsoft.Diagnostics.Runtime.DacInterface.SOSDac;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    /// <summary>
    /// This is an undocumented, untested, and unsupported interface.  Do not use directly.
    /// </summary>
    internal sealed unsafe class SOSDac16 : CallableCOMWrapper, ISOSDac16
    {
        private readonly DacLibrary _library;

        internal static readonly Guid IID_ISOSDac16 = new("4ba12ff8-daac-4e43-ac56-98cf8d5c595d");

        public SOSDac16(DacLibrary library, IntPtr ptr)
            : base(library?.OwningLibrary, IID_ISOSDac16, ptr)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
        }

        private ref readonly ISOSDac16VTable VTable => ref Unsafe.AsRef<ISOSDac16VTable>(_vtable);

        public int? GetDynamicAdaptationMode()
        {
            HResult hr = VTable.GetDynamicAdaptationMode(Self, out int result);
            return (hr == HResult.S_OK) ? result : null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly unsafe struct ISOSDac16VTable
        {
            public readonly delegate* unmanaged[Stdcall]<nint, out int, int> GetDynamicAdaptationMode;
        }
    }
}