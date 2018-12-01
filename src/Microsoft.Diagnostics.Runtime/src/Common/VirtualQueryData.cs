// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// The result of a VirtualQuery.
    /// </summary>
    [Serializable]
    public struct VirtualQueryData
    {
        /// <summary>
        /// The base address of the allocation.
        /// </summary>
        public ulong BaseAddress;

        /// <summary>
        /// The size of the allocation.
        /// </summary>
        public ulong Size;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="addr">Base address of the memory range.</param>
        /// <param name="size">The size of the memory range.</param>
        public VirtualQueryData(ulong addr, ulong size)
        {
            BaseAddress = addr;
            Size = size;
        }
    }
}