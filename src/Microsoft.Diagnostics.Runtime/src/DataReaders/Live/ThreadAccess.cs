// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    [Flags]
    internal enum ThreadAccess
    {
        SYNCHRONIZE = 0x00100000,
        
        THREAD_TERMINATE = 0x0001,
        THREAD_SUSPEND_RESUME = 0x0002,
        
        THREAD_GET_CONTEXT = 0x0008,
        THREAD_SET_CONTEXT = 0x0010,
        THREAD_SET_INFORMATION = 0x0020,
        THREAD_QUERY_INFORMATION = 0x0040,
        THREAD_SET_THREAD_TOKEN = 0x0080,
        
        THREAD_DIRECT_IMPERSONATION = 0x0200,
        THREAD_SET_LIMITED_INFORMATION = 0x0400,
        THREAD_QUERY_LIMITED_INFORMATION = 0x0800,
        
        THREAD_ALL_ACCESS = 0x1F03FF
    }
}