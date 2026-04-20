// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Marks a test that requires the HighBitHost harness. These tests generate and inspect
    /// dumps where the CLR heap has been forced above the 0x80000000 line in a 32-bit process,
    /// so they only run on Windows x86.
    /// </summary>
    internal sealed class HighBitFactAttribute : FactAttribute
    {
        public HighBitFactAttribute()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Skip = "HighBit tests require Windows.";
                return;
            }

            if (RuntimeInformation.ProcessArchitecture != Architecture.X86)
            {
                Skip = "HighBit tests require a 32-bit (x86) test host.";
            }
        }
    }
}
