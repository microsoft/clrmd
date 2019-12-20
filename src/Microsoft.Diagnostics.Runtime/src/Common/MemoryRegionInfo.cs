// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Contains information about a range of memory.
    /// </summary>
    public struct MemoryRegionInfo
    {
        /// <summary>
        /// The base address of the allocation.
        /// </summary>
        public ulong BaseAddress { get; }

        /// <summary>
        /// The size of the allocation.
        /// </summary>
        public ulong Size { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="addr">Base address of the memory range.</param>
        /// <param name="size">The size of the memory range.</param>
        public MemoryRegionInfo(ulong addr, ulong size)
        {
            BaseAddress = addr;
            Size = size;
        }
    }
}