using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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
    }
}
