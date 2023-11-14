// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    /// <summary>
    /// Interface to control the target dac/debugging layer.
    ///
    /// This interface is optional, but if not present the library
    /// will assume this dac is not thread safe and cannot flush
    /// cahed data.
    ///
    /// This interface is not "stable" and may change even in minor or patch
    /// versions of ClrMD.
    /// </summary>
    public interface IAbstractDacController
    {
        /// <summary>
        /// Whether all methods on all abstract dac APIs are thread
        /// safe or not.  If <see cref="IsThreadSafe"/> returns false, we will
        /// not allow any multithreaded use of ClrMD.
        /// </summary>
        bool IsThreadSafe { get; }

        /// <summary>
        /// Whether the library supports flushing cached data.  The
        /// <see cref="Flush"/> method will only be called if <see cref="CanFlush"/>
        /// returns true.
        /// </summary>
        bool CanFlush { get; }

        /// <summary>
        /// Flushes any built up caches of data reads within the dac.  This
        /// is typically called in a live debugging operation after the state
        /// of the process is changed (e.g. after stepping in the debugger)
        /// to ensure we aren't reading stale data.
        /// </summary>
        void Flush();
    }
}