// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime.Tests.Fixtures;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Runs all BasicArrayTests against a single-file dump.
    /// Inherits every test from BasicArrayTests but uses SingleFileArrayConnection.
    /// </summary>
    public class SingleFileBasicArrayTests : BasicArrayTests, IClassFixture<SingleFileArrayConnection>
    {
        public SingleFileBasicArrayTests(SingleFileArrayConnection connection) : base(connection)
        {
        }
    }
}
