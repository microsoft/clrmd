// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface IThreadPoolData
    {
        int TotalThreads { get; }
        int RunningThreads { get; }
        int IdleThreads { get; }
        int MinThreads { get; }
        int MaxThreads { get; }
        ulong FirstWorkRequest { get; }
        ulong QueueUserWorkItemCallbackFPtr { get; }
        ulong AsyncCallbackCompletionFPtr { get; }
        ulong AsyncTimerCallbackCompletionFPtr { get; }
        int MinCP { get; }
        int MaxCP { get; }
        int CPU { get; }
        int NumFreeCP { get; }
        int MaxFreeCP { get; }
    }
}