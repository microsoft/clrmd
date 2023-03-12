// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Benchmarks
{
    internal sealed class ThreadParallelRunner<T>
    {
        private int _started;
        private Action<T> _action;
        private Thread[] _threads;
        private ManualResetEvent _evt = new ManualResetEvent(false);
        private volatile int _count;
        private List<T>[] _data;

        public ThreadParallelRunner(int count, IEnumerable<T> items)
        {
            if (count <= 0)
                throw new ArgumentException($"{nameof(count)} must be greater than 0", nameof(count));

            _count = count;

            _data = new List<T>[_count];
            for (int i = 0; i < _count; i++)
                _data[i] = new List<T>();

            int j = 0;
            foreach (T item in items)
            {
                _data[j++].Add(item);
                j %= _count;
            }
        }

        public void Setup()
        {
            _started = 0;
            _evt.Reset();
            _threads = new Thread[_count];

            for (int i = 0; i < _count; i++)
            {
                _threads[i] = new Thread(ThreadProc) { Name = $"Action runner i" };
                _threads[i].Start(_data[i]);
            }

            while (_count != _started)
                Thread.Sleep(100);
        }

        public void Run(Action<T> action)
        {
            _action = action;

            if (!_threads[0].IsAlive)
                throw new InvalidOperationException();

            _evt.Set();
            foreach (Thread thread in _threads)
                thread.Join();
        }

        private void ThreadProc(object o)
        {
            Interlocked.Increment(ref _started);
            _evt.WaitOne();

            List<T> items = (List<T>)o;
            foreach (T item in items)
                _action(item);
        }
    }
}
