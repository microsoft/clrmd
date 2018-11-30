// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Returns the addresses and sizes of the hot and cold regions of a method.
    /// </summary>
    public class HotColdRegions
    {
        /// <summary>
        /// Returns the start address of the method's hot region.
        /// </summary>
        public ulong HotStart { get; internal set; }
        /// <summary>
        /// Returns the size of the hot region.
        /// </summary>
        public uint HotSize { get; internal set; }
        /// <summary>
        /// Returns the start address of the method's cold region.
        /// </summary>
        public ulong ColdStart { get; internal set; }
        /// <summary>
        /// Returns the size of the cold region.
        /// </summary>
        public uint ColdSize { get; internal set; }
    }
}