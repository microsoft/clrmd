// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ThreadPoolData : IThreadPoolData
    {
        public readonly int CpuUtilization;
        public readonly int NumIdleWorkerThreads;
        public readonly int NumWorkingWorkerThreads;
        public readonly int NumRetiredWorkerThreads;
        public readonly int MinLimitTotalWorkerThreads;
        public readonly int MaxLimitTotalWorkerThreads;

        public readonly ulong FirstUnmanagedWorkRequest;

        public readonly ulong HillClimbingLog;
        public readonly int HillClimbingLogFirstIndex;
        public readonly int HillClimbingLogSize;

        public readonly int NumTimers;

        public readonly int NumCPThreads;
        public readonly int NumFreeCPThreads;
        public readonly int MaxFreeCPThreads;
        public readonly int NumRetiredCPThreads;
        public readonly int MaxLimitTotalCPThreads;
        public readonly int CurrentLimitTotalCPThreads;
        public readonly int MinLimitTotalCPThreads;

        public readonly ulong _asyncTimerCallbackCompletionFPtr;

        int IThreadPoolData.MinCP => MinLimitTotalCPThreads;
        int IThreadPoolData.MaxCP => MaxLimitTotalCPThreads;
        int IThreadPoolData.CPU => CpuUtilization;
        int IThreadPoolData.NumFreeCP => NumFreeCPThreads;
        int IThreadPoolData.MaxFreeCP => MaxFreeCPThreads;
        int IThreadPoolData.TotalThreads => NumIdleWorkerThreads + NumWorkingWorkerThreads + NumRetiredWorkerThreads;
        int IThreadPoolData.RunningThreads => NumWorkingWorkerThreads;
        int IThreadPoolData.IdleThreads => NumIdleWorkerThreads;
        int IThreadPoolData.MinThreads => MinLimitTotalWorkerThreads;
        int IThreadPoolData.MaxThreads => MaxLimitTotalWorkerThreads;
        ulong IThreadPoolData.FirstWorkRequest => FirstUnmanagedWorkRequest;
        ulong IThreadPoolData.QueueUserWorkItemCallbackFPtr => 0;
        ulong IThreadPoolData.AsyncCallbackCompletionFPtr => 0;
        ulong IThreadPoolData.AsyncTimerCallbackCompletionFPtr => _asyncTimerCallbackCompletionFPtr;
    }
}