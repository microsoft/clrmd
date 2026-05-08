// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.StressLogs
{
    /// <summary>
    /// Identifies a stress log message that uses one of the well-known
    /// runtime format strings. Consumers (notably the GC history commands)
    /// use this in lieu of pattern-matching the format string text.
    /// </summary>
    public enum StressLogKnownFormat
    {
        /// <summary>Not a recognized well-known message.</summary>
        None,

        /// <summary>Beginning of a GC.</summary>
        GcStart,

        /// <summary>End of a GC.</summary>
        GcEnd,

        /// <summary>A GC root that was relocated during compaction.</summary>
        GcRoot,

        /// <summary>A GC root that was promoted (kept alive).</summary>
        GcRootPromote,

        /// <summary>A plug of objects that was relocated.</summary>
        GcPlugMove,

        /// <summary>A task switch marker.</summary>
        TaskSwitch,
    }
}
