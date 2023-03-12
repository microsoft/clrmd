// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Interfaces;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class InterfaceEqualityTests
    {
        [Fact]
        public void ThreadsAreSame()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            IClrRuntime iruntime = runtime;

            ImmutableArray<ClrThread> threads = runtime.Threads;
            ImmutableArray<IClrThread> ithreads = iruntime.Threads;

            Assert.NotEmpty(threads);
            Assert.Equal(threads.Length, ithreads.Length);
            for (int i = 0; i < threads.Length; i++)
                Assert.Same(threads[i], ithreads[i]);
        }

        [Fact]
        public void AppDomainsAreSame()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            IClrRuntime iruntime = runtime;

            ImmutableArray<ClrAppDomain> domains = runtime.AppDomains;
            ImmutableArray<IClrAppDomain> idomains = iruntime.AppDomains;

            Assert.NotEmpty(domains);
            Assert.Equal(domains.Length, idomains.Length);
            for (int i = 0; i < domains.Length; i++)
                Assert.Same(domains[i], idomains[i]);
        }

        [Fact]
        public void ObjectsAreSame()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            IClrRuntime iruntime = runtime;
            ClrHeap heap = runtime.Heap;
            IClrHeap iheap = heap;

            ClrObject[] objs = heap.EnumerateObjects().ToArray();
            IClrValue[] iobjs = iheap.EnumerateObjects().ToArray();

            Assert.NotEmpty(objs);
            Assert.Equal(objs.Length, iobjs.Length);

            for (int i = 0; i < objs.Length; i++)
            {
                Assert.Equal(objs[i].Address, iobjs[i].Address);
                Assert.Same(objs[i].Type, iobjs[i].Type);
                Assert.Equal(objs[i], iobjs[i]);
            }
        }
    }
}
