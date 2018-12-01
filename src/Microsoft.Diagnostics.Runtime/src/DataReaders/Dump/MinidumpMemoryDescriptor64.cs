// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// Describes a range of memory in the target.
    /// </summary>
    /// <remarks>
    /// This is used for full-memory minidumps where
    /// all of the raw memory is laid out sequentially at the
    /// end of the dump.  There is no need for individual RVAs
    /// as the RVA is the base RVA plus the sum of the preceeding
    /// data blocks.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MINIDUMP_MEMORY_DESCRIPTOR64
    {
        public const int SizeOf = 16;

        /// <summary>
        /// Starting Target address of the memory range.
        /// </summary>
        private readonly ulong _startofmemoryrange;
        public ulong StartOfMemoryRange => DumpNative.ZeroExtendAddress(_startofmemoryrange);

        /// <summary>
        /// Size of memory in bytes.
        /// </summary>
        public ulong DataSize;
    }
}