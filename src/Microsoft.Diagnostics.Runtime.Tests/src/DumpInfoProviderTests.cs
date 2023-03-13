// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class DumpInfoProviderTests
    {
        [WindowsFact]
        public void MiniDumpIsMiniTest()
        {
            using DataTarget dt = TestTargets.NestedException.LoadMinidump();
            IDumpInfoProvider dumpInfo = Assert.IsAssignableFrom<IDumpInfoProvider>(dt.DataReader);
            Assert.True(dumpInfo.IsMiniOrTriage);
        }

        [WindowsFact]
        public void MiniDumpIsNotMiniTest()
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump();
            IDumpInfoProvider dumpInfo = Assert.IsAssignableFrom<IDumpInfoProvider>(dt.DataReader);
            Assert.False(dumpInfo.IsMiniOrTriage);
        }
    }
}
