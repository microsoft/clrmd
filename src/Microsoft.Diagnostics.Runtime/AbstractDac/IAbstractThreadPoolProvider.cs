﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.AbstractDac
{
    internal interface IAbstractThreadPoolProvider
    {
        bool GetLegacyThreadPoolData(out LegacyThreadPoolInfo data);
        bool GetLegacyWorkRequestData(ulong workRequest, out LegacyWorkRequestInfo workRequestData);
    }

    internal struct LegacyThreadPoolInfo
    {
        public int CpuUtilization { get; set; }
        public int NumIdleWorkerThreads { get; set; }
        public int NumWorkingWorkerThreads { get; set; }
        public int NumRetiredWorkerThreads { get; set; }
        public int MinLimitTotalWorkerThreads { get; set; }
        public int MaxLimitTotalWorkerThreads { get; set; }

        public ulong FirstUnmanagedWorkRequest { get; set; }

        public ulong HillClimbingLog { get; set; }
        public uint HillClimbingLogFirstIndex { get; set; }
        public uint HillClimbingLogSize { get; set; }

        public int NumTimers { get; set; }

        public int NumCPThreads { get; set; }
        public int NumFreeCPThreads { get; set; }
        public int MaxFreeCPThreads { get; set; }
        public int NumRetiredCPThreads { get; set; }
        public int MaxLimitTotalCPThreads { get; set; }
        public int CurrentLimitTotalCPThreads { get; set; }
        public int MinLimitTotalCPThreads { get; set; }

        public ulong AsyncTimerCallbackCompletionFPtr { get; set; }
    }

    internal struct LegacyWorkRequestInfo
    {
        public ulong Function { get; set; }
        public ulong Context { get; set; }
        public ulong NextWorkRequest { get; set; }
    }
}