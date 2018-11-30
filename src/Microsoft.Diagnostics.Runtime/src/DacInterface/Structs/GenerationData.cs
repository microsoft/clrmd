// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct GenerationData
    {
        public readonly ulong StartSegment;
        public readonly ulong AllocationStart;

        // These are examined only for generation 0, otherwise NULL
        public readonly ulong AllocationContextPointer;
        public readonly ulong AllocationContextLimit;

        internal GenerationData(ref GenerationData other)
        {
            this = other;

            if (IntPtr.Size == 4)
            {
                FixupPointer(ref StartSegment);
                FixupPointer(ref AllocationStart);
                FixupPointer(ref AllocationContextPointer);
                FixupPointer(ref AllocationContextLimit);
            }
        }

        private static void FixupPointer(ref ulong ptr)
        {
            ptr = (uint)ptr;
        }
    }
}