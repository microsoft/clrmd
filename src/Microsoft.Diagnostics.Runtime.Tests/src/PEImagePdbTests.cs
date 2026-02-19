// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Utilities;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class PEImagePdbTests
    {
        [WindowsFact]
        public void ManagedPdbTest()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            PEModuleInfo clrModule = (PEModuleInfo)dt.EnumerateModules().SingleOrDefault(m =>
            {
                string name = Path.GetFileNameWithoutExtension(m.FileName);
                return name.Equals("clr", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("coreclr", StringComparison.OrdinalIgnoreCase);
            });

            Assert.NotNull(clrModule);

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
            string dllFileName = Path.Combine(Environment.SystemDirectory, "ntdll.dll");
            using PEImage img = new(new FileStream(dllFileName, FileMode.Open, FileAccess.Read));
            Assert.NotNull(img);

            PdbInfo imgPdb = img.DefaultPdb;
            Assert.NotNull(imgPdb);
            Assert.NotNull(imgPdb.Path);
        }

        [WindowsFact]
        public void ExportSymbolTest()
        {
            // Load Windows' ntdll.dll
            string dllFileName = Path.Combine(Environment.SystemDirectory, "ntdll.dll");
            using PEImage img = new(new FileStream(dllFileName, FileMode.Open, FileAccess.Read));
            Assert.NotNull(img);

            Assert.True(img.TryGetExportSymbol("DbgBreakPoint", out ulong offset));
            Assert.NotEqual(0UL, offset);
        }
    }
}
