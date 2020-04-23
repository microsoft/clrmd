// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct HeapDetails
    {
        public readonly ClrDataAddress Address; // Only filled in in server mode, otherwise NULL
        public readonly ClrDataAddress Allocated;
        public readonly ClrDataAddress MarkArray;
        public readonly ClrDataAddress CAllocateLH;
        public readonly ClrDataAddress NextSweepObj;
        public readonly ClrDataAddress SavedSweepEphemeralSeg;
        public readonly ClrDataAddress SavedSweepEphemeralStart;
        public readonly ClrDataAddress BackgroundSavedLowestAddress;
        public readonly ClrDataAddress BackgroundSavedHighestAddress;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly GenerationData[] GenerationTable;
        public readonly ClrDataAddress EphemeralHeapSegment;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public readonly ClrDataAddress[] FinalizationFillPointers;
        public readonly ClrDataAddress LowestAddress;
        public readonly ClrDataAddress HighestAddress;
        public readonly ClrDataAddress CardTable;

        public ulong EphemeralAllocContextPtr => GenerationTable[0].AllocationContextPointer;
        public ulong EphemeralAllocContextLimit => GenerationTable[0].AllocationContextLimit;

        public ulong FQAllObjectsStart => FinalizationFillPointers[0];
        public ulong FQAllObjectsStop => FinalizationFillPointers[3];
        public ulong FQRootsStart => FinalizationFillPointers[3];
        public ulong FQRootsStop => FinalizationFillPointers[5];
    }
}