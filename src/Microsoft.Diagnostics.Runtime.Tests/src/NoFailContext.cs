// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    internal sealed class NoFailContext : IDisposable
    {
        private static readonly NoFailListener s_noFailListener = new NoFailListener();
        private static readonly HashSet<int> s_hookedThreads = new HashSet<int>();
        private static TraceListener s_defaultListener;

        public NoFailContext()
        {
            _ = s_hookedThreads.Add(Thread.CurrentThread.ManagedThreadId);
            if (s_hookedThreads.Count == 1)
            {
                _ = Trace.Listeners.Add(s_noFailListener);
                s_defaultListener = Trace.Listeners["Default"];
                Trace.Listeners.Remove(s_defaultListener);
            }
        }

        // todo:  This on the finalizer thread tears down the process
        //~NoFailContext() => throw new InvalidOperationException();

        public void Dispose()
        {
            _ = s_hookedThreads.Remove(Thread.CurrentThread.ManagedThreadId);
            if (s_hookedThreads.Count == 0)
            {
                _ = Trace.Listeners.Add(s_defaultListener);
                Trace.Listeners.Remove(s_noFailListener);
                s_defaultListener = null;
            }
        }

        private sealed class NoFailListener : TraceListener
        {
            public override void Fail(string message, string detailMessage)
            {
                Assert.True(false, detailMessage);

                if (s_hookedThreads.Contains(Thread.CurrentThread.ManagedThreadId))
                    s_defaultListener.Fail(message, detailMessage);

                throw new DebugFailException(message);
            }

            public override void Write(string message) => s_defaultListener.Write(message);

            public override void WriteLine(string message) => s_defaultListener.WriteLine(message);

            private sealed class DebugFailException : Exception
            {
                public DebugFailException(string message) : base(message)
                {
                }
            }
        }
    }
}
