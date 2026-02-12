// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public class MethodTests
    {
        [FrameworkFact(Skip = "GetMethodByHandle does not reliably resolve MethodDescs across sessions with .NET 10 DAC.")]
        public void MethodHandleMultiDomainTests()
        {
            ulong[] methodDescs;
            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrType[] types = runtime.EnumerateModules().Where(m => m.Name.EndsWith("sharedlibrary.dll", System.StringComparison.OrdinalIgnoreCase)).Select(m => m.GetTypeByName("Foo")).Where(t => t != null).ToArray();

                Assert.Equal(2, types.Length);
                methodDescs = types.Select(t => t.Methods.Single(m => m.Name == "Bar")).Select(m => m.MethodDesc).ToArray();

                Assert.Equal(2, methodDescs.Length);
            }

            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrMethod method = runtime.GetMethodByHandle(methodDescs[0]);

                Assert.NotNull(method);
                Assert.Equal("Bar", method.Name);
                Assert.Equal("Foo", method.Type.Name);
            }

            using (DataTarget dt = TestTargets.AppDomains.LoadFullDump())
            {
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrMethod method = runtime.GetMethodByHandle(methodDescs[1]);

                Assert.NotNull(method);
                Assert.Equal("Bar", method.Name);
                Assert.Equal("Foo", method.Type.Name);
            }
        }

        [Fact]
        public void MethodHandleSingleDomainTests()
        {
            ulong methodDesc;
            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrModule module = runtime.GetModule("types.dll");
                ClrType type = module.GetTypeByName("Types");
                ClrMethod method = type.GetMethod("Inner");
                methodDesc = method.MethodDesc;

                Assert.NotEqual(0ul, methodDesc);
            }

            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();
                ClrMethod method = runtime.GetMethodByHandle(methodDesc);

                Assert.NotNull(method);
                Assert.Equal("Inner", method.Name);
                Assert.Equal("Types", method.Type.Name);
            }

            using (DataTarget dt = TestTargets.Types.LoadFullDump())
            {
                using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

                ClrModule module = runtime.GetModule("types.dll");
                ClrType type = module.GetTypeByName("Types");
                ClrMethod method = type.GetMethod("Inner");
                Assert.Equal(methodDesc, method.MethodDesc);
                Assert.Equal(method, runtime.GetMethodByHandle(methodDesc));
            }
        }

        /// <summary>
        /// Comprehensive regression test for ClrMethod.Name parsing (issue #935).
        /// Scans ALL methods in the dump (BCL and test target) and validates that the
        /// Name property satisfies key invariants. This catches regressions in the
        /// bracket-aware dot-search logic across thousands of real CLR signatures.
        /// </summary>
        [Fact]
        public void MethodName_InvariantsHoldForAllMethods()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            List<string> failures = new();
            int methodCount = 0;

            foreach (ClrModule module in runtime.EnumerateModules())
            {
                foreach ((ulong mt, int _) in module.EnumerateTypeDefToMethodTableMap())
                {
                    ClrType type = runtime.Heap.GetTypeByMethodTable(mt);
                    if (type == null)
                        continue;

                    foreach (ClrMethod method in type.Methods)
                    {
                        string signature = method.Signature;
                        if (signature == null)
                            continue;

                        string name = method.Name;
                        methodCount++;

                        if (name == null)
                        {
                            failures.Add($"Name is null for signature: {signature}");
                            continue;
                        }

                        if (name.Length == 0)
                        {
                            failures.Add($"Name is empty for signature: {signature}");
                            continue;
                        }

                        // Brackets in Name must be balanced
                        int opens = name.Count(c => c == '[');
                        int closes = name.Count(c => c == ']');
                        if (opens != closes)
                            failures.Add($"Unbalanced brackets ({opens} '[' vs {closes} ']') in name '{name}' from: {signature}");

                        // The method identifier (before any '[') must not contain dots
                        // (except for .ctor and .cctor which start with a dot)
                        int bracketIdx = name.IndexOf('[');
                        string methodIdent = bracketIdx >= 0 ? name.Substring(0, bracketIdx) : name;
                        if (methodIdent != ".ctor" && methodIdent != ".cctor" && methodIdent.Contains('.'))
                            failures.Add($"Method identifier '{methodIdent}' contains dots, name '{name}' from: {signature}");

                        // Name + "(" must appear in the signature
                        if (!signature.Contains(name + "("))
                            failures.Add($"Name '{name}' + '(' not found in: {signature}");

                        // Name must not contain parentheses — that indicates the
                        // outermost '(' was not correctly identified, e.g. when function
                        // pointer types introduce nested parens (issue #842).
                        if (name.Contains('(') || name.Contains(')'))
                            failures.Add($"Name '{name}' contains parentheses from: {signature}");
                    }
                }
            }

            Assert.True(methodCount > 100, $"Expected to scan at least 100 methods, found {methodCount}");
            Assert.True(failures.Count == 0,
                $"Found {failures.Count} Name parsing failures out of {methodCount} methods:\n" +
                string.Join("\n", failures.Take(50)));
        }

        /// <summary>
        /// Regression test for issue #935: methods on generic type instantiations whose
        /// type parameters contain dots (e.g., "System.Private.CoreLib") must not have
        /// their Name truncated into the bracket expression.
        /// </summary>
        [Fact]
        public void MethodName_GenericBracketSignatures_NotTruncated()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            List<string> failures = new();
            int bracketMethodCount = 0;

            foreach (ClrModule module in runtime.EnumerateModules())
            {
                foreach ((ulong mt, int _) in module.EnumerateTypeDefToMethodTableMap())
                {
                    ClrType type = runtime.Heap.GetTypeByMethodTable(mt);
                    if (type == null)
                        continue;

                    foreach (ClrMethod method in type.Methods)
                    {
                        string sig = method.Signature;
                        if (sig == null)
                            continue;

                        // Focus on signatures with generic parameter brackets before '('
                        int parenIdx = sig.LastIndexOf('(');
                        if (parenIdx < 0)
                            continue;

                        string beforeParen = sig.Substring(0, parenIdx);
                        if (!beforeParen.Contains("[["))
                            continue;

                        bracketMethodCount++;
                        string name = method.Name;

                        // Name ending with ]] but not containing [[ means it was
                        // truncated into a bracket expression (the original bug)
                        if (name != null && name.EndsWith("]]") && !name.Contains("[["))
                            failures.Add($"Name '{name}' appears truncated into generic brackets from: {sig}");

                        // Name should not be a namespace fragment from inside brackets
                        if (name != null && !name.Contains("["))
                        {
                            // If Name has no brackets but the type-method part of the
                            // signature does, the Name should be the part after the last
                            // dot that's outside all brackets
                            if (name.Contains("CoreLib") || name.Contains("Private") || name.Contains("System."))
                                failures.Add($"Name '{name}' looks like a namespace fragment from: {sig}");
                        }
                    }
                }
            }

            Assert.True(bracketMethodCount > 0, "Expected at least one method with generic brackets in its signature");
            Assert.True(failures.Count == 0,
                $"Found {failures.Count} truncated names out of {bracketMethodCount} bracket-containing methods:\n" +
                string.Join("\n", failures.Take(50)));
        }

        /// <summary>
        /// Verifies that constructors and static constructors are correctly identified
        /// by the Name property, including the double-dot prefix handling.
        /// </summary>
        [Fact]
        public void MethodName_Constructors_CorrectlyIdentified()
        {
            using DataTarget dt = TestTargets.Types.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            int ctorCount = 0;
            int cctorCount = 0;
            List<string> failures = new();

            foreach (ClrModule module in runtime.EnumerateModules())
            {
                foreach ((ulong mt, int _) in module.EnumerateTypeDefToMethodTableMap())
                {
                    ClrType type = runtime.Heap.GetTypeByMethodTable(mt);
                    if (type == null)
                        continue;

                    foreach (ClrMethod method in type.Methods)
                    {
                        string sig = method.Signature;
                        if (sig == null)
                            continue;

                        string name = method.Name;
                        if (name == null)
                            continue;

                        if (sig.Contains("..ctor("))
                        {
                            ctorCount++;
                            if (name != ".ctor")
                                failures.Add($"Expected '.ctor' but got '{name}' for: {sig}");

                            if (!method.IsConstructor)
                                failures.Add($"IsConstructor is false for: {sig}");
                        }
                        else if (sig.Contains("..cctor("))
                        {
                            cctorCount++;
                            if (name != ".cctor")
                                failures.Add($"Expected '.cctor' but got '{name}' for: {sig}");

                            if (!method.IsClassConstructor)
                                failures.Add($"IsClassConstructor is false for: {sig}");
                        }
                    }
                }
            }

            Assert.True(ctorCount > 10, $"Expected to find at least 10 constructors, found {ctorCount}");
            Assert.True(cctorCount > 0, $"Expected to find at least 1 static constructor, found {cctorCount}");
            Assert.True(failures.Count == 0,
                $"Found {failures.Count} constructor naming failures:\n" +
                string.Join("\n", failures.Take(20)));
        }

        [FrameworkFact]
        public void CompleteSignatureIsRetrievedForMethodsWithGenericParameters()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule("sharedlibrary.dll");
            ClrType type = module.GetTypeByName("Foo");

            ClrMethod[] methods = type.Methods.ToArray();
            ClrMethod genericMethod = type.GetMethod("GenericBar");

            string methodName = genericMethod.Signature;

            Assert.Equal(')', methodName.Last());
        }

        [WindowsFact]
        public void AssemblySize()
        {
            using DataTarget dt = TestTargets.AppDomains.LoadFullDump();
            using ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            ClrModule module = runtime.GetModule("sharedlibrary.dll");
            ClrType type = module.GetTypeByName("Foo");

            ClrMethod[] methods = type.Methods.ToArray();
            ClrMethod genericMethod = type.GetMethod("GenericBar");

            Assert.NotEqual<uint>(0, genericMethod.HotColdInfo.ColdSize + genericMethod.HotColdInfo.HotSize);
        }
    }
}
