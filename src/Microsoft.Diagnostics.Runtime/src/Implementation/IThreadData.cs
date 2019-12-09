// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface IThreadData
    {
        IThreadHelpers Helpers { get; }
        ulong Address { get; }
        bool IsFinalizer { get; }
        uint OSThreadID { get; }
        int ManagedThreadID { get; }
        uint LockCount { get; }
        int State { get; }
        ulong ExceptionHandle { get; }
        bool Preemptive { get; }
        ulong StackBase { get; }
        ulong StackLimit { get; }
    }
}