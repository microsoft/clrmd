using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    [TestClass]
    public class RuntimeTests
    {
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void CreationSpecificDacNegativeTest()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
            {
                string badDac = dt.SymbolLocator.FindBinary(SymbolLocatorTests.WellKnownDac, SymbolLocatorTests.WellKnownDacTimeStamp, SymbolLocatorTests.WellKnownDacImageSize, false);

                Assert.IsNotNull(badDac);
                
                dt.ClrVersions.Single().CreateRuntime(badDac);

                if (dt.ClrVersions.Single().DacInfo.FileName.Equals(SymbolLocatorTests.WellKnownDac, StringComparison.OrdinalIgnoreCase))
                    Assert.Inconclusive();

            }
        }

        [TestMethod]
        public void CreationSpecificDac()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
            {
                ClrInfo info = dt.ClrVersions.Single();
                string dac = info.LocalMatchingDac;

                Assert.IsNotNull(dac);

                ClrRuntime runtime = info.CreateRuntime(dac);
                Assert.IsNotNull(runtime);
            }
        }


        [TestMethod]
        public void RuntimeClrInfo()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
            {
                ClrInfo info = dt.ClrVersions.Single();
                ClrRuntime runtime = info.CreateRuntime();

                Assert.AreEqual(info, runtime.ClrInfo);
            }
        }

        [TestMethod]
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
                    Assert.IsTrue(expected.Contains(Path.GetFileName(module.FileName)));
                    Assert.IsFalse(modules.Contains(module));
                    modules.Add(module);
                }
            }
        }
    }
}
