// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.DataReaders.Implementation;
using Microsoft.Diagnostics.Runtime.Linux;
using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            using SOSDac dac = runtime.DacLibrary.SOSDacInterface;


            foreach (ClrThread thread in runtime.Threads)
            {
                if (!thread.IsAlive)
                    continue;

                Assert.NotEqual(0u, thread.OSThreadId);

                ulong teb = threadReader.GetThreadTeb(thread.OSThreadId);
                Assert.NotEqual(0ul, teb);

                if (dac.GetThreadData(thread.Address, out ThreadData threadData))
                    Assert.Equal((ulong)threadData.Teb, teb);
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

            var items = threadReader.EnumerateOSThreadIds().ToArray();

            uint mainThreadId = runtime.GetMainThread().OSThreadId;
            Assert.Equal(mainThreadId, threadReader.EnumerateOSThreadIds().First());
        }

        [Fact]
        public void ElfCoreSymbolTests()
        {
            ElfCoreFile dump = new ElfCoreFile(File.OpenRead(@"D:\linux_core\GCRoot_wks.dmp"));
            ElfLoadedImage image = dump.LoadedImages.Values.FirstOrDefault((image) => image.Path.Contains("libcoreclr.so"));
            Assert.NotNull(image);

            ElfFile file = image.Open();
            Assert.NotNull(file);

            Assert.True(file.DynamicSection.TryLookupSymbol("g_dacTable", out ElfSymbol symbol));
            Assert.NotEqual(0, symbol.Value);
        }
    }
}
