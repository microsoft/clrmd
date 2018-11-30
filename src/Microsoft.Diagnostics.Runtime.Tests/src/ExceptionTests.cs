using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class ExceptionTests
    {
        [Fact]
        public void ExceptionPropertyTest()
        {
            using (var dt = TestTargets.NestedException.LoadFullDump())
            {
                var runtime = dt.ClrVersions.Single().CreateRuntime();
                TestProperties(runtime);
            }
        }

        internal static void TestProperties(ClrRuntime runtime)
        {
            var thread = runtime.Threads.Where(t => !t.IsFinalizer).Single();
            var ex = thread.CurrentException;
            Assert.NotNull(ex);

            var testData = TestTargets.NestedExceptionData;
            Assert.Equal(testData.OuterExceptionMessage, ex.Message);
            Assert.Equal(testData.OuterExceptionType, ex.Type.Name);
            Assert.NotNull(ex.Inner);
        }
    }
}