// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    /// <summary>
    /// This is an undocumented, untested, and unsupported interface.  Do not use.
    /// </summary>
    public sealed unsafe class SOSDac8 : CallableCOMWrapper
    {
        internal static readonly Guid IID_ISOSDac8 = new Guid("c12f35a9-e55c-4520-a894-b3dc5165dfce");

        public SOSDac8(DacLibrary library, IntPtr ptr)
            : base(library?.OwningLibrary, IID_ISOSDac8, ptr)
        {
        }

        private ref readonly ISOSDac8VTable VTable => ref Unsafe.AsRef<ISOSDac8VTable>(_vtable);

        public HResult GetAssemblyLoadContext(ClrDataAddress methodTable, out ClrDataAddress assemblyLoadContext)
        {
            InitDelegate(ref _getAssemblyLoadContext, VTable.GetAssemblyLoadContext);
            return _getAssemblyLoadContext(Self, methodTable, out assemblyLoadContext);
        }

        private DacGetAssemblyLoadContext? _getAssemblyLoadContext;
        private delegate int DacGetAssemblyLoadContext(IntPtr self, ClrDataAddress methodTable, out ClrDataAddress assemblyLoadContext);

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct ISOSDac8VTable
        {
            private readonly IntPtr GetNumberGenerations;
            private readonly IntPtr GetGenerationTable;
            private readonly IntPtr GetFinalizationFillPointers;
            private readonly IntPtr GetGenerationTableSvr;
            private readonly IntPtr GetFinalizationFillPointersSvr;
            public readonly IntPtr GetAssemblyLoadContext;
        }
    }
}
