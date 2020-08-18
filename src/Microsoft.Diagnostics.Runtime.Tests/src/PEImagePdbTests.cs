// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Utilities;
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

            using PEImage img = clrModule.GetPEImage();
            Assert.NotNull(img);

            PdbInfo imgPdb = img.DefaultPdb;
            Assert.NotNull(imgPdb);
            Assert.NotNull(imgPdb.Path);
        }

        [WindowsFact]
        public void WindowsNativePdbTest()
        {
            // Load Windows' ntdll.dll
            var dllFileName = Path.Combine(Environment.SystemDirectory, "ntdll.dll");
            using PEImage img = new PEImage(new FileStream(dllFileName, FileMode.Open, FileAccess.Read));
            Assert.NotNull(img);

            PdbInfo imgPdb = img.DefaultPdb;
            Assert.NotNull(imgPdb);
            Assert.NotNull(imgPdb.Path);
        }
    }
}
