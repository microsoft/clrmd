// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct HeapDetails
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


        public ulong EphemeralAllocContextPtr => GenerationTable[0].AllocationContextPointer;
        public ulong EphemeralAllocContextLimit => GenerationTable[0].AllocationContextLimit;

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

        public ulong FQAllObjectsStart => FinalizationFillPointers[0];
        public ulong FQAllObjectsStop => FinalizationFillPointers[3];
        public ulong FQRootsStart => FinalizationFillPointers[3];
        public ulong FQRootsStop => FinalizationFillPointers[5];
    }
}