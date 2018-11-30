// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Specifies how to attach to a live process.
    /// </summary>
    public enum AttachFlag
    {
        /// <summary>
        /// Performs an invasive debugger attach.  Allows the consumer of this API to control the target
        /// process through normal IDebug function calls.  The process will be paused.
        /// </summary>
        Invasive,

        /// <summary>
        /// Performs a non-invasive debugger attach.  The process will be paused by this attached (and
        /// for the duration of the attach) but the caller cannot control the target process.  This is
        /// useful when there's already a debugger attached to the process.
        /// </summary>
        NonInvasive,

        /// <summary>
        /// Performs a "passive" attach, meaning no debugger is actually attached to the target process.
        /// The process is not paused, so queries for quickly changing data (such as the contents of the
        /// GC heap or callstacks) will be highly inconsistent unless the user pauses the process through
        /// other means.  Useful when attaching with ICorDebug (managed debugger), as you cannot use a
        /// non-invasive attach with ICorDebug.
        /// </summary>
        Passive
    }
}