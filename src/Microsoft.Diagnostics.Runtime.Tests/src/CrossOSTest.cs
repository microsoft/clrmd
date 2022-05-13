using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests.src
{
    public class CrossOSTest
    {
        [WindowsFact] // ModuleInfo.Version only supports native modules at the moment
        public void LinuxDebugTest()
        {
            string artifacts = TestTargets.GetTestArtifactFolder();
            Assert.NotNull(artifacts);

            string core = Path.Combine(artifacts, "arrays_wks_mini.coredump");
            Assert.True(File.Exists(core));

            using DataTarget dt = DataTarget.LoadDump(core);
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrAppDomain domain = Assert.Single(runtime.AppDomains);

            Assert.Equal("clrhost", domain.Name);
            Assert.Contains("/home/leculver/clrmd/src/TestTargets/bin/x64/Arrays.dll", domain.Modules.Select(m => m.Name));
        }
    }
}
