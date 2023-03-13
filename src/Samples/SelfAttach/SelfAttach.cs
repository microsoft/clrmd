// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Attaching to your own live process to inspect it is not a supported operation in ClrMD.
// Instead we offer a way to create a snapshot of your current process and attach to that
// snapshot.  On Windows we use PssCreateSnapshot which will create a snapshot to inspect
// within a few hundred milliseconds.  On Linux we don't have this OS feature, so instead
// we will create a coredump of the process in the user's tmp folder, read from that dump
// then delete the coredump when the user calls DataTarget.Dispose.  On Linux this operation
// takes significantly longer (~2-10 seconds or so).

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

namespace DesktopTest
{
    class SelfAttach
    {
        static void Main()
        {
            using DataTarget dt = DataTarget.CreateSnapshotAndAttach(Process.GetCurrentProcess().Id);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            foreach (ClrThread thread in runtime.Threads)
            {
                Console.WriteLine($"{thread.OSThreadId:x}:");
                foreach (ClrStackFrame frame in thread.EnumerateStackTrace())
                    Console.WriteLine($"    {frame}");
            }
        }
    }
}
