
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Diagnostics.Runtime.Utilities;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    [TestClass]
    public class TestSymbolLocator
    {
        private SymbolLocator _locator;

        [TestInitialize]
        public void Initialize()
        {
            string tmpPath = CreateTempPath();

            _locator = new SymbolLocator();
            _locator.SymbolCache = tmpPath;
        }


        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_locator.SymbolCache))
                Directory.Delete(_locator.SymbolCache, true);
        }

        private static string CreateTempPath()
        {
            Random r = new Random();
            string path;
            do
            {
                path = Path.Combine(Path.GetTempPath(), "clrmd_test_" + r.Next().ToString());
            } while (Directory.Exists(path));

            Directory.CreateDirectory(path);
            return path;
        }

        [TestMethod]
        public void TestFindBinary()
        {
            string dac = _locator.FindBinary("mscordacwks_X86_X86_4.6.96.00.dll", 0x55b96946, 0x006a8000, false);
            Assert.IsNotNull(dac);
            Assert.IsTrue(File.Exists(dac));
        }

        //[TestMethod]
        public void TestFindPdb()
        {
            // TODO:  Need to check in msdia so that these tests run out of the box.

            Guid guid = new Guid("0350aa66-2d49-4425-ab28-9b43a749638d");
            int age = 2;
            
            string pdb = _locator.FindPdb("clr.pdb", guid, age);
            Assert.IsNotNull(pdb);
            Assert.IsTrue(File.Exists(pdb));

            Assert.IsTrue(SymbolLocator.PdbMatches(pdb, guid, age));
        }
    }
}
