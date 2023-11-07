// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacImplementation;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.DataReaders.Implementation;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ThreadReaderTests
    {
        [WindowsFact]
        public void GetThreadTebTest()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDumpWithDbgEng();
            IThreadReader threadReader = (IThreadReader)dt.DataReader;
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            IAbstractRuntime runtimeService = runtime.GetService<IAbstractRuntime>();
            Assert.NotNull(runtimeService);

            ClrThreadInfo[] threads = runtimeService.EnumerateThreads().ToArray();

            foreach (ClrThread thread in runtime.Threads)
            {
                if (!thread.IsAlive)
                    continue;

                Assert.NotEqual(0u, thread.OSThreadId);

                ulong teb = threadReader.GetThreadTeb(thread.OSThreadId);
                Assert.NotEqual(0ul, teb);

                ClrThreadInfo curr = Assert.Single(threads, r => r.OSThreadId == thread.OSThreadId);
                Assert.Equal(curr.Teb, teb);
            }
        }

        [Fact]
        public void EnumerateOSThreadIdsTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            IThreadReader threadReader = (IThreadReader)dt.DataReader;
            uint[] threads = threadReader.EnumerateOSThreadIds().ToArray();

            Assert.NotEmpty(threads);
            Assert.DoesNotContain(0u, threads);

            // no duplicates
            Assert.Equal(threads.Length, new HashSet<uint>(threads).Count);

            foreach (uint threadId in runtime.Threads.Select(f => f.OSThreadId).Where(id => id != 0))
                Assert.Contains(threadId, threads);
        }

        [Fact]
        public void EnsureOSThreadOrdering()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            IThreadReader threadReader = (IThreadReader)dt.DataReader;

            uint[] items = threadReader.EnumerateOSThreadIds().ToArray();

            uint mainThreadId = runtime.GetMainThread().OSThreadId;
            Assert.Equal(mainThreadId, threadReader.EnumerateOSThreadIds().First());
        }
    }
}
