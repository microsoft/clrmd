using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ElfTests
    {
        [LinuxFact]
        public void ElfCoreSymbolTests()
        {
            var dump = new ElfCoreFile(TestTargets.GCRoot.BuildDumpName(GCMode.Workstation, true));
            ElfLoadedImage image = dump.LoadedImages.Values.FirstOrDefault((image) => image.FileName.Contains("libcoreclr.so"));
            Assert.NotNull(image);

            ElfFile file = image.Open();
            Assert.NotNull(file);

            Assert.True(file.DynamicSection.TryLookupSymbol("g_dacTable", out ElfSymbol symbol));
            Assert.True(symbol.Value == 0x00000000006d01a8);
        }
    }
}
