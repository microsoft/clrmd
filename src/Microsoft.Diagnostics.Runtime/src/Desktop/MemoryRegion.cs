// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;

#pragma warning disable 649

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class MemoryRegion : ClrMemoryRegion
    {
        private readonly DesktopRuntimeBase _runtime;
        private ulong _domainModuleHeap;
        private GCSegmentType _segmentType;

        private bool HasAppDomainData => Type <= ClrMemoryRegionType.CacheEntryHeap || Type == ClrMemoryRegionType.HandleTableChunk;

        private bool HasModuleData => Type == ClrMemoryRegionType.ModuleThunkHeap || Type == ClrMemoryRegionType.ModuleLookupTableHeap;

        private bool HasGCHeapData => Type == ClrMemoryRegionType.GCSegment || Type == ClrMemoryRegionType.ReservedGCSegment;

        public override ClrAppDomain AppDomain
        {
            get
            {
                if (!HasAppDomainData)
                    return null;

                return _runtime.GetAppDomainByAddress(_domainModuleHeap);
            }
        }

        public override string Module
        {
            get
            {
                if (!HasModuleData)
                    return null;

                return _runtime.GetModule(_domainModuleHeap).FileName;
            }
        }

        public override int HeapNumber
        {
            get
            {
                if (!HasGCHeapData)
                    return -1;

                Debug.Assert(_domainModuleHeap < uint.MaxValue);
                return (int)_domainModuleHeap;
            }
            set => _domainModuleHeap = (ulong)value;
        }

        public override GCSegmentType GCSegmentType
        {
            get
            {
                if (!HasGCHeapData)
                    throw new NotSupportedException();

                return _segmentType;
            }
            set => _segmentType = value;
        }

        public override string ToString(bool detailed)
        {
            string value = null;

            switch (Type)
            {
                case ClrMemoryRegionType.LowFrequencyLoaderHeap:
                    value = "Low Frequency Loader Heap";
                    break;

                case ClrMemoryRegionType.HighFrequencyLoaderHeap:
                    value = "High Frequency Loader Heap";
                    break;

                case ClrMemoryRegionType.StubHeap:
                    value = "Stub Heap";
                    break;

                // Virtual Call Stub heaps
                case ClrMemoryRegionType.IndcellHeap:
                    value = "Indirection Cell Heap";
                    break;

                case ClrMemoryRegionType.LookupHeap:
                    value = "Loopup Heap";
                    break;

                case ClrMemoryRegionType.ResolveHeap:
                    value = "Resolver Heap";
                    break;

                case ClrMemoryRegionType.DispatchHeap:
                    value = "Dispatch Heap";
                    break;

                case ClrMemoryRegionType.CacheEntryHeap:
                    value = "Cache Entry Heap";
                    break;

                // Other regions
                case ClrMemoryRegionType.JitHostCodeHeap:
                    value = "JIT Host Code Heap";
                    break;

                case ClrMemoryRegionType.JitLoaderCodeHeap:
                    value = "JIT Loader Code Heap";
                    break;

                case ClrMemoryRegionType.ModuleThunkHeap:
                    value = "Thunk Heap";
                    break;

                case ClrMemoryRegionType.ModuleLookupTableHeap:
                    value = "Lookup Table Heap";
                    break;

                case ClrMemoryRegionType.HandleTableChunk:
                    value = "GC Handle Table Chunk";
                    break;

                case ClrMemoryRegionType.ReservedGCSegment:
                case ClrMemoryRegionType.GCSegment:
                    if (_segmentType == GCSegmentType.Ephemeral)
                        value = "Ephemeral Segment";
                    else if (_segmentType == GCSegmentType.LargeObject)
                        value = "Large Object Segment";
                    else
                        value = "GC Segment";

                    if (Type == ClrMemoryRegionType.ReservedGCSegment)
                        value += " (Reserved)";
                    break;

                default:
                    // should never happen.
                    value = "<unknown>";
                    break;
            }

            if (detailed)
            {
                if (HasAppDomainData)
                {
                    if (_runtime.SharedDomain != null && _domainModuleHeap == _runtime.SharedDomain.Address)
                    {
                        value = $"{value} for Shared AppDomain";
                    }
                    else if (_runtime.SystemDomain != null && _domainModuleHeap == _runtime.SystemDomain.Address)
                    {
                        value = $"{value} for System AppDomain";
                    }
                    else
                    {
                        ClrAppDomain domain = AppDomain;
                        value = $"{value} for AppDomain {domain.Id}: {domain.Name}";
                    }
                }
                else if (HasModuleData)
                {
                    string fn = _runtime.GetModule(_domainModuleHeap).FileName;
                    value = $"{value} for Module: {Path.GetFileName(fn)}";
                }
                else if (HasGCHeapData)
                {
                    value = $"{value} for Heap {HeapNumber}";
                }
            }

            return value;
        }

        /// <summary>
        /// Equivalent to GetDisplayString(false).
        /// </summary>
        public override string ToString()
        {
            return ToString(false);
        }

        internal MemoryRegion(DesktopRuntimeBase clr, ulong addr, ulong size, ClrMemoryRegionType type, ulong moduleOrAppDomain)
        {
            Address = addr;
            Size = size;
            _runtime = clr;
            Type = type;
            _domainModuleHeap = moduleOrAppDomain;
        }

        internal MemoryRegion(DesktopRuntimeBase clr, ulong addr, ulong size, ClrMemoryRegionType type, ClrAppDomain domain)
        {
            Address = addr;
            Size = size;
            _runtime = clr;
            Type = type;
            _domainModuleHeap = domain.Address;
        }

        internal MemoryRegion(DesktopRuntimeBase clr, ulong addr, ulong size, ClrMemoryRegionType type)
        {
            Address = addr;
            Size = size;
            _runtime = clr;
            Type = type;
        }

        internal MemoryRegion(DesktopRuntimeBase clr, ulong addr, ulong size, ClrMemoryRegionType type, uint heap, GCSegmentType seg)
        {
            Address = addr;
            Size = size;
            _runtime = clr;
            Type = type;
            _domainModuleHeap = heap;
            _segmentType = seg;
        }
    }
}