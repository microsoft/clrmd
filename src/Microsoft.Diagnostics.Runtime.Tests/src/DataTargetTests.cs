// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class DataTargetTests : IDisposable
    {
        private readonly NoFailContext _context = new NoFailContext();

        public void Dispose() => _context.Dispose();

        [Fact]
        public void SuspendAndAttachToProcess()
        {
            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = TestTargets.Spin.Executable,
                Arguments = "_",
                RedirectStandardOutput = true,
            });

            try
            {
                _ = process.StandardOutput.ReadLine();

                using DataTarget dataTarget = DataTarget.SuspendAndAttachToProcess(process.Id);
                ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
                uint mainThreadId = runtime.GetMainThread().OSThreadId;
                ProcessThread mainThread = process.Threads.Cast<ProcessThread>().Single(thread => thread.Id == mainThreadId);

                Assert.Equal(ThreadState.Wait, mainThread.ThreadState);
            }
            finally
            {
                process.Kill();
            }
        }
    }
}
