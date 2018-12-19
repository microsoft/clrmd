// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// List of Threads in the minidump.
    /// </summary>
    internal class MINIDUMP_THREAD_LIST<T> : MinidumpArray<T>, IMinidumpThreadList
        where T : MINIDUMP_THREAD
    {
        internal MINIDUMP_THREAD_LIST(DumpPointer streamPointer, MINIDUMP_STREAM_TYPE streamType)
            : base(streamPointer, streamType)
        {
            if (streamType != MINIDUMP_STREAM_TYPE.ThreadListStream &&
                streamType != MINIDUMP_STREAM_TYPE.ThreadExListStream)
                throw new ClrDiagnosticsException("Only ThreadListStream and ThreadExListStream are supported.", ClrDiagnosticsExceptionKind.CrashDumpError);
        }

        // IMinidumpThreadList
        public new MINIDUMP_THREAD GetElement(uint idx)
        {
            T t = base.GetElement(idx);
            return t;
        }

        public new uint Count()
        {
            return base.Count;
        }
    }
}