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
            InitDelegate(ref _getAssemblyLoadContext, VTable.GetAssemblyLoadContext);
            return _getAssemblyLoadContext(Self, methodTable, out assemblyLoadContext);
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
            InitDelegate(ref _getGenerationTable, VTable.GetGenerationTable);

            if (!_getGenerationTable(Self, 0, null, out int needed))
                return null;

            GenerationData[] data = new GenerationData[needed];
            fixed (GenerationData* ptr = data)
                if (!_getGenerationTable(Self, needed, ptr, out _))
                    return null;

            return data;
        }

        public GenerationData[]? GetGenerationTable(ulong heap)
        {
            InitDelegate(ref _getGenerationTableSvr, VTable.GetGenerationTableSvr);

            if (!_getGenerationTableSvr(Self, heap, 0, null, out int needed))
                return null;

            GenerationData[] data = new GenerationData[needed];
            fixed (GenerationData* ptr = data)
                if (!_getGenerationTableSvr(Self, heap, needed, ptr, out _))
                    return null;

            return data;
        }

        public ClrDataAddress[]? GetFinalizationFillPointers()
        {
            InitDelegate(ref _getFinalizationPointers, VTable.GetFinalizationFillPointers);

            if (!_getFinalizationPointers(Self, 0, null, out int needed))
                return null;

            ClrDataAddress[] pointers = new ClrDataAddress[needed];
            fixed (ClrDataAddress* ptr = pointers)
                if (!_getFinalizationPointers(Self, needed, ptr, out _))
                    return null;

            return pointers;
        }

        public ClrDataAddress[]? GetFinalizationFillPointers(ulong heap)
        {
            InitDelegate(ref _getFinalizationPointersSvr, VTable.GetFinalizationFillPointersSvr);

            if (!_getFinalizationPointersSvr(Self, heap, 0, null, out int needed))
                return null;

            ClrDataAddress[] pointers = new ClrDataAddress[needed];
            fixed (ClrDataAddress* ptr = pointers)
                if (!_getFinalizationPointersSvr(Self, heap, needed, ptr, out _))
                    return null;

            return pointers;
        }

        private GetAssemblyLoadContextDelegate? _getAssemblyLoadContext;
        private GetNumberGenerationsDelegate? _getNumberGenerations;
        private GetGenerationTableDelegate? _getGenerationTable;
        private GetGenerationTableSvrDelegate? _getGenerationTableSvr;
        private GetFinalizationFillPointersDelegate? _getFinalizationPointers;
        private GetFinalizationFillPointersSvrDelegate? _getFinalizationPointersSvr;

        private delegate HResult GetAssemblyLoadContextDelegate(IntPtr self, ClrDataAddress methodTable, out ClrDataAddress assemblyLoadContext);
        private delegate HResult GetNumberGenerationsDelegate(IntPtr self, out int generations);
        
        // WKS
        private delegate HResult GetGenerationTableDelegate(IntPtr self, int cGenerations, GenerationData *pGenerationData, out int pNeeded);
        private delegate HResult GetFinalizationFillPointersDelegate(IntPtr self, int cFillPointers, ClrDataAddress* pFinalizationFillPointers, out int pNeeded);

        // SVR
        private delegate HResult GetGenerationTableSvrDelegate(IntPtr self, ulong heapAddr, int cGenerations, GenerationData *pGenerationData, out int pNeeded);
        private delegate HResult GetFinalizationFillPointersSvrDelegate(IntPtr self, ulong heapAddr, int cFillPointers, ClrDataAddress* pFinalizationFillPointers, out int pNeeded);

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct ISOSDac8VTable
        {
            public readonly IntPtr GetNumberGenerations;
            public readonly IntPtr GetGenerationTable;
            public readonly IntPtr GetFinalizationFillPointers;
            public readonly IntPtr GetGenerationTableSvr;
            public readonly IntPtr GetFinalizationFillPointersSvr;
            public readonly IntPtr GetAssemblyLoadContext;
        }
    }
}
