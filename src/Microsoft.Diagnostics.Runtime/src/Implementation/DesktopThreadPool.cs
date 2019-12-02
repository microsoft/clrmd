// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    internal class DesktopThreadPool : ClrThreadPool
    {
        private readonly ClrmdRuntime _runtime;
        private ClrHeap _heap;

        public DesktopThreadPool(ClrmdRuntime runtime, IThreadPoolData data)
        {
            _runtime = runtime;
            TotalThreads = data.TotalThreads;
            RunningThreads = data.RunningThreads;
            IdleThreads = data.IdleThreads;
            MinThreads = data.MinThreads;
            MaxThreads = data.MaxThreads;
            MinCompletionPorts = data.MinCP;
            MaxCompletionPorts = data.MaxCP;
            CpuUtilization = data.CPU;
            FreeCompletionPortCount = data.NumFreeCP;
            MaxFreeCompletionPorts = data.MaxFreeCP;
        }


        public override int MinCompletionPorts { get; }
        public override int MaxCompletionPorts { get; }
        public override int CpuUtilization { get; }
        public override int FreeCompletionPortCount { get; }
        public override int MaxFreeCompletionPorts { get; }
        public override int TotalThreads { get; }
        public override int RunningThreads { get; }
        public override int IdleThreads { get; }
        public override int MinThreads { get; }
        public override int MaxThreads { get; }

        //TODO
        public override IEnumerable<NativeWorkItem> EnumerateNativeWorkItems() => throw new NotImplementedException();

        public override IEnumerable<ManagedWorkItem> EnumerateManagedWorkItems()
        {
            foreach (ulong obj in EnumerateManagedThreadpoolObjects())
            {
                if (obj != 0)
                {
                    ClrType type = _heap.GetObjectType(obj);
                    if (type != null)
                        yield return new DesktopManagedWorkItem(type, obj);
                }
            }
        }

        private IEnumerable<ulong> EnumerateManagedThreadpoolObjects()
        {
            _heap = _runtime.Heap;

            ClrModule mscorlib = _runtime.BaseClassLibrary;
            if (mscorlib != null)
            {
                ClrType queueType = mscorlib.GetTypeByName("System.Threading.ThreadPoolGlobals");
                if (queueType != null)
                {
                    ClrStaticField workQueueField = queueType.GetStaticFieldByName("workQueue");
                    if (workQueueField != null)
                    {
                        foreach (ClrAppDomain appDomain in _runtime.AppDomains)
                        {
                            object workQueueValue = workQueueField.GetValue(appDomain);
                            ulong workQueue = workQueueValue == null ? 0L : (ulong)workQueueValue;
                            ClrType workQueueType = _heap.GetObjectType(workQueue);

                            if (workQueue == 0 || workQueueType == null)
                                continue;

                            ClrObject queueHead = new ClrObject(workQueue, workQueueType).GetObjectField("queueHead");
                            while (!queueHead.IsNull)
                            {
                                ClrObject nodes = queueHead.GetObjectField("nodes");
                                if (!nodes.IsNull)
                                {
                                    int len = nodes.Length;
                                    for (int i = 0; i < len; i++)
                                    {
                                        ulong addr = (ulong)nodes.Type.GetArrayElementValue(nodes, i);
                                        if (addr != 0)
                                            yield return addr;
                                    }

                                }

                                queueHead = queueHead.GetObjectField("Next");
                            }
                        }
                    }
                }

                queueType = mscorlib.GetTypeByName("System.Threading.ThreadPoolWorkQueue");
                if (queueType != null)
                {
                    ClrStaticField threadQueuesField = queueType.GetStaticFieldByName("allThreadQueues");
                    if (threadQueuesField != null)
                    {
                        foreach (ClrAppDomain domain in _runtime.AppDomains)
                        {
                            ulong? threadQueueAddress = (ulong?)threadQueuesField.GetValue(domain);
                            if (!threadQueueAddress.HasValue || threadQueueAddress.Value == 0)
                                continue;

                            ClrType threadQueueType = _heap.GetObjectType(threadQueueAddress.Value);
                            if (threadQueueType == null)
                                continue;

                            ClrObject threadQueue = new ClrObject(threadQueueAddress.Value, threadQueueType);
                            ClrObject outerArray = threadQueue.GetObjectField("m_array");

                            if (outerArray.IsNull)
                                continue;

                            int outerLen = outerArray.Length;
                            for (int i = 0; i < outerLen; ++i)
                            {
                                ulong entry = (ulong)outerArray.Type.GetArrayElementValue(outerArray, i);
                                if (entry == 0)
                                    continue;

                                ClrType entryType = _heap.GetObjectType(entry);
                                if (entryType == null)
                                    continue;

                                ClrObject array = outerArray.GetObjectField("m_array");
                                if (array.IsNull)
                                    continue;

                                int len = array.Length;
                                for (int j = 0; j < len; ++j)
                                {
                                    ulong addr = (ulong)array.Type.GetArrayElementValue(array, i);
                                    if (addr != 0)
                                        yield return addr;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}