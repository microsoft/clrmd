using Xunit;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ExceptionTests
    {
        [Fact]
        public void ExceptionPropertyTest()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                TestProperties(runtime);
            }
        }

        internal static void TestProperties(ClrRuntime runtime)
        {
            ClrThread thread = runtime.Threads.Where(t => !t.IsFinalizer).Single();
            ClrException ex = thread.CurrentException;
            Assert.NotNull(ex);

            ExceptionTestData testData = TestTargets.NestedExceptionData;
            Assert.Equal(testData.OuterExceptionMessage, ex.Message);
            Assert.Equal(testData.OuterExceptionType, ex.Type.Name);
            Assert.NotNull(ex.Inner);
        }
    }
}
