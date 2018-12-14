// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable 0649
#pragma warning disable 0169

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct V2ThreadPoolData : IThreadPoolData
    {
        public readonly int CpuUtilization;
        public readonly int NumWorkerThreads;
        public readonly int MinLimitTotalWorkerThreads;
        public readonly int MaxLimitTotalWorkerThreads;
        public readonly int NumRunningWorkerThreads;
        public readonly int NumIdleWorkerThreads;
        public readonly int NumQueuedWorkRequests;

        public readonly ulong FirstWorkRequest;
        public readonly uint NumTimers;

        public readonly int NumCPThreads;
        public readonly int NumFreeCPThreads;
        public readonly int MaxFreeCPThreads;
        public readonly int NumRetiredCPThreads;
        public readonly int MaxLimitTotalCPThreads;
        public readonly int CurrentLimitTotalCPThreads;
        public readonly int MinLimitTotalCPThreads;

        public readonly ulong QueueUserWorkItemCallbackFPtr;
        public readonly ulong AsyncCallbackCompletionFPtr;
        public readonly ulong AsyncTimerCallbackCompletionFPtr;

        int IThreadPoolData.MinCP => MinLimitTotalCPThreads;
        int IThreadPoolData.MaxCP => MaxLimitTotalCPThreads;
        int IThreadPoolData.CPU => CpuUtilization;
        int IThreadPoolData.NumFreeCP => NumFreeCPThreads;
        int IThreadPoolData.MaxFreeCP => MaxFreeCPThreads;
        int IThreadPoolData.TotalThreads => NumWorkerThreads;
        int IThreadPoolData.RunningThreads => NumRunningWorkerThreads;
        int IThreadPoolData.IdleThreads => NumIdleWorkerThreads;
        int IThreadPoolData.MinThreads => MinLimitTotalWorkerThreads;
        int IThreadPoolData.MaxThreads => MaxLimitTotalWorkerThreads;
        ulong IThreadPoolData.FirstWorkRequest => FirstWorkRequest;
        ulong IThreadPoolData.QueueUserWorkItemCallbackFPtr => QueueUserWorkItemCallbackFPtr;
        ulong IThreadPoolData.AsyncCallbackCompletionFPtr => AsyncCallbackCompletionFPtr;
        ulong IThreadPoolData.AsyncTimerCallbackCompletionFPtr => AsyncTimerCallbackCompletionFPtr;
    }
}