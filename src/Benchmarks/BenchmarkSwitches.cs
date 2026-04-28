// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Benchmarks
{
    internal static class BenchmarkSwitches
    {
        public static IEnumerable<long> RelevantCacheSizes
        {
            get
            {
                // x86 default, 128meg
                yield return 0x800_0000;

                // 1gb
                yield return 0x4000_0000;

                // x64 default, 4gb
                if (IntPtr.Size == 8)
                    yield return 0x1_0000_0000;
            }
        }

        public static IEnumerable<int> Parallelism => new int[] { 1, 4, 8 };
    }
}
