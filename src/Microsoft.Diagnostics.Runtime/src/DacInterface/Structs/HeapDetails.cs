// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct HeapDetails : IHeapDetails
    {
        public readonly ulong Address; // Only filled in in server mode, otherwise NULL
        public readonly ulong Allocated;
        public readonly ulong MarkArray;
        public readonly ulong CAllocateLH;
        public readonly ulong NextSweepObj;
        public readonly ulong SavedSweepEphemeralSeg;
        public readonly ulong SavedSweepEphemeralStart;
        public readonly ulong BackgroundSavedLowestAddress;
        public readonly ulong BackgroundSavedHighestAddress;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly GenerationData[] GenerationTable;
        public readonly ulong EphemeralHeapSegment;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public readonly ulong[] FinalizationFillPointers;
        public readonly ulong LowestAddress;
        public readonly ulong HighestAddress;
        public readonly ulong CardTable;

        internal HeapDetails(ref HeapDetails other)
        {
            this = other;

            unchecked
            {
                if (IntPtr.Size == 4)
                {
                    FixupPointer(ref Address);
                    FixupPointer(ref Allocated);
                    FixupPointer(ref MarkArray);
                    FixupPointer(ref CAllocateLH);
                    FixupPointer(ref NextSweepObj);
                    FixupPointer(ref SavedSweepEphemeralSeg);
                    FixupPointer(ref SavedSweepEphemeralStart);
                    FixupPointer(ref BackgroundSavedHighestAddress);
                    FixupPointer(ref BackgroundSavedLowestAddress);

                    FixupPointer(ref EphemeralHeapSegment);
                    FixupPointer(ref LowestAddress);
                    FixupPointer(ref HighestAddress);
                    FixupPointer(ref CardTable);

                    for (int i = 0; i < FinalizationFillPointers.Length; i++)
                        FixupPointer(ref FinalizationFillPointers[i]);

                    for (int i = 0; i < GenerationTable.Length; i++)
                        GenerationTable[i] = new GenerationData(ref GenerationTable[i]);
                }
            }
        }

        private static void FixupPointer(ref ulong ptr)
        {
            ptr = (uint)ptr;
        }

        ulong IHeapDetails.FirstHeapSegment => GenerationTable[2].StartSegment;

        ulong IHeapDetails.FirstLargeHeapSegment => GenerationTable[3].StartSegment;
        ulong IHeapDetails.EphemeralSegment => EphemeralHeapSegment;
        ulong IHeapDetails.EphemeralEnd => Allocated;
        ulong IHeapDetails.EphemeralAllocContextPtr => GenerationTable[0].AllocationContextPointer;
        ulong IHeapDetails.EphemeralAllocContextLimit => GenerationTable[0].AllocationContextLimit;
        ulong IHeapDetails.FQAllObjectsStart => FinalizationFillPointers[0];
        ulong IHeapDetails.FQAllObjectsStop => FinalizationFillPointers[3];
        ulong IHeapDetails.FQRootsStart => FinalizationFillPointers[3];
        ulong IHeapDetails.FQRootsStop => FinalizationFillPointers[5];
        ulong IHeapDetails.Gen0Start => GenerationTable[0].AllocationStart;
        ulong IHeapDetails.Gen0Stop => Allocated;
        ulong IHeapDetails.Gen1Start => GenerationTable[1].AllocationStart;
        ulong IHeapDetails.Gen1Stop => GenerationTable[0].AllocationStart;
        ulong IHeapDetails.Gen2Start => GenerationTable[2].AllocationStart;
        ulong IHeapDetails.Gen2Stop => GenerationTable[1].AllocationStart;
    }
}