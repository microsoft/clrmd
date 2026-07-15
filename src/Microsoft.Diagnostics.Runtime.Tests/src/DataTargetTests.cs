// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class DataTargetTests
    {
        [Fact]
        public void SkipRuntimeEnumeration_ClrVersionsIsEmpty()
        {
            // With SkipRuntimeEnumeration set, ClrMD performs no detection and ClrVersions is empty until a
            // host registers a runtime explicitly.
            using DataTarget dt = TestTargets.Types.LoadFullDump(options: new DataTargetOptions { SkipRuntimeEnumeration = true });
            Assert.Empty(dt.ClrVersions);
        }

        [Fact]
        public void AddLoadedRuntime_ReplacesRuntimeWithSameModuleInfo()
        {
            // Registering a runtime for a ModuleInfo that already has one (e.g. ClrMD detected it) replaces
            // the existing entry rather than adding a duplicate.
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            ClrInfo detected = dt.ClrVersions.Single();
            int originalCount = dt.ClrVersions.Length;

            ClrInfo hosted = new(dt, detected.ModuleInfo, detected.Version)
            {
                Flavor = detected.Flavor,
                IsSingleFile = detected.IsSingleFile,
            };

            dt.AddLoadedRuntime(hosted, () => IntPtr.Zero);

            Assert.Equal(originalCount, dt.ClrVersions.Length);
            Assert.Same(hosted, dt.ClrVersions.Single(c => ReferenceEquals(c.ModuleInfo, detected.ModuleInfo)));
            Assert.DoesNotContain(detected, dt.ClrVersions);
        }

        [Fact]
        public void AddLoadedRuntime_ForeignClrDataProcess_MatchesNormalRuntime()
        {
            // Validates the host-supplied IXCLRDataProcess path (used by SOS). We stand in for the host by
            // producing a fresh IXCLRDataProcess from ClrMD's own DAC, then feeding the raw pointer through
            // AddLoadedRuntime and confirming the resulting runtime walks the heap identically.
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            ClrInfo clrInfo = dt.ClrVersions.Single();

            using ClrRuntime baseline = clrInfo.CreateRuntime();
            (ulong Address, string Type)[] expected = baseline.Heap.EnumerateObjects()
                                                               .Take(2000)
                                                               .Select(o => (o.Address, o.Type?.Name ?? ""))
                                                               .ToArray();

            DacLibrary dac = baseline.GetService<DacLibrary>();
            Assert.NotNull(dac);

            // A host would create its own IXCLRDataProcess; here we clone one from the DAC (this AddRefs it).
            using ClrDataProcess process = dac.CreateClrDataProcess();
            IntPtr pClrDataProcess = process.DangerousGetHandle();

            ClrInfo hosted = new(dt, clrInfo.ModuleInfo, clrInfo.Version)
            {
                Flavor = clrInfo.Flavor,
                IsSingleFile = clrInfo.IsSingleFile,
            };

            Assert.Same(hosted, dt.AddLoadedRuntime(hosted, pClrDataProcess));

            using ClrRuntime foreign = hosted.CreateRuntime();
            (ulong Address, string Type)[] actual = foreign.Heap.EnumerateObjects()
                                                             .Take(2000)
                                                             .Select(o => (o.Address, o.Type?.Name ?? ""))
                                                             .ToArray();

            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i].Address, actual[i].Address);
                Assert.Equal(expected[i].Type, actual[i].Type);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EnsureFinalReleaseOfInterfaces(bool singleFile)
        {
            RefCountedFreeLibrary library;

            using (DataTarget dt = TestTargets.Types.LoadFullDump(singleFile))
            {
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;
                _ = heap.EnumerateObjects().Count(); // ensure we warm up and use a bunch of SOSDac interfaces
                DacLibrary dac = runtime.GetService<DacLibrary>();

                library = dac.OwningLibrary;

                // Keep library alive
                library.AddRef();
            }

            Assert.Equal(0, library.Release());
        }

        [WindowsFact]
        public void LoadDump_ThrowsInvalidDataExceptionForEmptyFile()
        {
            string path = Path.GetTempFileName();
            _ = Assert.Throws<InvalidDataException>(() => DataTarget.LoadDump(path));
        }

        [WindowsFact]
        public void LoadDump_StreamDisplayNameDoesNotNeedToBeAFilePath()
        {
            using (DataTarget _ = TestTargets.Types.LoadFullDump())
            {
            }

            string dumpPath = TestTargets.Types.BuildDumpName(GCMode.Workstation, full: true);
            using FileStream stream = File.OpenRead(dumpPath);
            using DataTarget dt = DataTarget.LoadDump("<display name>", stream, options: new DataTargetOptions
            {
                CacheOptions = new CacheOptions
                {
                    MaxDumpCacheSize = 0x200_0000,
                },
            });

            Assert.NotEmpty(dt.EnumerateModules());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ProcessIdIsValid(bool singleFile)
        {
            using DataTarget dt = TestTargets.NestedException.LoadFullDump(singleFile);
            Assert.True(dt.DataReader.ProcessId > 0);
        }
    }
}
