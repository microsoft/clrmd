// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class PdbTests
    {
        [Fact]
        public void PdbEqualityTest()
        {
            // Ensure all methods in our source file is in the pdb.
            using DataTarget dt = TestTargets.NestedException.LoadFullDump();
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            PdbInfo[] allPdbs = runtime.Modules.Where(m => m.Pdb != null).Select(m => m.Pdb).ToArray();
            Assert.True(allPdbs.Length > 1);

            for (int i = 0; i < allPdbs.Length; i++)
            {
                Assert.True(allPdbs[i].Equals(allPdbs[i]));
                for (int j = i + 1; j < allPdbs.Length; j++)
                {
                    Assert.False(allPdbs[i].Equals(allPdbs[j]));
                    Assert.False(allPdbs[j].Equals(allPdbs[i]));
                }
            }
        }
    }
}