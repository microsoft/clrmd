// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class RuntimeTests
    {
        [Fact]
        public void CreationSpecificDacNegativeTest()
        {
            using (var dt = TestTargets.NestedException.LoadFullDump())
            {
                var badDac = dt.SymbolLocator.FindBinary(
                    SymbolLocatorTests.WellKnownDac,
                    SymbolLocatorTests.WellKnownDacTimeStamp,
                    SymbolLocatorTests.WellKnownDacImageSize,
                    false);

                Assert.NotNull(badDac);
                Assert.Throws<InvalidOperationException>(() => dt.ClrVersions.Single().CreateRuntime(badDac));
            }
        }

        [Fact]
        public void CreationSpecificDac()
        {
            using (var dt = TestTargets.NestedException.LoadFullDump())
            {
                var info = dt.ClrVersions.Single();
                var dac = info.LocalMatchingDac;

                Assert.NotNull(dac);

                var runtime = info.CreateRuntime(dac);
                Assert.NotNull(runtime);
            }
        }

        [Fact]
        public void RuntimeClrInfo()
        {
            using (var dt = TestTargets.NestedException.LoadFullDump())
            {
                var info = dt.ClrVersions.Single();
                var runtime = info.CreateRuntime();

                Assert.Equal(info, runtime.ClrInfo);
            }
        }

        [Fact]
        public void ModuleEnumerationTest()
        {
            // This test ensures that we enumerate all modules in the process exactly once.

            using (var dt = TestTargets.AppDomains.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();

                var expected = new HashSet<string>(new[] {"mscorlib.dll", "sharedlibrary.dll", "nestedexception.exe", "appdomains.exe"}, StringComparer.OrdinalIgnoreCase);
                var modules = new HashSet<ClrModule>();

                foreach (var module in runtime.Modules)
                {
                    Assert.Contains(Path.GetFileName(module.FileName), expected);
                    Assert.DoesNotContain(module, modules);
                    modules.Add(module);
                }
            }
        }
    }
}