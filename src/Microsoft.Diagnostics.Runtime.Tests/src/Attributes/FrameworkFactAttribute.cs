// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    internal sealed class FrameworkFactAttribute : FactAttribute
    {
        public FrameworkFactAttribute()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Skip = "Only supported on .NET Framework";
            }
        }
    }
}
