// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// <see cref="TheoryAttribute"/> sibling of <see cref="WindowsOrNet11FactAttribute"/>.
    /// Runs on Windows unconditionally; on Linux/macOS, only when the runtime version is
    /// 11.0 or higher.
    /// </summary>
    internal sealed class WindowsOrNet11TheoryAttribute : TheoryAttribute
    {
        public WindowsOrNet11TheoryAttribute()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.Version.Major < 11)
            {
                Skip = "Runtime bug: DAC crashes (SIGSEGV) in EnumerateStackRoots on .NET 10 on Linux/macOS. Fixed in .NET 11.";
            }
        }
    }
}
