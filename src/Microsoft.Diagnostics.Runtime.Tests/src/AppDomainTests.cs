using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class AppDomainTests
    {
        [Fact]
        public void ModuleDomainTest()
        {
            using (var dt = TestTargets.AppDomains.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();

                var appDomainExe = runtime.GetDomainByName("AppDomains.exe");
                var nestedDomain = runtime.GetDomainByName("Second AppDomain");

                var mscorlib = runtime.GetModule("mscorlib.dll");
                AssertModuleContainsDomains(mscorlib, runtime.SharedDomain, appDomainExe, nestedDomain);
                AssertModuleDoesntContainDomains(mscorlib, runtime.SystemDomain);

                // SharedLibrary.dll is loaded into both domains but not as shared library like mscorlib.
                // This means it will not be in the shared domain.
                var sharedLibrary = runtime.GetModule("sharedlibrary.dll");
                AssertModuleContainsDomains(sharedLibrary, appDomainExe, nestedDomain);
                AssertModuleDoesntContainDomains(sharedLibrary, runtime.SharedDomain, runtime.SystemDomain);

                var appDomainsExeModule = runtime.GetModule("AppDomains.exe");
                AssertModuleContainsDomains(appDomainsExeModule, appDomainExe);
                AssertModuleDoesntContainDomains(appDomainsExeModule, runtime.SystemDomain, runtime.SharedDomain, nestedDomain);

                var nestedExeModule = runtime.GetModule("NestedException.exe");
                AssertModuleContainsDomains(nestedExeModule, nestedDomain);
                AssertModuleDoesntContainDomains(nestedExeModule, runtime.SystemDomain, runtime.SharedDomain, appDomainExe);
            }
        }

        private void AssertModuleDoesntContainDomains(ClrModule module, params ClrAppDomain[] domainList)
        {
            var moduleDomains = module.AppDomains;

            foreach (var domain in domainList)
                Assert.False(moduleDomains.Contains(domain));
        }

        private void AssertModuleContainsDomains(ClrModule module, params ClrAppDomain[] domainList)
        {
            var moduleDomains = module.AppDomains;

            foreach (var domain in domainList)
                Assert.True(moduleDomains.Contains(domain));

            Assert.Equal(domainList.Length, moduleDomains.Count);
        }

        [Fact]
        public void AppDomainPropertyTest()
        {
            using (var dt = TestTargets.AppDomains.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();

                var systemDomain = runtime.SystemDomain;
                Assert.Equal("System Domain", systemDomain.Name);
                Assert.NotEqual(0ul, systemDomain.Address);

                var sharedDomain = runtime.SharedDomain;
                Assert.Equal("Shared Domain", sharedDomain.Name);
                Assert.NotEqual(0ul, sharedDomain.Address);

                Assert.NotEqual(systemDomain.Address, sharedDomain.Address);

                Assert.Equal(2, runtime.AppDomains.Count);
                var AppDomainsExe = runtime.AppDomains[0];
                Assert.Equal("AppDomains.exe", AppDomainsExe.Name);
                Assert.Equal(1, AppDomainsExe.Id);

                var NestedExceptionExe = runtime.AppDomains[1];
                Assert.Equal("Second AppDomain", NestedExceptionExe.Name);
                Assert.Equal(2, NestedExceptionExe.Id);
            }
        }

        [Fact]
        public void SystemAndSharedLibraryModulesTest()
        {
            using (var dt = TestTargets.AppDomains.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();

                var systemDomain = runtime.SystemDomain;
                Assert.Equal(0, systemDomain.Modules.Count);

                var sharedDomain = runtime.SharedDomain;
                Assert.Equal(1, sharedDomain.Modules.Count);

                var mscorlib = sharedDomain.Modules.Single();
                Assert.Equal("mscorlib.dll", Path.GetFileName(mscorlib.FileName), true);
            }
        }

        [Fact]
        public void ModuleAppDomainEqualityTest()
        {
            using (var dt = TestTargets.AppDomains.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();

                var appDomainsExe = runtime.GetDomainByName("AppDomains.exe");
                var nestedExceptionExe = runtime.GetDomainByName("Second AppDomain");

                var appDomainsModules = GetDomainModuleDictionary(appDomainsExe);

                Assert.True(appDomainsModules.ContainsKey("appdomains.exe"));
                Assert.True(appDomainsModules.ContainsKey("mscorlib.dll"));
                Assert.True(appDomainsModules.ContainsKey("sharedlibrary.dll"));

                Assert.False(appDomainsModules.ContainsKey("nestedexception.exe"));

                var nestedExceptionModules = GetDomainModuleDictionary(nestedExceptionExe);

                Assert.True(nestedExceptionModules.ContainsKey("nestedexception.exe"));
                Assert.True(nestedExceptionModules.ContainsKey("mscorlib.dll"));
                Assert.True(nestedExceptionModules.ContainsKey("sharedlibrary.dll"));

                Assert.False(nestedExceptionModules.ContainsKey("appdomains.exe"));

                // Ensure that we use the same ClrModule in each AppDomain.
                Assert.Equal(appDomainsModules["mscorlib.dll"], nestedExceptionModules["mscorlib.dll"]);
                Assert.Equal(appDomainsModules["sharedlibrary.dll"], nestedExceptionModules["sharedlibrary.dll"]);
            }
        }

        private static Dictionary<string, ClrModule> GetDomainModuleDictionary(ClrAppDomain domain)
        {
            var result = new Dictionary<string, ClrModule>(StringComparer.OrdinalIgnoreCase);
            foreach (var module in domain.Modules)
                result.Add(Path.GetFileName(module.FileName), module);

            return result;
        }
    }
}