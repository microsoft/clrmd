// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime.Tests.Fixtures;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Runs all ClrObjectTests against a single-file dump.
    /// Inherits every test from ClrObjectTests but uses SingleFileClrObjectConnection.
    /// </summary>
    public class SingleFileClrObjectTests : ClrObjectTests, IClassFixture<SingleFileClrObjectConnection>
    {
        public SingleFileClrObjectTests(SingleFileClrObjectConnection connection) : base(connection)
        {
        }
    }
}
