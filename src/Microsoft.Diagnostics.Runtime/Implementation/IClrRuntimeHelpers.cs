﻿using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal interface IClrRuntimeHelpers : IDisposable
    {
        void Flush();
        IEnumerable<ClrThread> EnumerateThreads();
        ClrHeap CreateHeap();
        ClrAppDomainData GetAppDomainData();
        ClrMethod? GetMethodByMethodDesc(ulong methodDesc);
        ClrMethod? GetMethodByInstructionPointer(ulong ip);
        IEnumerable<ClrHandle> EnumerateHandles();
        IEnumerable<ClrJitManager> EnumerateClrJitManagers();
        string? GetJitHelperFunctionName(ulong address);
    }
}
