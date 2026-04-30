// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct TimeVal64
    {
        public long Seconds;
        // Field name is historical; this is actually microseconds (suseconds_t)
        // matching the Linux struct timeval.tv_usec field.
        public long Milliseconds;

        public TimeSpan? ToTimeSpan()
        {
            try
            {
                return TimeSpan.FromTicks(checked(Seconds * TimeSpan.TicksPerSecond + Milliseconds * 10L));
            }
            catch (OverflowException)
            {
                return null;
            }
        }
    }
}