// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface IRuntimeHelpers : IDisposable
    {
        ITypeFactory Factory { get; }
        IDataReader DataReader { get; }
        IReadOnlyList<ClrThread> GetThreads(ClrRuntime runtime);
        IReadOnlyList<ClrAppDomain> GetAppDomains(ClrRuntime runtime, out ClrAppDomain? system, out ClrAppDomain? shared);
        IEnumerable<ClrHandle> EnumerateHandleTable(ClrRuntime runtime);
        void FlushCachedData();
        ulong GetMethodDesc(ulong ip);
        string? GetJitHelperFunctionName(ulong ip);
        ClrModule? GetBaseClassLibrary(ClrRuntime runtime);
    }
}