using Xunit;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ModuleTests
    {
        [Fact]
        public void TestGetTypeByName()
        {
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrHeap heap = runtime.Heap;

                ClrModule shared = runtime.GetModule("sharedlibrary.dll");
                Assert.NotNull(shared.GetTypeByName("Foo"));
                Assert.Null(shared.GetTypeByName("Types"));

                ClrModule types = runtime.GetModule("types.exe");
                Assert.NotNull(types.GetTypeByName("Types"));
                Assert.Null(types.GetTypeByName("Foo"));
            }
        }
    }
}
