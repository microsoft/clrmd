// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Utilities;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class BinaryLocatorTests
    {
        public static readonly string WellKnownDac = "mscordacwks_X86_X86_4.6.96.00.dll";
        public static readonly int WellKnownDacTimeStamp = 0x55b96946;
        public static readonly int WellKnownDacImageSize = 0x006a8000;

        public static readonly string WellKnownNativePdb = "clr.pdb";
        public static readonly Guid WellKnownNativePdbGuid = new Guid("0350aa66-2d49-4425-ab28-9b43a749638d");
        public static readonly int WellKnownNativePdbAge = 2;

        internal static IBinaryLocator GetLocator()
        {
            return new SymbolServerLocator($"srv*{Helpers.TestWorkingDirectory}*http://msdl.microsoft.com/download/symbols");
        }

        [Fact(Skip = "Touches network, don't run regularly.")]
        public void FindBinaryNegativeTest()
        {
            IBinaryLocator locator = GetLocator();
            string dac = locator.FindBinary(WellKnownDac, WellKnownDacTimeStamp + 1, WellKnownDacImageSize + 1, false);
            Assert.Null(dac);
        }

        [Fact(Skip = "Touches network, don't run regularly.")]
        public async Task FindBinaryAsyncNegativeTest()
        {
            IBinaryLocator locator = GetLocator();

            List<Task<string>> tasks = new List<Task<string>>();
            for (int i = 0; i < 10; i++)
                tasks.Add(locator.FindBinaryAsync(WellKnownDac, WellKnownDacTimeStamp + 1, WellKnownDacImageSize + 1, false));

            // Ensure we got the same answer for everything.
            foreach (Task<string> task in tasks)
            {
                string dac = await task;
                Assert.Null(dac);
            }
        }

        [Fact(Skip = "Touches network, don't run regularly.")]
        public void FindBinaryTest()
        {
            IBinaryLocator locator = GetLocator();
            string dac = locator.FindBinary(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false);
            Assert.NotNull(dac);
            Assert.True(File.Exists(dac));
        }

        [Fact(Skip = "Touches network, don't run regularly.")]
        public async Task FindBinaryAsyncTest()
        {
            IBinaryLocator locator = GetLocator();
            Task<string> first = locator.FindBinaryAsync(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false);

            List<Task<string>> tasks = new List<Task<string>>();
            for (int i = 0; i < 10; i++)
                tasks.Add(locator.FindBinaryAsync(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false));

            string dac = await first;

            Assert.NotNull(dac);
            Assert.True(File.Exists(dac));
            using (PEImage peimage = new PEImage(File.OpenRead(dac)))
            {
                
                Assert.True(peimage.IsValid);
            }

            // Ensure we got the same answer for everything.
            foreach (Task<string> task in tasks)
            {
                string taskDac = await task;
                Assert.Equal(dac, taskDac);
            }
        }
    }
}
