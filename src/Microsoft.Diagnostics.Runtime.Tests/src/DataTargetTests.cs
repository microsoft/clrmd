// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class DataTargetTests
    {
        [Fact]
        public void EnsureFinalReleaseOfInterfaces()
        {
            RefCountedFreeLibrary library;

            using (DataTarget dt = TestTargets.Types.LoadFullDump())
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
    }
}
