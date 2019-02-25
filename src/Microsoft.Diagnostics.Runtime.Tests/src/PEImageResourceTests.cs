using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class PEImageResourceTests
    {
        [Fact]
        public void TestResourceImages()
        {
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                ClrInfo clr = dt.ClrVersions.Single();
                PEImage image = clr.ModuleInfo.GetPEImage();
                ResourceEntry entry = image.Resources;
                WalkEntry(entry);
            }
        }


        private static void WalkEntry(ResourceEntry entry, int depth = 0)
        {
            string depthStr = new string(' ', depth * 4);
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
        struct ClrDebugResource
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
