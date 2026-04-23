// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Runs on Windows unconditionally. On Linux/macOS, only runs when the
    /// runtime version is 11.0 or higher (the DAC crash in EnumerateStackRoots
    /// on .NET 10 was fixed in .NET 11).
    /// </summary>
    internal sealed class WindowsOrNet11FactAttribute : FactAttribute
    {
        public WindowsOrNet11FactAttribute()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.Version.Major < 11)
            {
                Skip = "Runtime bug: DAC crashes (SIGSEGV) in EnumerateStackRoots on .NET 10 on Linux/macOS. Fixed in .NET 11.";
            }
        }
    }
}
