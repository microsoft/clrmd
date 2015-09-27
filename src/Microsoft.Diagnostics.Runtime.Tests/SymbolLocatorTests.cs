
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Diagnostics.Runtime.Utilities;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Diagnostics.Runtime.Utilities.Pdb;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    [TestClass]
    public class SymbolLocatorTests
    {
        static public readonly string WellKnownDac = "mscordacwks_X86_X86_4.6.96.00.dll";
        static public readonly int WellKnownDacTimeStamp = 0x55b96946;
        static public readonly int WellKnownDacImageSize = 0x006a8000;

        static public readonly string WellKnownNativePdb = "clr.pdb";
        static public readonly Guid WellKnownNativePdbGuid = new Guid("0350aa66-2d49-4425-ab28-9b43a749638d");
        static public readonly int WellKnownNativePdbAge = 2;

        static internal SymbolLocator GetLocator()
        {
            string tmpPath = Helpers.TestWorkingDirectory;

            SymbolLocator locator = new DefaultSymbolLocator();
            locator.SymbolCache = tmpPath;
            return locator;
        }

        [TestMethod]
        public void SymbolLocatorTimeoutNegativeTest()
        {
            var locator = GetLocator();
            locator.Timeout = 1;
            locator.SymbolCache += "\\TestTimeoutNegative";

            string pdb = locator.FindPdb(WellKnownNativePdb, WellKnownNativePdbGuid, WellKnownNativePdbAge);
            Assert.IsNull(pdb);
        }

        [TestMethod]
        public void SymbolLocatorTimeoutTest()
        {
            var locator = GetLocator();
            locator.Timeout = 10000;
            locator.SymbolCache += "\\TestTimeout";

            string pdb = locator.FindPdb(WellKnownNativePdb, WellKnownNativePdbGuid, WellKnownNativePdbAge);
            Assert.IsNotNull(pdb);
        }

        [TestMethod]
        public void FindBinaryNegativeTest()
        {
            SymbolLocator _locator = GetLocator();
            string dac = _locator.FindBinary(WellKnownDac, WellKnownDacTimeStamp + 1, WellKnownDacImageSize + 1, false);
            Assert.IsNull(dac);
        }

        [TestMethod]
        public void FindPdbNegativeTest()
        {
            SymbolLocator _locator = GetLocator();
            string pdb = _locator.FindPdb(WellKnownNativePdb, WellKnownNativePdbGuid, WellKnownNativePdbAge + 1);
            Assert.IsNull(pdb);
        }
        [TestMethod]
        public async Task FindBinaryAsyncNegativeTest()
        {
            SymbolLocator _locator = GetLocator();

            List<Task<string>> tasks = new List<Task<string>>();
            for (int i = 0; i < 10; i++)
                tasks.Add(_locator.FindBinaryAsync(WellKnownDac, WellKnownDacTimeStamp + 1, WellKnownDacImageSize + 1, false));
            
            // Ensure we got the same answer for everything.
            foreach (var task in tasks)
            {
                string dac = await task;
                Assert.IsNull(dac);
            }
        }

        [TestMethod]
        public async Task FindPdbAsyncNegativeTest()
        {
            SymbolLocator _locator = GetLocator();

            List<Task<string>> tasks = new List<Task<string>>();
            for (int i = 0; i < 10; i++)
                tasks.Add(_locator.FindPdbAsync(WellKnownNativePdb, WellKnownNativePdbGuid, WellKnownNativePdbAge + 1));
            
            // Ensure we got the same answer for everything.
            foreach (var task in tasks)
            {
                string pdb = await task;
                Assert.IsNull(pdb);
            }
        }

        [TestMethod]
        public void FindBinaryTest()
        {
            SymbolLocator _locator = GetLocator();
            string dac = _locator.FindBinary(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false);
            Assert.IsNotNull(dac);
            Assert.IsTrue(File.Exists(dac));
        }

        [TestMethod]
        public void FindPdbTest()
        {
            SymbolLocator _locator = GetLocator();
            string pdb = _locator.FindPdb(WellKnownNativePdb, WellKnownNativePdbGuid, WellKnownNativePdbAge);
            Assert.IsNotNull(pdb);
            Assert.IsTrue(File.Exists(pdb));

            Assert.IsTrue(PdbMatches(pdb, WellKnownNativePdbGuid, WellKnownNativePdbAge));
        }

        static bool PdbMatches(string pdb, Guid guid, int age)
        {
            Guid fileGuid;
            int fileAge;
            PdbReader.GetPdbProperties(pdb, out fileGuid, out fileAge);

            return guid == fileGuid && age == fileAge;
        }

        [TestMethod]
        public async Task FindBinaryAsyncTest()
        {
            SymbolLocator _locator = GetLocator();
            Task<string> first = _locator.FindBinaryAsync(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false);

            List<Task<string>> tasks = new List<Task<string>>();
            for (int i = 0; i < 10; i++)
                tasks.Add(_locator.FindBinaryAsync(WellKnownDac, WellKnownDacTimeStamp, WellKnownDacImageSize, false));

            string dac = await first;

            Assert.IsNotNull(dac);
            Assert.IsTrue(File.Exists(dac));
            new PEFile(dac).Dispose();  // This will throw if the image is invalid.

            // Ensure we got the same answer for everything.
            foreach (var task in tasks)
            {
                string taskDac = await task;
                Assert.AreEqual(dac, taskDac);
            }
        }


        [TestMethod]
        public async Task FindPdbAsyncTest()
        {
            SymbolLocator _locator = GetLocator();
            Task<string> first = _locator.FindPdbAsync(WellKnownNativePdb, WellKnownNativePdbGuid, WellKnownNativePdbAge);

            List<Task<string>> tasks = new List<Task<string>>();
            for (int i = 0; i < 10; i++)
                tasks.Add(_locator.FindPdbAsync(WellKnownNativePdb, WellKnownNativePdbGuid, WellKnownNativePdbAge));

            string pdb = await first;
            
            Assert.IsNotNull(pdb);
            Assert.IsTrue(File.Exists(pdb));
            Assert.IsTrue(PdbMatches(pdb, WellKnownNativePdbGuid, WellKnownNativePdbAge));

            // Ensure we got the same answer for everything.
            foreach (var task in tasks)
            {
                string taskPdb = await task;
                Assert.AreEqual(taskPdb, pdb);
            }
        }
    }
}
