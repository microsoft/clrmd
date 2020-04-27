// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Utilities;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ModuleTests
    {
        [Fact]
        public void FileVersionInfoVersionTest()
        {
            bool found = false;

            // Make sure we never return different values for the version
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            foreach (ModuleInfo module in dt.EnumerateModules())
            {
                if (!module.IsManaged)
                    continue;

                PEImage img = module.GetPEImage();
                Assert.NotNull(img);

                FileVersionInfo fileVersionInfo = img.GetFileVersionInfo();
                if (fileVersionInfo != null)
                {
                    VersionInfo moduleVersion = module.Version;
                    Assert.Equal(moduleVersion, fileVersionInfo.VersionInfo);
                    found = true;
                }
            }

            Assert.True(found, "Did not find any modules with non-null GetFileVersionInfo to compare");
        }

        [FrameworkFact]
        public void NoDuplicateModules()
        {
            // Modules should have a unique .Address.
            // https://github.com/microsoft/clrmd/issues/440

            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            HashSet<ulong> seen = new HashSet<ulong> { 0 };
            HashSet<ClrModule> seenModules = new HashSet<ClrModule> { null };
            foreach (ClrModule module in runtime.EnumerateModules())
            {
                Assert.True(seenModules.Add(module));
                Assert.True(seen.Add(module.Address));
            }
        }

        [Fact]
        public void TestGetTypeByName()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
            ClrHeap heap = runtime.Heap;

            ClrModule shared = runtime.GetModule("sharedlibrary.dll");
            Assert.NotNull(shared.GetTypeByName("Foo"));
            Assert.Null(shared.GetTypeByName("Types"));

            ClrModule types = runtime.GetModule(TypeTests.ModuleName);
            Assert.NotNull(types.GetTypeByName("Types"));
            Assert.Null(types.GetTypeByName("Foo"));
        }

        [Fact]
        public void ModuleEqualityTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule[] oldModules = runtime.EnumerateModules().ToArray();
            Assert.NotEmpty(oldModules);

            runtime.FlushCachedData();

            ClrModule[] newModules = runtime.EnumerateModules().ToArray();
            Assert.Equal(oldModules.Length, newModules.Length);

            for (int i = 0; i < newModules.Length; i++)
            {
                Assert.Equal(oldModules[i], newModules[i]);
                Assert.NotSame(oldModules[i], newModules[i]);
            }
        }

        [CoreFact]
        public void TestModuleSize()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            foreach (ClrModule module in runtime.EnumerateModules())
            {
                Assert.True(module.IsDynamic || module.Size > 0);
            }
        }

        [Fact]
        public void TestTypeMapRoundTrip()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            int badTypes = 0;
            foreach (ClrModule module in runtime.EnumerateModules())
            {
                foreach ((ulong mt, int token) in module.EnumerateTypeDefToMethodTableMap())
                {
                    Assert.NotEqual(0, token);
                    Assert.True((token & 0x02000000) == 0x02000000);

                    ClrType type = runtime.GetTypeByMethodTable(mt);
                    if (type == null)
                    {
                        // We really want to Assert.NotNull(type), but it turns out that one type
                        // (System.Runtime.Remoting.Proxies.__TransparentProxy) cannot be constructed because
                        // GetMethodTableData returns null for it.  This is an issue with the dac so we'll
                        // simply count types that are null and assert there's only one

                        badTypes++;

                        continue;
                    }

                    Assert.NotNull(type);

                    ClrType typeFromToken = module.ResolveToken(token);
                    Assert.NotNull(typeFromToken);

                    Assert.Same(type, typeFromToken);
                }
            }

            Assert.True(badTypes <= 1);
        }
    }
}
