// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class PEImageResourceTests
    {
        [FrameworkFact]
        public void FileInfoVersionTest()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            ModuleInfo clrModule = dt.EnumerateModules().SingleOrDefault(m => Path.GetFileNameWithoutExtension(m.FileName).Equals("clr", StringComparison.OrdinalIgnoreCase));

            using PEImage img = clrModule.GetPEImage();
            Assert.NotNull(img);

            FileVersionInfo fileVersion = img.GetFileVersionInfo();
            Assert.NotNull(fileVersion);
            Assert.NotNull(fileVersion.FileVersion);

            ClrInfo clrInfo = dt.ClrVersions[0];
            Assert.Contains(clrInfo.Version.ToString(), fileVersion.FileVersion);
        }

        [FrameworkFact]
        public void TestResourceImages()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            ClrInfo clr = dt.ClrVersions.Single();
            using PEImage image = clr.ModuleInfo.GetPEImage();
            ResourceEntry entry = image.Resources;
            WalkEntry(entry);
        }

        private static void WalkEntry(ResourceEntry entry, int depth = 0)
        {
            foreach (var child in entry.Children)
            {
                WalkEntry(child, depth + 1);

                if (child.Name == "CLRDEBUGINFO")
                {
                    var dbg = child.Children.First().GetData<ClrDebugResource>();

                    Assert.NotEqual(0, dbg.dwDacSizeOfImage);
                    Assert.NotEqual(0, dbg.dwDacTimeStamp);
                    Assert.NotEqual(0, dbg.dwDbiSizeOfImage);
                    Assert.NotEqual(0, dbg.dwDbiTimeStamp);
                    Assert.NotEqual(Guid.Empty, dbg.signature);

                    Assert.Equal(0, dbg.dwDacSizeOfImage & 0xf);
                    Assert.Equal(0, dbg.dwDbiSizeOfImage & 0xf);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ClrDebugResource
        {
            public int dwVersion;
            public Guid signature;
            public int dwDacTimeStamp;
            public int dwDacSizeOfImage;
            public int dwDbiTimeStamp;
            public int dwDbiSizeOfImage;
        }
    }
}
