// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    internal interface IAbstractNativeHeapProvider
    {
        IEnumerable<ClrNativeHeapInfo> EnumerateDomainHeaps(ulong domain);
        IEnumerable<ClrNativeHeapInfo> EnumerateJitManagerHeaps(ulong jitManager);
        IEnumerable<ClrNativeHeapInfo> EnumerateLoaderAllocatorNativeHeaps(ulong loaderAllocator);
        IEnumerable<ClrNativeHeapInfo> EnumerateThunkHeaps(ulong thunkHeapAddress);
    }
}