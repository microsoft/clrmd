// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ThreadPoolData
    {
        public readonly int CpuUtilization;
        public readonly int NumIdleWorkerThreads;
        public readonly int NumWorkingWorkerThreads;
        public readonly int NumRetiredWorkerThreads;
        public readonly int MinLimitTotalWorkerThreads;
        public readonly int MaxLimitTotalWorkerThreads;

        public readonly ClrDataAddress FirstUnmanagedWorkRequest;

        public readonly ClrDataAddress HillClimbingLog;
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

        public readonly ClrDataAddress AsyncTimerCallbackCompletionFPtr;
    }
}