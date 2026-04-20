// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Marks a test that only runs under the HighBit harness (32-bit Windows).
    /// Skipped on all other platforms/architectures because the low-memory
    /// reservation trick is meaningless there.
    /// </summary>
    internal sealed class HighBitFactAttribute : FactAttribute
    {
        public HighBitFactAttribute()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Skip = "HighBit tests are Windows-only.";
            else if (RuntimeInformation.ProcessArchitecture != Architecture.X86)
                Skip = "HighBit tests require a 32-bit test host.";
        }
    }
}
