// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// The on-disk layout we are reading. Chosen during open based on the
    /// header bytes; controls how subsequent fields are interpreted.
    /// </summary>
    internal enum StressLogLayoutKind
    {
        /// <summary>
        /// In-process layout from before the module table was added. Format
        /// offsets are interpreted relative to the single <c>moduleOffset</c>
        /// field in the header. Recognized only as a fallback when the module
        /// table looks unusable.
        /// </summary>
        InProcessLegacy,

        /// <summary>
        /// In-process layout with the module table. Used when the header's
        /// module table appears valid and the runtime version reports
        /// stress-log version &gt;= 3.
        /// </summary>
        InProcessV3,

        /// <summary>
        /// In-process layout with the V4 <c>StressMsg</c> bit packing
        /// (split <c>formatOffset</c>). Distinguished from V3 by the
        /// runtime version reported by the DAC; the structural layout
        /// of the header itself is unchanged.
        /// </summary>
        InProcessV4,

        /// <summary>
        /// Memory-mapped log, version <c>0x00010001</c>: <c>StressLogHeader</c>
        /// followed by an embedded module image. Format offsets index into
        /// the module image rather than into target memory. Uses the legacy
        /// 26-bit <c>formatOffset</c>.
        /// </summary>
        MemoryMappedV1,

        /// <summary>
        /// Memory-mapped log, version <c>0x00010002</c>: same shape as V1
        /// but with the V4 39-bit <c>formatOffset</c>.
        /// </summary>
        MemoryMappedV2,
    }
}
