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
    public class AppDomainTests
    {
        [TestMethod]
        public void ModuleDomainTest()
        {
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrAppDomain appDomainExe = runtime.GetDomainByName("AppDomains.exe");
                ClrAppDomain nestedDomain = runtime.GetDomainByName("Second AppDomain");

                ClrModule mscorlib = runtime.GetModule("mscorlib.dll");
                AssertModuleContainsDomains(mscorlib, runtime.SharedDomain, appDomainExe, nestedDomain);
                AssertModuleDoesntContainDomains(mscorlib, runtime.SystemDomain);
                
                // SharedLibrary.dll is loaded into both domains but not as shared library like mscorlib.
                // This means it will not be in the shared domain.
                ClrModule sharedLibrary = runtime.GetModule("sharedlibrary.dll");
                AssertModuleContainsDomains(sharedLibrary, appDomainExe, nestedDomain);
                AssertModuleDoesntContainDomains(sharedLibrary, runtime.SharedDomain, runtime.SystemDomain);

                ClrModule appDomainsExeModule = runtime.GetModule("AppDomains.exe");
                AssertModuleContainsDomains(appDomainsExeModule, appDomainExe);
                AssertModuleDoesntContainDomains(appDomainsExeModule, runtime.SystemDomain, runtime.SharedDomain, nestedDomain);


                ClrModule nestedExeModule = runtime.GetModule("NestedException.exe");
                AssertModuleContainsDomains(nestedExeModule, nestedDomain);
                AssertModuleDoesntContainDomains(nestedExeModule, runtime.SystemDomain, runtime.SharedDomain, appDomainExe);
            }
        }

        private void AssertModuleDoesntContainDomains(ClrModule module, params ClrAppDomain[] domainList)
        {
            IList<ClrAppDomain> moduleDomains = module.AppDomains;

            foreach (ClrAppDomain domain in domainList)
                Assert.IsFalse(moduleDomains.Contains(domain));
        }

        private void AssertModuleContainsDomains(ClrModule module, params ClrAppDomain[] domainList)
        {
            IList<ClrAppDomain> moduleDomains = module.AppDomains;
            
            foreach (ClrAppDomain domain in domainList)
                Assert.IsTrue(moduleDomains.Contains(domain));

            Assert.AreEqual(domainList.Length, moduleDomains.Count);
        }

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
                ClrAppDomain AppDomainsExe = runtime.AppDomains[0];
                Assert.AreEqual("AppDomains.exe", AppDomainsExe.Name);
                Assert.AreEqual(1, AppDomainsExe.Id);

                ClrAppDomain NestedExceptionExe = runtime.AppDomains[1];
                Assert.AreEqual("Second AppDomain", NestedExceptionExe.Name);
                Assert.AreEqual(2, NestedExceptionExe.Id);
            }
        }

        [TestMethod]
        public void SystemAndSharedLibraryModulesTest()
        {
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrAppDomain systemDomain = runtime.SystemDomain;
                Assert.AreEqual(0, systemDomain.Modules.Count);

                ClrAppDomain sharedDomain = runtime.SharedDomain;
                Assert.AreEqual(1, sharedDomain.Modules.Count);

                ClrModule mscorlib = sharedDomain.Modules.Single();
                Assert.IsTrue(Path.GetFileName(mscorlib.FileName).Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase));
            }
        }

        [TestMethod]
        public void ModuleAppDomainEqualityTest()
        {
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrAppDomain appDomainsExe = runtime.GetDomainByName("AppDomains.exe");
                ClrAppDomain nestedExceptionExe = runtime.GetDomainByName("Second AppDomain");

                Dictionary<string, ClrModule> appDomainsModules = GetDomainModuleDictionary(appDomainsExe);

                Assert.IsTrue(appDomainsModules.ContainsKey("appdomains.exe"));
                Assert.IsTrue(appDomainsModules.ContainsKey("mscorlib.dll"));
                Assert.IsTrue(appDomainsModules.ContainsKey("sharedlibrary.dll"));

                Assert.IsFalse(appDomainsModules.ContainsKey("nestedexception.exe"));

                Dictionary<string, ClrModule> nestedExceptionModules = GetDomainModuleDictionary(nestedExceptionExe);

                Assert.IsTrue(nestedExceptionModules.ContainsKey("nestedexception.exe"));
                Assert.IsTrue(nestedExceptionModules.ContainsKey("mscorlib.dll"));
                Assert.IsTrue(nestedExceptionModules.ContainsKey("sharedlibrary.dll"));

                Assert.IsFalse(nestedExceptionModules.ContainsKey("appdomains.exe"));

                // Ensure that we use the same ClrModule in each AppDomain.
                Assert.AreEqual(appDomainsModules["mscorlib.dll"], nestedExceptionModules["mscorlib.dll"]);
                Assert.AreEqual(appDomainsModules["sharedlibrary.dll"], nestedExceptionModules["sharedlibrary.dll"]);
            }
        }

        private static Dictionary<string, ClrModule> GetDomainModuleDictionary(ClrAppDomain domain)
        {
            Dictionary<string, ClrModule> result = new Dictionary<string, ClrModule>(StringComparer.OrdinalIgnoreCase);
            foreach (ClrModule module in domain.Modules)
                result.Add(Path.GetFileName(module.FileName), module);

            return result;
        }
    }
}
