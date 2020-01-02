// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Exception kind
    /// </summary>
    public enum ClrDiagnosticsExceptionKind
    {
        /// <summary>
        /// Unknown error occured.
        /// </summary>
        Unknown,

        /// <summary>
        /// Dump file is corrupted or has an unknown format.
        /// </summary>
        CorruptedFileOrUnknownFormat,

        /// <summary>
        /// The caller attempted to re-use an object after calling <see cref="ClrRuntime.FlushCachedData"/>.  See the
        /// documentation for <see cref="ClrRuntime.FlushCachedData"/> for more details.
        /// </summary>
        RevisionMismatch,

        /// <summary>
        /// Something unexpected went wrong with the debugger we used to attach to the process or load the crash dump.
        /// </summary>
        DebuggerError,

        /// <summary>
        /// An error occurred while processing the given crash dump.
        /// </summary>
        CrashDumpError,

        /// <summary>
        /// Something unexpected went wrong when requesting data from the target process.
        /// </summary>
        DataRequestError,

        /// <summary>
        /// Hit an unexpected (non-recoverable) DAC error.
        /// </summary>
        DacError,

        /// <summary>
        /// The dll of the specified runtime (<i>mscorwks.dll</i> or <i>clr.dll</i>) is loaded into the process, but
        /// has not actually been initialized and thus cannot be debugged.
        /// </summary>
        RuntimeUninitialized
    }
}