// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class RuntimeTests
    {
        [WindowsFact]
        public void CreationSpecificDacNegativeTest()
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump();
            string badDac = dt.EnumerateModules().Single(m => Path.GetFileNameWithoutExtension(m.FileName).Equals("clr", StringComparison.OrdinalIgnoreCase)).FileName;

            Assert.Throws<ClrDiagnosticsException>(() => dt.ClrVersions.Single().CreateRuntime(badDac));
        }

        [Fact]
        public void CreationSpecificDac()
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump();
            ClrInfo info = dt.ClrVersions.Single();
            foreach (DebugLibraryInfo dac in info.DebuggingLibraries.Where(r => Path.GetFileName(r.FileName) != r.FileName))
            {
                using ClrRuntime runtime = info.CreateRuntime(dac.FileName);
                Assert.NotNull(runtime);
            }
        }

        [Fact]
        public void RuntimeClrInfo()
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump();
            ClrInfo info = dt.ClrVersions.Single();
            using ClrRuntime runtime = info.CreateRuntime();

            Assert.Equal(info, runtime.ClrInfo);
        }

        [FrameworkFact]
        public void ModuleEnumerationTest()
        {
            // This test ensures that we enumerate all modules in the process exactly once.

            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            HashSet<string> expected = new(new[] { "mscorlib.dll", "system.dll", "system.core.dll", "sharedlibrary.dll", "nestedexception.exe", "appdomains.exe" }, StringComparer.OrdinalIgnoreCase);
            foreach (ClrAppDomain domain in runtime.AppDomains)
            {
                HashSet<ClrModule> modules = new();
                foreach (ClrModule module in domain.Modules)
                {
                    if (Path.GetExtension(module.Name) == ".nlp")
                        continue;

                    Assert.Contains(Path.GetFileName(module.Name), expected);
                    Assert.DoesNotContain(module, modules);
                    modules.Add(module);
                }
            }
        }

        [Fact]
        public void EnsureFlushClearsData()
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrAppDomain oldShared = runtime.SharedDomain;
            ClrAppDomain oldSystem = runtime.SystemDomain;
            System.Collections.Immutable.ImmutableArray<ClrAppDomain> oldDomains = runtime.AppDomains;
            ClrHeap oldHeap = runtime.Heap;
            ClrModule[] oldModules = runtime.EnumerateModules().ToArray();
            ClrObject[] oldObjects = oldHeap.EnumerateObjects().Take(20).ToArray();
            ClrInstanceField[] oldFields = oldObjects.SelectMany(o => o.Type.Fields).ToArray();
            ClrStaticField[] oldStaticFields = oldObjects.SelectMany(o => o.Type.StaticFields).ToArray();
            ClrMethod[] oldMethods = oldObjects.SelectMany(o => o.Type.Methods).ToArray();
            System.Collections.Immutable.ImmutableArray<ClrThread> oldThreads = runtime.Threads;

            // Ensure names are read and cached
            foreach (ClrObject obj in oldObjects)
            {
                _ = obj.Type.Name;
                foreach (ClrMethod item in obj.Type.Methods)
                    _ = item.Name;
                foreach (ClrInstanceField item in obj.Type.Fields)
                    _ = item.Name;
                foreach (ClrStaticField item in obj.Type.StaticFields)
                    _ = item.Name;
            }

            foreach (ClrModule module in oldModules)
            {
                _ = module.Name;
                _ = module.Name;
                _ = module.AssemblyName;
            }

            // Ensure we have some data to compare against
            Assert.NotEmpty(oldDomains);
            Assert.NotEmpty(oldModules);
            Assert.NotEmpty(oldObjects);
            Assert.NotEmpty(oldThreads);
            Assert.NotEmpty(oldFields);
            Assert.NotEmpty(oldStaticFields);
            Assert.NotEmpty(oldMethods);

            // Make sure we aren't regenerating this list every time.
            Assert.Equal(oldDomains, runtime.AppDomains);

            // Clear all cached data.
            runtime.FlushCachedData();

            CheckDomainNotSame(oldShared, runtime.SharedDomain);
            CheckDomainNotSame(oldSystem, runtime.SystemDomain);
            Assert.Equal(oldDomains.Length, runtime.AppDomains.Length);
            for (int i = 0; i < oldDomains.Length; i++)
                CheckDomainNotSame(oldDomains[i], runtime.AppDomains[i]);

            ClrModule[] newModules = runtime.EnumerateModules().ToArray();
            for (int i = 0; i < oldModules.Length; i++)
                CheckModuleNotSame(oldModules[i], newModules[i]);

            ClrHeap newHeap = runtime.Heap;

            ClrObject[] newObjs = newHeap.EnumerateObjects().Take(20).ToArray();
            Assert.Equal(oldObjects.Length, newObjs.Length);
            for (int i = 0; i < oldObjects.Length; i++)
            {
                Assert.Equal(oldObjects[i].Address, newObjs[i].Address);
                CheckTypeNotSame(oldObjects[i].Type, newObjs[i].Type);
            }

            System.Collections.Immutable.ImmutableArray<ClrThread> newThreads = runtime.Threads;
            Assert.Equal(newThreads, runtime.Threads);
            Assert.Equal(oldThreads.Length, newThreads.Length);
            for (int i = 0; i < oldThreads.Length; i++)
            {
                Assert.Equal(oldThreads[i].OSThreadId, newThreads[i].OSThreadId);
                Assert.NotSame(oldThreads[i], newThreads[i]);
            }
        }

        private void CheckTypeNotSame(ClrType oldType, ClrType newType)
        {
            // Don't check that the basic types are different, it's ok to cache those
            if (oldType.IsFree || oldType.IsArray || oldType.IsException || oldType.BaseType == null || oldType.IsString)
                return;

            Assert.Equal(oldType.MethodTable, newType.MethodTable);

            AssertEqualNotSame(oldType.Name, newType.Name);

            for (int i = 0; i < oldType.Fields.Length; i++)
                AssertEqualNotSame(oldType.Fields[i].Name, newType.Fields[i].Name);

            for (int i = 0; i < oldType.StaticFields.Length; i++)
                AssertEqualNotSame(oldType.StaticFields[i].Name, newType.StaticFields[i].Name);

            for (int i = 0; i < oldType.Methods.Length; i++)
                AssertEqualNotSame(oldType.Methods[i].Name, newType.Methods[i].Name);
        }

        private void AssertEqualNotSame(string t1, string t2)
        {
            Assert.Equal(t1, t2);
            Assert.NotSame(t1, t2);
        }

        private void CheckModuleNotSame(ClrModule oldModule, ClrModule newModule)
        {
            // These should be different physical objects, and they should have been enumerated in the same order

            Assert.Equal(oldModule.Address, newModule.Address);
            Assert.NotSame(oldModule, newModule);

            CheckDomainNotSame(oldModule.AppDomain, newModule.AppDomain);

            AssertEqualNotSame(oldModule.Name, newModule.Name);
            AssertEqualNotSame(oldModule.AssemblyName, newModule.AssemblyName);
        }

        private static void CheckDomainNotSame(ClrAppDomain oldDomain, ClrAppDomain domain)
        {
            if (oldDomain != null)
            {
                Assert.Equal(oldDomain.Address, domain.Address);
                Assert.NotSame(oldDomain, domain);
            }
        }
    }
}
