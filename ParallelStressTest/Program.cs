using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelStressTest
{
    static class Program
    {
        const int Iterations = 500;
        const int Threads = 8;
        private static ClrObject[] _expectedObjects;
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
            }


            for (int i = 0; i < Iterations; i++)
            {
                Console.Write($"\rIteration: {i + 1:n0}");
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                List<Task<IReadOnlyList<ClrObject>>> tasks = new List<Task<IReadOnlyList<ClrObject>>>();

                for (int j = 0; j < Threads; j++)
                    tasks.Add(CreateAndStartThread(runtime));

                _event.Set();


                while (tasks.Count > 0)
                {
                    int index = Task.WaitAny(tasks.ToArray());
                    var curr = tasks[index].Result;
                    tasks.RemoveAt(index);

                    if (_expectedObjects.Length != curr.Count)
                        throw new InvalidOperationException($"Expected objects enumerated was {_expectedObjects.Length:n0}, got {curr.Count:n0}");

                    for (int j = 0; j < _expectedObjects.Length; j++)
                    {
                        if (_expectedObjects[j].Address != curr[j].Address)
                            throw new InvalidOperationException($"Object {j} was incorrect address: Expected {_expectedObjects[j].Address:x}, got {curr[j].Address:x}");

                        if (_expectedObjects[j].Type != curr[j].Type)
                            throw new InvalidOperationException($"Object {j} was incorrect type: Expected {_expectedObjects[j].Type.Name}, got {curr[j].Type?.Name ?? ""}");
                    }
                }


                _event.Reset();
            }
        }

        private static Task<IReadOnlyList<ClrObject>> CreateAndStartThread(ClrRuntime runtime)
        {
            TaskCompletionSource<IReadOnlyList<ClrObject>> source = new TaskCompletionSource<IReadOnlyList<ClrObject>>();

            Thread t = new Thread(() => WorkerThread(runtime, source));
            t.Start();

            return source.Task;
        }

        private static void WorkerThread(ClrRuntime runtime, TaskCompletionSource<IReadOnlyList<ClrObject>> source)
        {
            _event.WaitOne();
            try
            {
                ClrObject[] objects = runtime.Heap.EnumerateObjects().ToArray();
                source.SetResult(objects);
            }
            catch (Exception e)
            {
                source.SetException(e);
            }
        }
    }
}
