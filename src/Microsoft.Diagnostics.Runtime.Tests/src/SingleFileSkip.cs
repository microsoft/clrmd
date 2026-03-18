// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Helper for conditionally skipping tests at runtime for single-file limitations.
    /// Uses xUnit's dynamic skip convention ($XunitDynamicSkip$ message prefix).
    /// </summary>
    internal static class SingleFileSkip
    {
        /// <summary>
        /// Skips the current test if singleFile is true, with the given reason.
        /// </summary>
        public static void SkipIf(bool singleFile, string reason)
        {
            if (singleFile)
                throw new Exception("$XunitDynamicSkip$Single-file: " + reason);
        }
    }
}
