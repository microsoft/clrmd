// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Diagnostics.Runtime.Implementation;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class FileLocatorTests
    {
        public static readonly string WellKnownDac = "mscordacwks_X86_X86_4.6.96.00.dll";
        public static readonly int WellKnownDacTimeStamp = 0x55b96946;
        public static readonly int WellKnownDacImageSize = 0x006a8000;

        public static readonly string WellKnownNativePdb = "clr.pdb";
        public static readonly Guid WellKnownNativePdbGuid = new("0350aa66-2d49-4425-ab28-9b43a749638d");
        public static readonly int WellKnownNativePdbAge = 2;

        internal static IFileLocator GetLocator()
        {
            return SymbolGroup.CreateFromSymbolPath($"srv*{Helpers.TestWorkingDirectory}*http://msdl.microsoft.com/download/symbols", true, null);
        }

        [Fact(Skip = "Touches network, don't run regularly.")]
        public void FindBinaryNegativeTest()
        {
            IFileLocator locator = GetLocator();
            string dac = locator.FindPEImage(WellKnownDac, WellKnownDacTimeStamp + 1, WellKnownDacImageSize + 1, false);
            Assert.Null(dac);
        }


        [Fact(Skip = "Touches network, don't run regularly.")]
        public void FindBinaryTest()
        {
            IFileLocator locator = GetLocator();
            string dac = locator.FindPEImage(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false);
            Assert.NotNull(dac);
            Assert.True(File.Exists(dac));
        }
    }
}
