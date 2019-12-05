using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ParallelStressTest
{
    static class Program
    {
        const int Iterations = 500;
        const int Threads = 8;
        private static ClrObject[] _expectedObjects;
        private static ClrSegment[] _segments;
        private static readonly ManualResetEvent _event = new ManualResetEvent(initialState: false);

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

            TimeSpan elapsed = default;
            for (int i = 0; i < Iterations; i++)
            {
                Console.Write($"\rIteration: {i + 1:n0} {elapsed}");

                dt.DataReader.FlushCachedData();
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

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

                _event.Reset();
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
            _event.WaitOne();

            if (_segments.Length != runtime.Heap.Segments.Count)
                throw new InvalidOperationException($"Segment count mismatch.  Expected {_segments.Length} segments, found {runtime.Heap.Segments.Count}.");

            for (int i = 0; i < _segments.Length; i++)
                if (runtime.Heap.Segments[i].FirstObject != _segments[i].FirstObject || runtime.Heap.Segments[i].CommittedEnd != _segments[i].CommittedEnd)
                    throw new InvalidOperationException($"Segment[{i}] range [{runtime.Heap.Segments[i].FirstObject:x12}-{runtime.Heap.Segments[i].CommittedEnd:x12}], expected [{_segments[i].FirstObject:x12}-{_segments[i].CommittedEnd:x12}]");


            int count = 0;
            IEnumerator<ClrObject> enumerator = runtime.Heap.EnumerateObjects().GetEnumerator();
            while (enumerator.MoveNext())
            {
                ClrObject curr = enumerator.Current;
                if (curr.Address != _expectedObjects[count].Address)
                    throw new InvalidOperationException($"Object {count} was incorrect address: Expected {_expectedObjects[count].Address:x12}, got {curr.Address:x12}");

                if (curr.Type != _expectedObjects[count].Type)
                    throw new InvalidOperationException($"Object {count} was incorrect type: Expected {_expectedObjects[count].Type.Name}, got {curr.Type?.Name ?? ""}");

                count++;
            }

            if (count != _expectedObjects.Count())
                throw new InvalidOperationException($"Expected {_expectedObjects.Length:n0} objects, found {count:n0}.");
        }
    }
}
