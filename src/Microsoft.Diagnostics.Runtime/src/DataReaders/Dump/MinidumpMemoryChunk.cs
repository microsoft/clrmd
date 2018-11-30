// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal class MinidumpMemoryChunk : IComparable<MinidumpMemoryChunk>
    {
        public ulong Size;
        public ulong TargetStartAddress;
        // TargetEndAddress is the first byte beyond the end of this chunk.
        public ulong TargetEndAddress;
        public ulong RVA;

        public int CompareTo(MinidumpMemoryChunk other)
        {
            return TargetStartAddress.CompareTo(other.TargetStartAddress);
        }
    }
}