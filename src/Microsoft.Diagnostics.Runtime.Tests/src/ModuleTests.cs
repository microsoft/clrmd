// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ModuleTests
    {
        [Fact]
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
    }
}