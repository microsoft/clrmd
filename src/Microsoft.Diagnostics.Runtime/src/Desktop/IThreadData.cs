// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface IThreadData
    {
        ulong Next { get; }
        ulong AllocPtr { get; }
        ulong AllocLimit { get; }
        uint OSThreadID { get; }
        uint ManagedThreadID { get; }
        ulong Teb { get; }
        ulong AppDomain { get; }
        uint LockCount { get; }
        int State { get; }
        ulong ExceptionPtr { get; }
        bool Preemptive { get; }
    }
}