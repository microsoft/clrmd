using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    [TestClass]
    public class GCHandleTests
    {
        [TestMethod]
        public void EnsureEnumerationStability()
        {
            // I made some changes to v4.5 handle enumeration to enumerate handles out faster.
            // This test makes sure I have a stable enumeration.
            using (DataTarget dt = TestTargets.GCHandles.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                List<ClrHandle> handles = new List<ClrHandle>();
                
                bool cont;
                do
                {
                    cont = false;
                    int i = 0;
                    foreach (var hnd in runtime.EnumerateHandles())
                    {
                        if (i > handles.Count)
                            break;

                        if (i == handles.Count)
                        {
                            cont = true;
                            handles.Add(hnd);
                            break;
                        }

                        Assert.AreEqual(handles[i++], hnd);
                    }
                } while (cont);

                // We create at least this many handles in the test, plus the runtime uses some.
                Assert.IsTrue(handles.Count > 4);
            }
        }
    }
}
