// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

// Test target for stress log integration tests. The runtime is launched with
// the stress log enabled via DOTNET_StressLog / COMPlus_StressLog env vars,
// then this program forces several full, compacting, blocking GCs. Those GCs
// emit GcStart/GcEnd plus per-relocation messages into the stress log, which
// the test then parses out of the dump.
//
// The program ends with a deliberate unhandled exception so the test harness
// (createdump on Linux/macOS, DbgEng on Windows) captures a process dump.
class Program
{
    static void Main()
    {
        // Build a graph that has both reachable and unreachable references so
        // a compacting GC actually has work to relocate. Use varied object
        // sizes to encourage gen-2 promotion and at least one plug move.
        const int RootCount = 256;
        object[] roots = new object[RootCount];

        Random rng = new Random(0x5712);
        for (int i = 0; i < RootCount; i++)
        {
            int size = 16 + rng.Next(0, 256);
            byte[] payload = new byte[size];
            rng.NextBytes(payload);

            // Make some roots a small tree so there are interior references
            // for the GC to walk and (potentially) relocate.
            List<object> children = new List<object>(4);
            for (int j = 0; j < 4; j++)
            {
                children.Add(new byte[8 + rng.Next(0, 32)]);
            }

            roots[i] = new object[] { payload, children, "stresslog-" + i };
        }

        // Drop half of the roots so the next collection has live + dead mix.
        for (int i = 0; i < RootCount; i += 2)
        {
            roots[i] = null;
        }

        // Force two full, compacting, blocking GCs. Compacting + blocking is
        // the strongest signal that the GC heap will produce relocate /
        // plug-move stress log entries.
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        // Keep the survivors live across the collection so the JIT cannot
        // optimize the roots array away.
        GC.KeepAlive(roots);

        // Throw to trigger a crash dump from the harness.
        throw new Exception("StressLog test target intentional crash for dump capture.");
    }
}
