// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable CA1721 // Property names should not match get methods

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    /// <summary>
    /// This struct represents a single step in <see cref="ClrmdHeap"/>'s heap walk.  This is used for diagnostic purposes.
    /// </summary>
    public struct HeapWalkStep
    {
        public ulong Address { get; set; }
        public ulong MethodTable { get; set; }
        public uint Count { get; set; }
        public int BaseSize { get; set; }
        public int ComponentSize { get; set; }
    }
}