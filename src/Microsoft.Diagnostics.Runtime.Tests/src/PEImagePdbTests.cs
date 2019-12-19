// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class PEImagePdbTests
    {
        [FrameworkFact]
        public void ManagedPdbTest()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            ModuleInfo clrModule = dt.EnumerateModules().SingleOrDefault(m => Path.GetFileNameWithoutExtension(m.FileName).Equals("clr", StringComparison.OrdinalIgnoreCase));

            PEImage img = clrModule.GetPEImage();
            Assert.NotNull(img);

            PdbInfo imgPdb = img.DefaultPdb;
            Assert.NotNull(imgPdb);
            Assert.NotNull(imgPdb.FileName);
        }

        [FrameworkFact]
        public void WindowsNativePdbTest()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Load Windows' ntdll.dll
                var dllFileName = Path.Combine(Environment.SystemDirectory, "ntdll.dll");
                using (var winFileStream = new FileStream(dllFileName, FileMode.Open, FileAccess.Read))
                {
                    PEImage img = new PEImage(winFileStream);
                    Assert.NotNull(img);

                    PdbInfo imgPdb = img.DefaultPdb;
                    Assert.NotNull(imgPdb);
                    Assert.NotNull(imgPdb.FileName);
                }
            }
        }
    }
}
