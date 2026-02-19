// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Helper for conditionally skipping tests at runtime for single-file limitations.
    /// Uses xUnit v2 SkipException for dynamic skip support.
    /// Note: Requires xUnit runner support for $XunitDynamicSkip$ token.
    /// </summary>
    internal static class SingleFileSkip
    {
        /// <summary>
        /// Skips the current test if singleFile is true, with the given reason.
        /// </summary>
        public static void SkipIf(bool singleFile, string reason)
        {
            if (singleFile)
                throw new Xunit.Sdk.SkipException("Single-file: " + reason);
        }
    }
}
