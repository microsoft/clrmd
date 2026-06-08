// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Diagnostics.Runtime.DacImplementation;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class DacHeapTests
    {
        [Theory]
        [InlineData(ClrFlavor.Desktop, 8, 0)]
        [InlineData(ClrFlavor.Core, 0, 2)]
        [InlineData(ClrFlavor.Core, 7, 0)]
        [InlineData(ClrFlavor.Core, 8, 2)]
        [InlineData(ClrFlavor.Core, 9, 1)]
        public void GetThinLockLayoutSelectsRuntimeLayout(ClrFlavor flavor, int majorVersion, int expected)
        {
            Assert.Equal(expected, (int)DacHeap.GetThinLockLayout(flavor, new Version(majorVersion, 0)));
        }

        [Theory]
        [InlineData(false, 0x00001401u, 1u, 5u)]
        [InlineData(true, 0x00050001u, 1u, 5u)]
        [InlineData(false, 0x000017FFu, 1023u, 5u)]
        [InlineData(true, 0x0005FFFFu, 65535u, 5u)]
        public void GetThinlockDataDecodesRuntimeLayout(bool useLargeThreadIdLayout, uint header, uint expectedThreadId, uint expectedRecursion)
        {
            (uint threadId, uint recursion) = DacHeap.GetThinlockData(header, useLargeThreadIdLayout);

            Assert.Equal(expectedThreadId, threadId);
            Assert.Equal(expectedRecursion, recursion);
        }

        [Theory]
        [InlineData(false, 0x00001401u, true)]
        [InlineData(true, 0x00050001u, true)]
        [InlineData(false, 0x00001400u, false)]
        [InlineData(true, 0x00050000u, false)]
        [InlineData(false, 0x08000001u, false)]
        [InlineData(true, 0x08000001u, false)]
        [InlineData(false, 0x10000001u, false)]
        [InlineData(true, 0x10000001u, false)]
        public void HasThinlockUsesRuntimeLayout(bool useLargeThreadIdLayout, uint header, bool expected)
        {
            Assert.Equal(expected, DacHeap.HasThinlock(header, useLargeThreadIdLayout));
        }
    }
}
