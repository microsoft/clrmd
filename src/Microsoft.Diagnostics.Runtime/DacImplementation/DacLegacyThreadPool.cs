// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal class DacLegacyThreadPool : IAbstractLegacyThreadPool
    {
        private readonly SOSDac _sos;

        public DacLegacyThreadPool(SOSDac sos)
        {
            _sos = sos;
        }

        public bool GetLegacyThreadPoolData(out LegacyThreadPoolInfo result)
        {
            HResult hr = _sos.GetThreadPoolData(out ThreadPoolData data);
            result = new()
            {
                AsyncTimerCallbackCompletionFPtr = data.AsyncTimerCallbackCompletionFPtr,
                CpuUtilization = data.CpuUtilization,
                CurrentLimitTotalCPThreads = data.CurrentLimitTotalCPThreads,
                FirstUnmanagedWorkRequest = data.FirstUnmanagedWorkRequest,
                HillClimbingLog = data.HillClimbingLog,
                HillClimbingLogFirstIndex = data.HillClimbingLogFirstIndex,
                HillClimbingLogSize = data.HillClimbingLogSize,
                MaxFreeCPThreads = data.MaxFreeCPThreads,
                MaxLimitTotalCPThreads = data.MaxLimitTotalCPThreads,
                MaxLimitTotalWorkerThreads = data.MaxLimitTotalWorkerThreads,
                MinLimitTotalCPThreads = data.MinLimitTotalCPThreads,
                MinLimitTotalWorkerThreads = data.MinLimitTotalWorkerThreads,
                NumCPThreads = data.NumCPThreads,
                NumFreeCPThreads = data.NumFreeCPThreads,
                NumIdleWorkerThreads = data.NumIdleWorkerThreads,
                NumRetiredCPThreads = data.NumRetiredCPThreads,
                NumRetiredWorkerThreads = data.NumRetiredWorkerThreads,
                NumTimers = data.NumTimers,
                NumWorkingWorkerThreads = data.NumWorkingWorkerThreads,
            };

            return hr;
        }

        public bool GetLegacyWorkRequestData(ulong workRequest, out LegacyWorkRequestInfo workRequestInfo)
        {
            bool res = _sos.GetWorkRequestData(workRequest, out WorkRequestData workRequestData);
            workRequestInfo = new()
            {
                Function = workRequestData.Function,
                Context = workRequestData.Context,
                NextWorkRequest = workRequestData.NextWorkRequest,
            };

            return res;
        }
    }
}