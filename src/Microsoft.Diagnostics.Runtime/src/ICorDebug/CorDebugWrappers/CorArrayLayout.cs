// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential)]
    public struct COR_ARRAY_LAYOUT
    {
        public COR_TYPEID componentID; // The type of objects the array contains
        public CorElementType componentType; // Whether the component itself is a GC reference, value class, or primitive
        public int firstElementOffset; // The offset to the first element
        public int elementSize; // The size of each element
        public int countOffset; // The offset to the number of elements in the array.

        // For multidimensional arrays (works with normal arrays too).
        public int rankSize; // The size of the rank
        public int numRanks; // The number of ranks in the array (1 for array, N for multidimensional array)
        public int rankOffset; // The offset at which the ranks start
    }
}