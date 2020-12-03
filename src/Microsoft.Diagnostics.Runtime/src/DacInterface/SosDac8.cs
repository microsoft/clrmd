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
        internal static readonly Guid IID_ISOSDac8 = new("c12f35a9-e55c-4520-a894-b3dc5165dfce");

        public SOSDac8(DacLibrary library, IntPtr ptr)
            : base(library?.OwningLibrary, IID_ISOSDac8, ptr)
        {
        }

        private ref readonly ISOSDac8VTable VTable => ref Unsafe.AsRef<ISOSDac8VTable>(_vtable);

        public HResult GetAssemblyLoadContext(ClrDataAddress methodTable, out ClrDataAddress assemblyLoadContext)
        {
            return VTable.GetAssemblyLoadContext(Self, methodTable, out assemblyLoadContext);
        }

        public int GenerationCount
        {
            get
            {
                InitDelegate(ref _getNumberGenerations, VTable.GetNumberGenerations);

                if (_getNumberGenerations(Self, out int generations))
                    return generations;

                return 0;
            }
        }

        public GenerationData[]? GetGenerationTable()
        {
            if (!VTable.GetGenerationTable(Self, 0, null, out int needed))
                return null;

            GenerationData[] data = new GenerationData[needed];
            fixed (GenerationData* ptr = data)
                if (!VTable.GetGenerationTable(Self, needed, ptr, out _))
                    return null;

            return data;
        }

        public GenerationData[]? GetGenerationTable(ulong heap)
        {
            if (!VTable.GetGenerationTableSvr(Self, heap, 0, null, out int needed))
                return null;

            GenerationData[] data = new GenerationData[needed];
            fixed (GenerationData* ptr = data)
                if (!VTable.GetGenerationTableSvr(Self, heap, needed, ptr, out _))
                    return null;

            return data;
        }

        public ClrDataAddress[]? GetFinalizationFillPointers()
        {
            if (!VTable.GetFinalizationFillPointers(Self, 0, null, out int needed))
                return null;

            ClrDataAddress[] pointers = new ClrDataAddress[needed];
            fixed (ClrDataAddress* ptr = pointers)
                if (!VTable.GetFinalizationFillPointers(Self, needed, ptr, out _))
                    return null;

            return pointers;
        }

        public ClrDataAddress[]? GetFinalizationFillPointers(ulong heap)
        {
            if (!VTable.GetFinalizationFillPointersSvr(Self, heap, 0, null, out int needed))
                return null;

            ClrDataAddress[] pointers = new ClrDataAddress[needed];
            fixed (ClrDataAddress* ptr = pointers)
                if (!VTable.GetFinalizationFillPointersSvr(Self, heap, needed, ptr, out _))
                    return null;

            return pointers;
        }
        private GetNumberGenerationsDelegate? _getNumberGenerations;
        private delegate HResult GetNumberGenerationsDelegate(IntPtr self, out int generations);

        [StructLayout(LayoutKind.Sequential)]
        private readonly unsafe struct ISOSDac8VTable
        {
            public readonly IntPtr GetNumberGenerations;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int, GenerationData*, out int, HResult> GetGenerationTable;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int, ClrDataAddress*, out int, HResult> GetFinalizationFillPointers;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong, int, GenerationData*, out int, HResult> GetGenerationTableSvr;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong, int, ClrDataAddress*, out int, HResult> GetFinalizationFillPointersSvr;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, ClrDataAddress, out ClrDataAddress, HResult> GetAssemblyLoadContext;
        }
    }
}
