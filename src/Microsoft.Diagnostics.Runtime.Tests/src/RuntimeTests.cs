using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class RuntimeTests
    {
        [Fact]
        public void CreationSpecificDacNegativeTest()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
            {
                string badDac = dt.SymbolLocator.FindBinary(SymbolLocatorTests.WellKnownDac, SymbolLocatorTests.WellKnownDacTimeStamp, SymbolLocatorTests.WellKnownDacImageSize, false);

                Assert.NotNull(badDac);
                
                Assert.Throws<InvalidOperationException>(() => dt.ClrVersions.Single().CreateRuntime(badDac));
            }
        }

        [Fact]
        public void CreationSpecificDac()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
            {
                ClrInfo info = dt.ClrVersions.Single();
                string dac = info.LocalMatchingDac;

                Assert.NotNull(dac);

                ClrRuntime runtime = info.CreateRuntime(dac);
                Assert.NotNull(runtime);
            }
        }


        [Fact]
        public void RuntimeClrInfo()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
            {
                ClrInfo info = dt.ClrVersions.Single();
                ClrRuntime runtime = info.CreateRuntime();

                Assert.Equal(info, runtime.ClrInfo);
            }
        }

        [Fact]
        public void ModuleEnumerationTest()
        {
            // This test ensures that we enumerate all modules in the process exactly once.

            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                HashSet<string> expected = new HashSet<string>(new string[] { "mscorlib.dll", "sharedlibrary.dll", "nestedexception.exe", "appdomains.exe" }, StringComparer.OrdinalIgnoreCase);
                HashSet<ClrModule> modules = new HashSet<ClrModule>();

                foreach (ClrModule module in runtime.Modules)
                {
                    Assert.Contains(Path.GetFileName(module.FileName), expected);
                    Assert.DoesNotContain(module, modules);
                    modules.Add(module);
                }
            }
        }
    }
}
