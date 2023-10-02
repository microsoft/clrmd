// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class CrossOSTest
    {
        [WindowsFact]
        public void LinuxDebugTest()
        {
            string artifacts = TestTargets.GetTestArtifactFolder();
            Assert.NotNull(artifacts);

            string core = Path.Combine(artifacts, "arrays_wks_mini.coredump");
            Assert.True(File.Exists(core));

            using DataTarget dt = DataTarget.LoadDump(core);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrAppDomain domain = Assert.Single(runtime.AppDomains);

            Assert.Equal("clrhost", domain.Name);
            Assert.Contains("/home/leculver/clrmd/src/TestTargets/bin/x64/Arrays.dll", domain.Modules.Select(m => m.Name));
        }

        [Fact]
        public void SingleFileTest()
        {
            string artifacts = TestTargets.GetTestArtifactFolder();
            Assert.NotNull(artifacts);

            string dump = Path.Combine(artifacts, "single_file_full.dmp");
            Assert.True(File.Exists(dump));

            using DataTarget dt = DataTarget.LoadDump(dump);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrAppDomain domain = Assert.Single(runtime.AppDomains);

            Assert.Equal("clrhost", domain.Name);
            Assert.Contains("single-file.dll", domain.Modules.Select(m => m.Name));
        }
    }
}
