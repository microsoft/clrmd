using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class GCHandleTests
    {
        [Fact]
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

                        Assert.Equal(handles[i++], hnd);
                    }
                } while (cont);

                // We create at least this many handles in the test, plus the runtime uses some.
                Assert.True(handles.Count > 4);
            }
        }

        [Fact]
        public void EnsureAllItemsAreUnique()
        {
            // Making sure that handles are returned only once
            var handles = new HashSet<ClrHandle>();

            using (DataTarget dt = TestTargets.GCHandles.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                foreach (var handle in runtime.EnumerateHandles())
                {
                    Assert.True(handles.Add(handle));
                }

                // Make sure we had at least one AsyncPinned handle
                Assert.Contains(handles, h => h.HandleType == HandleType.AsyncPinned);
            }
        }
    }
}
