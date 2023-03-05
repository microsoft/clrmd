// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime.Interfaces
{
    public interface IClrRuntime : IDisposable
    {
        ImmutableArray<IClrAppDomain> AppDomains { get; }
        IClrModule BaseClassLibrary { get; }
        IClrInfo ClrInfo { get; }
        IDataTarget DataTarget { get; }
        ClrHeap Heap { get; }
        bool IsThreadSafe { get; }
        IClrAppDomain? SharedDomain { get; }
        IClrAppDomain? SystemDomain { get; }
        ImmutableArray<IClrThread> Threads { get; }

        IEnumerable<ClrNativeHeapInfo> EnumerateClrNativeHeaps();
        IEnumerable<IClrRoot> EnumerateHandles();
        IEnumerable<IClrJitManager> EnumerateJitManagers();
        IEnumerable<IClrModule> EnumerateModules();
        void FlushCachedData();
        string? GetJitHelperFunctionName(ulong address);
        IClrMethod? GetMethodByHandle(ulong methodHandle);
        IClrMethod? GetMethodByInstructionPointer(ulong ip);
        IClrType? GetTypeByMethodTable(ulong methodTable);
    }
}