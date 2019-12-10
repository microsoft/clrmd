using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Implementation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ParallelStressTest
{
    static class Program
    {
        const bool BackgroundClear = true;
        const int Iterations = int.MaxValue;
        const int Threads = 8;
        static readonly object _sync = new object();
        private static ClrObject[] _expectedObjects;
        private static ClrSegment[] _segments;
        private static readonly ManualResetEvent _event = new ManualResetEvent(initialState: false);

        private static volatile ClrRuntime _runtimeForClearing;

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Must specify a crash dump to inspect.");
                Environment.Exit(1);
            }

            using DataTarget dt = DataTarget.LoadCrashDump(args[0]);
            using (ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime())
            {
                _expectedObjects = runtime.Heap.EnumerateObjects().ToArray();
                _segments = runtime.Heap.Segments.ToArray();
            }

            if (BackgroundClear)
            {
                Thread t = new Thread(ClearThreadProc);
                t.Start();
            }

            TimeSpan elapsed = default;
            for (int i = 0; i < Iterations; i++)
            {
                Console.Write($"\rIteration: {i + 1:n0} {elapsed}");

                dt.DataReader.FlushCachedData();
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                lock (_sync)
                    _runtimeForClearing = runtime;

                Thread[] threads = new Thread[Threads];

                for (int j = 0; j < Threads; j++)
                    threads[j] = CreateAndStartThread(runtime);

                Stopwatch sw = new Stopwatch();
                sw.Start();
                _event.Set();

                foreach (Thread thread in threads)
                    thread.Join();

                sw.Stop();
                elapsed = sw.Elapsed;

                lock (_sync)
                    _runtimeForClearing = null;

                _event.Reset();
            }
        }

        private static void ClearThreadProc()
        {
            while (true)
            {
                lock (_sync)
                {
                    if (_runtimeForClearing != null)
                        _runtimeForClearing.FlushCachedData();
                }

                Thread.Sleep(500);
            }
        }

        private static Thread CreateAndStartThread(ClrRuntime runtime)
        {
            Thread t = new Thread(() => WorkerThread(runtime));
            t.Start();

            return t;
        }

        private static void WorkerThread(ClrRuntime runtime)
        {
            ClrmdHeap.LogHeapWalkSteps(32);

            _event.WaitOne();

            if (_segments.Length != runtime.Heap.Segments.Count)
            {
                Fail(false, $"Segment count mismatch.  Expected {_segments.Length} segments, found {runtime.Heap.Segments.Count}.");
            }

            for (int i = 0; i < _segments.Length; i++)
                if (runtime.Heap.Segments[i].FirstObject != _segments[i].FirstObject || runtime.Heap.Segments[i].CommittedEnd != _segments[i].CommittedEnd)
                    Fail(false, $"Segment[{i}] range [{runtime.Heap.Segments[i].FirstObject:x12}-{runtime.Heap.Segments[i].CommittedEnd:x12}], expected [{_segments[i].FirstObject:x12}-{_segments[i].CommittedEnd:x12}]");

            int count = 0;
            IEnumerator<ClrObject> enumerator = runtime.Heap.EnumerateObjects().GetEnumerator();
            while (enumerator.MoveNext())
            {
                ClrObject curr = enumerator.Current;
                if (curr.Address != _expectedObjects[count].Address)
                    Fail(true, $"Object {count} was incorrect address: Expected {_expectedObjects[count].Address:x12}, got {curr.Address:x12}");

                if (curr.Type != _expectedObjects[count].Type)
                    Fail(true, $"Object {count} was incorrect type: Expected {_expectedObjects[count].Type.Name}, got {curr.Type?.Name ?? ""}");

                count++;
            }

            if (count != _expectedObjects.Count())
                Fail(true, $"Expected {_expectedObjects.Length:n0} objects, found {count:n0}.");
        }

        private static void Fail(bool printHeapSteps, string reason)
        {
            lock (_sync)
            {
                Console.WriteLine();
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId:x}");
                Console.WriteLine(reason);

                if (printHeapSteps)
                {
                    int i = ClrmdHeap.Step;
                    do
                    {
                        i = (i + 1) % ClrmdHeap.Steps.Count;
                        HeapWalkStep step = ClrmdHeap.Steps[i];
                        Console.WriteLine($"obj:{step.Address:x12} mt:{step.MethodTable:x12} base:{step.BaseSize:x8} comp:{step.ComponentSize:x8} count:{step.Count:x8}");
                    } while (i != ClrmdHeap.Step);
                }
            }

            Break();
        }


        private static void Break()
        {
            if (Debugger.IsAttached)
                Debugger.Break();

            while (!Debugger.IsAttached)
                Thread.Sleep(1000);
        }
    }
}
