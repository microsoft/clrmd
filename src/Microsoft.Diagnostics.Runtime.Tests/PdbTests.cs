using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.Runtime.Utilities.Pdb;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    [TestClass]
    public class PdbTests
    {
        [TestMethod]
        public void PdbGuidAgeTest()
        {
            int pdbAge;
            Guid pdbSignature;
            PdbReader.GetPdbProperties(TestTargets.NestedException.Pdb, out pdbSignature, out pdbAge);
            
            // Ensure we get the same answer a different way.
            using (PdbReader pdbReader = new PdbReader(TestTargets.NestedException.Pdb))
            {
                Assert.AreEqual(pdbAge, pdbReader.Age);
                Assert.AreEqual(pdbSignature, pdbReader.Signature);
            }

            // Ensure the PEFile has the same signature/age.
            using (PEFile peFile = new PEFile(TestTargets.NestedException.Executable))
            {
                Assert.AreEqual(peFile.PdbInfo.Guid, pdbSignature);
                Assert.AreEqual(peFile.PdbInfo.Revision, pdbAge);
            }
        }

        [TestMethod]
        public void PdbSourceLineTest()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrThread thread = runtime.GetMainThread();
                
                HashSet<int> sourceLines = new HashSet<int>();
                using (PdbReader reader = new PdbReader(TestTargets.NestedException.Pdb))
                {
                    Assert.IsTrue(TestTargets.NestedException.Source.Equals(reader.Sources.Single().Name, StringComparison.OrdinalIgnoreCase));

                    var functions = from frame in thread.StackTrace
                                            where frame.Kind != ClrStackFrameType.Runtime
                                            select reader.GetFunctionFromToken(frame.Method.MetadataToken);

                    foreach (PdbFunction function in functions)
                    {
                        PdbSequencePointCollection sourceFile = function.SequencePoints.Single();

                        foreach (int line in sourceFile.Lines.Select(l => l.LineBegin))
                            sourceLines.Add(line);
                    }
                }


                int curr = 0;
                foreach (var line in File.ReadLines(TestTargets.NestedException.Source))
                {
                    curr++;
                    if (line.Contains("/* seq */"))
                        Assert.IsTrue(sourceLines.Contains(curr));
                }
            }
        }

        [TestMethod]
        public void PdbMethodTest()
        {
            // Ensure all methods in our source file is in the pdb.
            using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrModule module = runtime.Modules.Where(m => m.Name.Equals(TestTargets.NestedException.Executable, StringComparison.OrdinalIgnoreCase)).Single();
                ClrType type = module.GetTypeByName("Program");

                using (PdbReader pdb = new PdbReader(TestTargets.NestedException.Pdb))
                {
                    foreach (ClrMethod method in type.Methods)
                    {
                        // ignore inherited methods and constructors
                        if (method.Type != type || method.IsConstructor || method.IsClassConstructor)
                            continue;

                        Assert.IsNotNull(pdb.GetFunctionFromToken(method.MetadataToken));
                    }
                }
            }
        }
    }
}
