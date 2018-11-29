// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// The type of work item this is.
    /// </summary>
    public enum WorkItemKind
    {
        /// <summary>
        /// Unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// Callback for an async timer.
        /// </summary>
        AsyncTimer,

        /// <summary>
        /// Async callback.
        /// </summary>
        AsyncCallback,

        /// <summary>
        /// From ThreadPool.QueueUserWorkItem.
        /// </summary>
        QueueUserWorkItem,

        /// <summary>
        /// Timer delete callback.
        /// </summary>
        TimerDelete
    }

    /// <summary>
    /// Provides information about CLR's threadpool.
    /// </summary>
    public abstract class ClrThreadPool
    {
        /// <summary>
        /// The total number of threadpool worker threads in the process.
        /// </summary>
        abstract public int TotalThreads { get; }

        /// <summary>
        /// The number of running threadpool threads in the process.
        /// </summary>
        abstract public int RunningThreads { get; }

        /// <summary>
        /// The number of idle threadpool threads in the process.
        /// </summary>
        abstract public int IdleThreads { get; }

        /// <summary>
        /// The minimum number of threadpool threads allowable.
        /// </summary>
        abstract public int MinThreads { get; }

        /// <summary>
        /// The maximum number of threadpool threads allowable.
        /// </summary>
        abstract public int MaxThreads { get; }

        /// <summary>
        /// Returns the minimum number of completion ports (if any).
        /// </summary>
        abstract public int MinCompletionPorts { get; }

        /// <summary>
        /// Returns the maximum number of completion ports.
        /// </summary>
        abstract public int MaxCompletionPorts { get; }

        /// <summary>
        /// Returns the CPU utilization of the threadpool (as a percentage out of 100).
        /// </summary>
        abstract public int CpuUtilization { get; }

        /// <summary>
        /// The number of free completion port threads.
        /// </summary>
        abstract public int FreeCompletionPortCount { get; }

        /// <summary>
        /// The maximum number of free completion port threads.
        /// </summary>
        abstract public int MaxFreeCompletionPorts { get; }

        /// <summary>
        /// Enumerates the work items on the threadpool (native side).
        /// </summary>
        abstract public IEnumerable<NativeWorkItem> EnumerateNativeWorkItems();

        /// <summary>
        /// Enumerates work items on the thread pool (managed side).
        /// </summary>
        /// <returns></returns>
        abstract public IEnumerable<ManagedWorkItem> EnumerateManagedWorkItems();
    }
}
