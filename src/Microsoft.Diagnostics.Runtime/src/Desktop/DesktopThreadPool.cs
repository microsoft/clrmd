// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopThreadPool : ClrThreadPool
    {
        private readonly DesktopRuntimeBase _runtime;
        private ClrHeap _heap;

        public DesktopThreadPool(DesktopRuntimeBase runtime, IThreadPoolData data)
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

        public override int TotalThreads { get; }
        public override int RunningThreads { get; }
        public override int IdleThreads { get; }
        public override int MinThreads { get; }
        public override int MaxThreads { get; }

        public override IEnumerable<NativeWorkItem> EnumerateNativeWorkItems()
        {
            return _runtime.EnumerateWorkItems();
        }

        public override IEnumerable<ManagedWorkItem> EnumerateManagedWorkItems()
        {
            foreach (var obj in EnumerateManagedThreadpoolObjects())
            {
                if (obj != 0)
                {
                    var type = _heap.GetObjectType(obj);
                    if (type != null)
                        yield return new DesktopManagedWorkItem(type, obj);
                }
            }
        }

        private IEnumerable<ulong> EnumerateManagedThreadpoolObjects()
        {
            _heap = _runtime.Heap;

            var mscorlib = GetMscorlib();
            if (mscorlib != null)
            {
                var queueType = mscorlib.GetTypeByName("System.Threading.ThreadPoolGlobals");
                if (queueType != null)
                {
                    var workQueueField = queueType.GetStaticFieldByName("workQueue");
                    if (workQueueField != null)
                    {
                        foreach (var appDomain in _runtime.AppDomains)
                        {
                            var workQueueValue = workQueueField.GetValue(appDomain);
                            var workQueue = workQueueValue == null ? 0L : (ulong)workQueueValue;
                            var workQueueType = _heap.GetObjectType(workQueue);

                            if (workQueue == 0 || workQueueType == null)
                                continue;

                            ulong queueHead;
                            do
                            {
                                if (!GetFieldObject(workQueueType, workQueue, "queueHead", out var queueHeadType, out queueHead))
                                    break;

                                if (GetFieldObject(queueHeadType, queueHead, "nodes", out var nodesType, out var nodes) && nodesType.IsArray)
                                {
                                    var len = nodesType.GetArrayLength(nodes);
                                    for (var i = 0; i < len; ++i)
                                    {
                                        var addr = (ulong)nodesType.GetArrayElementValue(nodes, i);
                                        if (addr != 0)
                                            yield return addr;
                                    }
                                }

                                if (!GetFieldObject(queueHeadType, queueHead, "Next", out queueHeadType, out queueHead))
                                    break;
                            } while (queueHead != 0);
                        }
                    }
                }

                queueType = mscorlib.GetTypeByName("System.Threading.ThreadPoolWorkQueue");
                if (queueType != null)
                {
                    var threadQueuesField = queueType.GetStaticFieldByName("allThreadQueues");
                    if (threadQueuesField != null)
                    {
                        foreach (var domain in _runtime.AppDomains)
                        {
                            var threadQueue = (ulong?)threadQueuesField.GetValue(domain);
                            if (!threadQueue.HasValue || threadQueue.Value == 0)
                                continue;

                            var threadQueueType = _heap.GetObjectType(threadQueue.Value);
                            if (threadQueueType == null)
                                continue;

                            if (!GetFieldObject(threadQueueType, threadQueue.Value, "m_array", out var outerArrayType, out var outerArray) || !outerArrayType.IsArray)
                                continue;

                            var outerLen = outerArrayType.GetArrayLength(outerArray);
                            for (var i = 0; i < outerLen; ++i)
                            {
                                var entry = (ulong)outerArrayType.GetArrayElementValue(outerArray, i);
                                if (entry == 0)
                                    continue;

                                var entryType = _heap.GetObjectType(entry);
                                if (entryType == null)
                                    continue;

                                if (!GetFieldObject(entryType, entry, "m_array", out var arrayType, out var array) || !arrayType.IsArray)
                                    continue;

                                var len = arrayType.GetArrayLength(array);
                                for (var j = 0; j < len; ++j)
                                {
                                    var addr = (ulong)arrayType.GetArrayElementValue(array, i);
                                    if (addr != 0)
                                        yield return addr;
                                }
                            }
                        }
                    }
                }
            }
        }

        private ClrModule GetMscorlib()
        {
            foreach (var module in _runtime.Modules)
                if (module.AssemblyName.Contains("mscorlib.dll"))
                    return module;

            // Uh oh, this shouldn't have happened.  Let's look more carefully (slowly).
            foreach (var module in _runtime.Modules)
                if (module.AssemblyName.ToLower().Contains("mscorlib"))
                    return module;

            // Ok...not sure why we couldn't find it.
            return null;
        }

        private bool GetFieldObject(ClrType type, ulong obj, string fieldName, out ClrType valueType, out ulong value)
        {
            value = 0;
            valueType = null;

            var field = type.GetFieldByName(fieldName);
            if (field == null)
                return false;

            value = (ulong)field.GetValue(obj);
            if (value == 0)
                return false;

            valueType = _heap.GetObjectType(value);
            return valueType != null;
        }

        public override int MinCompletionPorts { get; }
        public override int MaxCompletionPorts { get; }
        public override int CpuUtilization { get; }
        public override int FreeCompletionPortCount { get; }
        public override int MaxFreeCompletionPorts { get; }
    }
}