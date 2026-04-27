// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct TimeVal32
    {
        public int Seconds;
        // Field name is historical; this is actually microseconds (suseconds_t)
        // matching the Linux struct timeval.tv_usec field.
        public int Milliseconds;

        public TimeSpan ToTimeSpan() => TimeSpan.FromTicks(Seconds * TimeSpan.TicksPerSecond + Milliseconds * 10L);
    }
}