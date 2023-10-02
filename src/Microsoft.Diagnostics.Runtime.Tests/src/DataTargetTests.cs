// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class DataTargetTests
    {
        [Fact]
        public void EnsureFinalReleaseOfInterfaces()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();

            RefCountedFreeLibrary library;
            SOSDac sosDac;

            using (ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime())
            {
                DacImplementation runtimeHelpers = (DacImplementation)runtime.DacLibrary;

                library = runtimeHelpers.Library.OwningLibrary;
                sosDac = runtimeHelpers.SOSDacInterface;

                // Keep library alive
                library.AddRef();
            }

            sosDac.Dispose();
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
