// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Benchmarks
{
    internal static class BenchmarkSwitches
    {
        public static IEnumerable<bool> OSMemoryFeatureFlags
        {
            get
            {
                yield return false;

                // Currently the only time the CacheOptions.UseOSMemoryFeatures flag makes a behavioral change is
                // on Windows and when AWE is enabled.  We will skip that part of the test if the user isn't running
                // in that environment so that the tests don't run as long and so that the results aren't confused
                // as having tested the AWE reader when we didn't.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && AweHelpers.IsAweEnabled)
                    yield return true;
            }
        }

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
