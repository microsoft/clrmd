// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Interfaces;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Information about the CLR Runtime's ThreadPool.
    /// </summary>
    public sealed class ClrThreadPool : IClrThreadPool
    {
        private readonly ClrRuntime _runtime;
        private readonly IAbstractLegacyThreadPool? _legacyData;
        private readonly ulong _nativeLogAddress;
        private readonly uint _nativeLogStart;
        private readonly uint _nativeLogSize;

        /// <summary>
        /// Used to track whether we successfully initialized this object to prevent throw/catch.
        /// </summary>
        internal bool Initialized { get; }

        /// <summary>
        /// Whether this runtime is using the Portable threadpool or not.
        /// </summary>
        public bool UsingPortableThreadPool { get; }

        /// <summary>
        /// Whether this runtime is using the Windows threadpool or not.
        /// </summary>
        public bool UsingWindowsThreadPool { get; }

        /// <summary>
        /// The current CPU utilization of the ThreadPool (a number between 0 and 100).
        /// </summary>
        public int CpuUtilization { get; }

        /// <summary>
        /// The minimum number of worker threads allowed for the ThreadPool.
        /// </summary>
        public int MinThreads { get; }

        /// <summary>
        /// The maximum number of worker threads allowed for the ThreadPool.
        /// </summary>
        public int MaxThreads { get; }

        /// <summary>
        /// The number of idle worker threads.
        /// </summary>
        public int IdleWorkerThreads { get; }

        /// <summary>
        /// The number of active worker threads.
        /// </summary>
        public int ActiveWorkerThreads { get; }

        public int WindowsThreadPoolThreadCount { get; }
        public int TotalCompletionPorts { get; }
        public int FreeCompletionPorts { get; }
        public int MaxFreeCompletionPorts { get; }
        public int CompletionPortCurrentLimit { get; }
        public int MinCompletionPorts { get; }
        public int MaxCompletionPorts { get; }
        public bool HasLegacyData { get; }

        /// <summary>
        /// The number of retired worker threads.
        /// </summary>
        public int RetiredWorkerThreads { get; }

        private readonly ClrDataAddress _firstLegacyWorkRequest;
        private readonly ClrDataAddress _asyncTimerFunction;

        internal ClrThreadPool(ClrRuntime runtime, IAbstractLegacyThreadPool? helpers)
        {
            _runtime = runtime;
            _legacyData = helpers;


            LegacyThreadPoolInfo tpData = default;
            HasLegacyData = _legacyData is not null && _legacyData.GetLegacyThreadPoolData(out tpData);

            ClrAppDomain domain = GetDomain();

            GetPortableOrWindowsThreadPoolInfo(domain,
                                            out bool usingPortableThreadPool,
                                            out ClrObject portableThreadPool,
                                            out bool usingWindowsThreadPool,
                                            out ClrType? windowsThreadPoolType);

            UsingPortableThreadPool = usingPortableThreadPool;
            UsingWindowsThreadPool = usingWindowsThreadPool;

            Initialized = UsingPortableThreadPool || UsingWindowsThreadPool || HasLegacyData;

            if (UsingWindowsThreadPool)
            {
                ClrStaticField threadCountField = windowsThreadPoolType!.GetStaticFieldByName("s_threadCount")!;
                WindowsThreadPoolThreadCount = threadCountField.Read<int>(domain);
                return;
            }

            if (UsingPortableThreadPool)
            {
                CpuUtilization = portableThreadPool.ReadField<int>("_cpuUtilization");
                MinThreads = portableThreadPool.ReadField<ushort>("_minThreads");
                MaxThreads = portableThreadPool.ReadField<ushort>("_maxThreads");

                ClrValueType counts = portableThreadPool.ReadValueTypeField("_separated").ReadValueTypeField("counts").ReadValueTypeField("_data");
                ulong dataValue = counts.ReadField<ulong>("m_value");

                int processingWorkCount = (ushort)(dataValue & 0xffff);
                int existingThreadCount = (ushort)((dataValue >> 16) & 0xffff);

                IdleWorkerThreads = existingThreadCount - processingWorkCount;
                ActiveWorkerThreads = processingWorkCount;

                RetiredWorkerThreads = 0;
            }
            else if (HasLegacyData)
            {
                CpuUtilization = tpData.CpuUtilization;
                MinThreads = tpData.MinLimitTotalWorkerThreads;
                MaxThreads = tpData.MaxLimitTotalWorkerThreads;
                IdleWorkerThreads = tpData.NumIdleWorkerThreads;
                ActiveWorkerThreads = tpData.NumWorkingWorkerThreads;
                RetiredWorkerThreads = tpData.NumRetiredWorkerThreads;

                _nativeLogAddress = tpData.HillClimbingLog;
                _nativeLogStart = tpData.HillClimbingLogFirstIndex;
                _nativeLogSize = tpData.HillClimbingLogSize;

                _firstLegacyWorkRequest = tpData.FirstUnmanagedWorkRequest;
                _asyncTimerFunction = tpData.AsyncTimerCallbackCompletionFPtr;
            }

            // The legacy IO completion thread pool may also be used while the portable thread pool is being used for worker threads
            if (HasLegacyData)
            {
                TotalCompletionPorts = tpData.NumCPThreads;
                FreeCompletionPorts = tpData.NumFreeCPThreads;
                MaxFreeCompletionPorts = tpData.MaxFreeCPThreads;
                CompletionPortCurrentLimit = tpData.CurrentLimitTotalCPThreads;
                MaxCompletionPorts = tpData.MaxLimitTotalCPThreads;
                MinCompletionPorts = tpData.MinLimitTotalCPThreads;
            }
        }

        /// <summary>
        /// Enumerates LegacyThreadPoolWorkRequests.  We only have this for Desktop CLR.
        /// </summary>
        /// <returns>An enumeration of work requests, or an empty enumeration of the runtime
        /// does not have them.</returns>
        public IEnumerable<LegacyThreadPoolWorkRequest> EnumerateLegacyWorkRequests()
        {
            if (_legacyData is null)
                yield break;

            ulong curr = _firstLegacyWorkRequest;
            while (curr != 0 && _legacyData.GetLegacyWorkRequestData(curr, out LegacyWorkRequestInfo workRequestData))
            {
                yield return new LegacyThreadPoolWorkRequest()
                {
                    Context = workRequestData.Context,
                    Function = workRequestData.Function,
                    IsAsyncTimerCallback = workRequestData.Function == _asyncTimerFunction
                };

                curr = workRequestData.NextWorkRequest;
                if (curr == _firstLegacyWorkRequest)
                    break;
            }
        }

        /// <summary>
        /// Enumerates the ThreadPool's HillClimbing log.  This is the log of why we decided to add
        /// or remove threads from the ThreadPool.
        /// Note this is currently only supported on .Net Core and not Desktop CLR.
        /// </summary>
        /// <returns>An enumeration of the HillClimbing log, or an empty enumeration for Desktop CLR.</returns>
        public IEnumerable<HillClimbingLogEntry> EnumerateHillClimbingLog()
        {
            if (UsingPortableThreadPool)
            {
                ClrType? hillClimbingType = _runtime.BaseClassLibrary.GetTypeByName("System.Threading.PortableThreadPool+HillClimbing");
                ClrStaticField? hillClimberField = hillClimbingType?.GetStaticFieldByName("ThreadPoolHillClimber");
                if (hillClimberField is null)
                    yield break;

                ClrObject hillClimber = hillClimberField.ReadObject(GetDomain());

                int start = hillClimber.ReadField<int>("_logStart");
                int size = hillClimber.ReadField<int>("_logSize");
                ClrObject log = hillClimber.ReadObjectField("_log");

                ClrArray logArray = log.AsArray();
                size = Math.Min(size, logArray.Length);

                for (int i = 0; i < size; i++)
                {
                    int index = (i + start) % size;
                    ClrValueType logEntry = logArray.GetStructValue(index);

                    int tickCount = logEntry.ReadField<int>("tickCount");
                    HillClimbingTransition stateOrTransition = logEntry.ReadField<HillClimbingTransition>("stateOrTransition");
                    int newControlSetting = logEntry.ReadField<int>("newControlSetting");
                    int lastHistoryCount = logEntry.ReadField<int>("lastHistoryCount");
                    float lastHistoryMean = logEntry.ReadField<float>("lastHistoryMean");
                    yield return new HillClimbingLogEntry(tickCount, stateOrTransition, newControlSetting, lastHistoryCount, lastHistoryMean);
                }
            }
            else if (_nativeLogAddress != 0)
            {
                IDataReader reader = _runtime.DataTarget.DataReader;

                uint sizeOfLogEntry = (uint)Unsafe.SizeOf<NativeHillClimbingLogEntry>();
                for (uint i = 0; i < _nativeLogSize; i++)
                {
                    uint index = (i + _nativeLogStart) % _nativeLogSize;
                    ulong address = _nativeLogAddress + index * sizeOfLogEntry;

                    if (reader.Read(address, out NativeHillClimbingLogEntry entry))
                        yield return new HillClimbingLogEntry(entry);
                }
            }
        }

        private void GetPortableOrWindowsThreadPoolInfo(
            ClrAppDomain domain,
            out bool usingPortableThreadPool,
            out ClrObject portableThreadPool,
            out bool usingWindowsThreadPool,
            out ClrType? windowsThreadPoolType)
        {
            usingPortableThreadPool = usingWindowsThreadPool = false;
            portableThreadPool = default;
            windowsThreadPoolType = null;

            ClrModule bcl = _runtime.BaseClassLibrary;
            ClrType? threadPoolType = bcl.GetTypeByName("System.Threading.ThreadPool");
            if (threadPoolType is null)
                return;

            windowsThreadPoolType = bcl.GetTypeByName("System.Threading.WindowsThreadPool");
            ClrType? portableThreadPoolType = bcl.GetTypeByName("System.Threading.PortableThreadPool");

            // Check if the Windows thread pool is being used
            ClrStaticField? useWindowsThreadPool = threadPoolType.GetStaticFieldByName("s_useWindowsThreadPool");
            bool useWindowsThreadPoolOnSwitch = useWindowsThreadPool != null && useWindowsThreadPool.Read<bool>(domain);
            bool onlyWindowsThreadPool = windowsThreadPoolType != null && portableThreadPoolType is null;
            // For Windows thread pool, check if the switch is on (.NET8) or if it's the only thread pool present
            if (useWindowsThreadPoolOnSwitch || onlyWindowsThreadPool)
            {
                usingWindowsThreadPool = true;
                return;
            }

            // Check if the Portable thread pool is being used
            if (portableThreadPoolType != null)
            {
                ClrStaticField? instanceField = portableThreadPoolType.GetStaticFieldByName("ThreadPoolInstance");
                if (instanceField is null)
                    return;

                portableThreadPool = instanceField.ReadObject(domain);
                usingPortableThreadPool = !portableThreadPool.IsNull && portableThreadPool.IsValid;
            }
        }

        private ClrAppDomain GetDomain() => _runtime.SharedDomain ?? _runtime.SystemDomain ?? _runtime.AppDomains[0];

    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativeHillClimbingLogEntry
    {
        public readonly int TickCount;
        public readonly HillClimbingTransition Transition;
        public readonly int NewControlSetting;
        public readonly int LastHistoryCount;
        public readonly float LastHistoryMean;
    }

    /// <summary>
    /// An entry in the HillClimbing log.
    /// </summary>
    public class HillClimbingLogEntry
    {
        /// <summary>
        /// The tick count of this entry.
        /// </summary>
        public int TickCount { get; }

        /// <summary>
        /// The new state.
        /// </summary>
        public HillClimbingTransition StateOrTransition { get; }

        /// <summary>
        /// The new control setting.
        /// </summary>
        public int NewThreadCount { get; }

        /// <summary>
        /// The last history count.
        /// </summary>
        public int SampleCount { get; }

        /// <summary>
        /// The last history mean.
        /// </summary>
        public float Throughput { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public HillClimbingLogEntry(int tickCount, HillClimbingTransition stateOrTransition, int newThreadCount, int sampleCount, float throughput)
        {
            TickCount = tickCount;
            StateOrTransition = stateOrTransition;
            NewThreadCount = newThreadCount;
            SampleCount = sampleCount;
            Throughput = throughput;
        }

        internal HillClimbingLogEntry(NativeHillClimbingLogEntry entry)
        {
            TickCount = entry.TickCount;
            StateOrTransition = entry.Transition;
            NewThreadCount = entry.NewControlSetting;
            SampleCount = entry.LastHistoryCount;
            Throughput = entry.LastHistoryMean;
        }
    }
}