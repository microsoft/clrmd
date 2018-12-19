// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.Runtime.Utilities.Pdb;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class PdbTests
    {
        [Fact]
        public void PdbEqualityTest()
        {
            // Ensure all methods in our source file is in the pdb.
            using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                PdbInfo[] allPdbs = runtime.Modules.Where(m => m.Pdb != null).Select(m => m.Pdb).ToArray();
                Assert.True(allPdbs.Length > 1);

                for (int i = 0; i < allPdbs.Length; i++)
                {
                    Assert.True(allPdbs[i].Equals(allPdbs[i]));
                    for (int j = i + 1; j < allPdbs.Length; j++)
                    {
                        Assert.False(allPdbs[i].Equals(allPdbs[j]));
                        Assert.False(allPdbs[j].Equals(allPdbs[i]));
                    }
                }
            }
        }

        [Fact]
        public void PdbGuidAgeTest()
        {
            PdbReader.GetPdbProperties(TestTargets.NestedException.Pdb, out Guid pdbSignature, out int pdbAge);

            // Ensure we get the same answer a different way.
            using (PdbReader pdbReader = new PdbReader(TestTargets.NestedException.Pdb))
            {
                Assert.Equal(pdbAge, pdbReader.Age);
                Assert.Equal(pdbSignature, pdbReader.Signature);
            }

            // Ensure the PEFile has the same signature/age.
            using (Stream stream = File.OpenRead(TestTargets.NestedException.Executable))
            {
                PEImage peimage = new PEImage(stream);
                Assert.True(peimage.IsValid);
                Assert.Equal(peimage.DefaultPdb.Guid, pdbSignature);
                Assert.Equal(peimage.DefaultPdb.Revision, pdbAge);
            }
        }

        [Fact]
        public void PdbSourceLineTest()
        {
            using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
            {
                ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrThread thread = runtime.GetMainThread();

                HashSet<int> sourceLines = new HashSet<int>();
                using (PdbReader reader = new PdbReader(TestTargets.NestedException.Pdb))
                {
                    Assert.Equal(TestTargets.NestedException.Source, reader.Sources.Single().Name, true);

                    IEnumerable<PdbFunction> functions = from frame in thread.StackTrace
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
                foreach (string line in File.ReadLines(TestTargets.NestedException.Source))
                {
                    curr++;
                    if (line.Contains("/* seq */"))
                        Assert.Contains(curr, sourceLines);
                }
            }
        }

        [Fact]
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

                        Assert.NotNull(pdb.GetFunctionFromToken(method.MetadataToken));
                    }
                }
            }
        }
    }
}