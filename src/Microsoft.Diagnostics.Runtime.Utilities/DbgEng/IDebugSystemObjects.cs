// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    public interface IDebugSystemObjects
    {
        public int ProcessSystemId { get; }
        public int CurrentThreadId { get; set; }

        public int GetCurrentThreadTeb(out ulong teb);
        public int GetNumberThreads(out int threadCount);
        public int GetThreadIdBySystemId(int systemId, out int threadId);
        public int GetThreadSystemIDs(Span<uint> sysIds);
        public int GetCurrentProcessUpTime(out TimeSpan upTime);
        public int GetCurrentProcessExecutableNameWide(out string? exeName);
        public int GetCurrentProcessTeb(out ulong teb);
    }
}