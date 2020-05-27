// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DumpAnalyzer.Library.Utility
{
    internal class DisposerQueue : IDisposable
    {
        private BlockingCollection<IDisposable> collection;
        private List<Thread> disposerThreads;

        internal static readonly DisposerQueue Shared = new DisposerQueue();

        private class DisposerWithTCS : IDisposable
        {
            private IDisposable toDispose;
            private readonly TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            private Action afterDisposalCallback;

            internal DisposerWithTCS(IDisposable toDispose, Action afterDisposalCallback)
            {
                this.toDispose = toDispose;
                this.afterDisposalCallback = afterDisposalCallback;
            }

            internal Task Task => this.tcs.Task;

            public void Dispose()
            {
                try
                {
                    this.toDispose.Dispose();
                    if(this.afterDisposalCallback != null)
                    {
                        this.afterDisposalCallback();
                    }
                }
                finally
                {
                    this.tcs.SetResult(true);
                }
            }
        }

        internal DisposerQueue()
        {
            this.collection = new BlockingCollection<IDisposable>(new ConcurrentQueue<IDisposable>());

            int threadCount = Environment.ProcessorCount - 1;

            this.disposerThreads = new List<Thread>(threadCount);
            for(int i = 0; i < threadCount; i++)
            {
                // We can go with small thread stacks as they wil never be doing complex work that needs a lot of stack, 100k should be fine
                Thread t = new Thread(ThreadProc, maxStackSize: (1 << 10) * 100)
                {
                    Name = $"Disposer Thread #{i + 1}",
                    IsBackground = true
                };
                t.Start(this.collection);

                this.disposerThreads.Add(t);
            }
        }

        internal void Enqueue(IDisposable disposable)
        {
            this.collection.Add(disposable);
        }

        internal Task EnqueueWithTask(IDisposable disposable)
        {
           return EnqueueWithTask(disposable, afterDisposalCallback: null);
        }

        internal Task EnqueueWithTask(IDisposable disposable, Action afterDisposalCallback)
        {
            DisposerWithTCS disposer = new DisposerWithTCS(disposable, afterDisposalCallback);
            this.collection.Add(disposer);

            return disposer.Task;
        }

        private static void ThreadProc(object obj)
        {
            BlockingCollection<IDisposable> collection = (BlockingCollection<IDisposable>)obj;
            try
            {

                while (true)
                {
                    IDisposable toDispose = collection.Take();
                    toDispose.Dispose();
                }
            }
            catch (Exception)
            { }
        }

        public void Dispose()
        {
            this.collection?.Dispose();
            this.collection = null;
        }
    }
}