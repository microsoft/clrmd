// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.Runtime.StressLogs
{
    /// <summary>
    /// Bit flags identifying which runtime subsystem emitted a stress log message.
    /// Mirrors the runtime's <c>loglf.h</c> facility values.
    /// </summary>
    [Flags]
    public enum StressLogFacility : uint
    {
        /// <summary>No facility.</summary>
        None        = 0x00000000,

        /// <summary>Garbage collector.</summary>
        GC          = 0x00000001,

        /// <summary>Garbage collector roots.</summary>
        GCRoots     = 0x00000002,

        /// <summary>Garbage collector allocation.</summary>
        GCAlloc     = 0x00000100,

        /// <summary>GC information / detailed tracing.</summary>
        GCInfo      = 0x00010000,

        /// <summary>EE memory subsystem.</summary>
        EEMem       = 0x00020000,

        /// <summary>Always logged regardless of facility filter.</summary>
        Always      = 0x80000000,
    }
}
