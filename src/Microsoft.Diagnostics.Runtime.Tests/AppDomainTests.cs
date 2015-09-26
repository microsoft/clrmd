using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    [TestClass]
    public class AppDomainTests
    {
        [TestMethod]
        public void AppDomainPropertyTest()
        {
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrAppDomain systemDomain = runtime.SystemDomain;
                Assert.AreEqual("System Domain", systemDomain.Name);
                Assert.AreNotEqual(0, systemDomain.Address);

                ClrAppDomain sharedDomain = runtime.SharedDomain;
                Assert.AreEqual("Shared Domain", sharedDomain.Name);
                Assert.AreNotEqual(0, sharedDomain.Address);

                Assert.AreNotEqual(systemDomain.Address, sharedDomain.Address);

                Assert.AreEqual(2, runtime.AppDomains.Count);
                ClrAppDomain domain0 = runtime.AppDomains[0];
                Assert.AreEqual("AppDomains.exe", domain0.Name);
                Assert.AreEqual(1, domain0.Id);

                ClrAppDomain domain1 = runtime.AppDomains[1];
                Assert.AreEqual("Second AppDomain", domain1.Name);
                Assert.AreEqual(2, domain1.Id);
            }
        }
    }
}
