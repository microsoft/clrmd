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
    public class AppDomainTests
    {
        [Fact]
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
                Assert.False(moduleDomains.Contains(domain));
        }

        private void AssertModuleContainsDomains(ClrModule module, params ClrAppDomain[] domainList)
        {
            IList<ClrAppDomain> moduleDomains = module.AppDomains;

            foreach (ClrAppDomain domain in domainList)
                Assert.True(moduleDomains.Contains(domain));

            Assert.Equal(domainList.Length, moduleDomains.Count);
        }

        [Fact]
        public void AppDomainPropertyTest()
        {
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrAppDomain systemDomain = runtime.SystemDomain;
                Assert.Equal("System Domain", systemDomain.Name);
                Assert.NotEqual(0ul, systemDomain.Address);

                ClrAppDomain sharedDomain = runtime.SharedDomain;
                Assert.Equal("Shared Domain", sharedDomain.Name);
                Assert.NotEqual(0ul, sharedDomain.Address);

                Assert.NotEqual(systemDomain.Address, sharedDomain.Address);

                Assert.Equal(2, runtime.AppDomains.Count);
                ClrAppDomain AppDomainsExe = runtime.AppDomains[0];
                Assert.Equal("AppDomains.exe", AppDomainsExe.Name);
                Assert.Equal(1, AppDomainsExe.Id);

                ClrAppDomain NestedExceptionExe = runtime.AppDomains[1];
                Assert.Equal("Second AppDomain", NestedExceptionExe.Name);
                Assert.Equal(2, NestedExceptionExe.Id);
            }
        }

        [Fact]
        public void SystemAndSharedLibraryModulesTest()
        {
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrAppDomain systemDomain = runtime.SystemDomain;
                Assert.Equal(0, systemDomain.Modules.Count);

                ClrAppDomain sharedDomain = runtime.SharedDomain;

                if (runtime.ClrInfo.Version.Major == 2)
                {
                    Assert.Equal(3, sharedDomain.Modules.Count);
                    Assert.Contains("mscorlib.dll", sharedDomain.Modules.Select(m => Path.GetFileName("mscorlib.dll")));
                }
                else
                {
                    Assert.Equal(1, sharedDomain.Modules.Count);

                    ClrModule mscorlib = sharedDomain.Modules.Single();
                    Assert.Equal("mscorlib.dll", Path.GetFileName(mscorlib.FileName), true);
                }
            }
        }

        [Fact]
        public void ModuleAppDomainEqualityTest()
        {
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrAppDomain appDomainsExe = runtime.GetDomainByName("AppDomains.exe");
                ClrAppDomain nestedExceptionExe = runtime.GetDomainByName("Second AppDomain");

                Dictionary<string, ClrModule> appDomainsModules = GetDomainModuleDictionary(appDomainsExe);

                Assert.True(appDomainsModules.ContainsKey("appdomains.exe"));
                Assert.True(appDomainsModules.ContainsKey("mscorlib.dll"));
                Assert.True(appDomainsModules.ContainsKey("sharedlibrary.dll"));

                Assert.False(appDomainsModules.ContainsKey("nestedexception.exe"));

                Dictionary<string, ClrModule> nestedExceptionModules = GetDomainModuleDictionary(nestedExceptionExe);

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
            Dictionary<string, ClrModule> result = new Dictionary<string, ClrModule>(StringComparer.OrdinalIgnoreCase);
            foreach (ClrModule module in domain.Modules)
                result.Add(Path.GetFileName(module.FileName), module);

            return result;
        }
    }
}