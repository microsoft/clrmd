// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public enum GCMode
    {
        Workstation,
        Server
    }

    public class ExceptionTestData
    {
        public readonly string OuterExceptionMessage = "IOE Message";
        public readonly string OuterExceptionType = "System.InvalidOperationException";
    }

    public static class TestTargets
    {
        private static readonly Lazy<TestTarget> _arrays = new Lazy<TestTarget>(() => new TestTarget("Arrays.cs"));
        private static readonly Lazy<TestTarget> _clrObjects = new Lazy<TestTarget>(() => new TestTarget("ClrObjects.cs"));
        private static readonly Lazy<TestTarget> _gcroot = new Lazy<TestTarget>(() => new TestTarget("GCRoot.cs"));
        private static readonly Lazy<TestTarget> _nestedException = new Lazy<TestTarget>(() => new TestTarget("NestedException.cs"));
        private static readonly Lazy<TestTarget> _gcHandles = new Lazy<TestTarget>(() => new TestTarget("GCHandles.cs"));
        private static readonly Lazy<TestTarget> _types = new Lazy<TestTarget>(() => new TestTarget("Types.cs"));
        private static readonly Lazy<TestTarget> _appDomains = new Lazy<TestTarget>(() => new TestTarget("AppDomains.cs"));
        private static readonly Lazy<TestTarget> _finalizationQueue = new Lazy<TestTarget>(() => new TestTarget("FinalizationQueue.cs"));

        public static TestTarget GCRoot => _gcroot.Value;
        public static TestTarget NestedException => _nestedException.Value;
        public static ExceptionTestData NestedExceptionData => new ExceptionTestData();
        public static TestTarget GCHandles => _gcHandles.Value;
        public static TestTarget Types => _types.Value;
        public static TestTarget AppDomains => _appDomains.Value;
        public static TestTarget FinalizationQueue => _finalizationQueue.Value;
        public static TestTarget ClrObjects => _clrObjects.Value;
        public static TestTarget Arrays => _arrays.Value;
    }

    public class TestTarget
    {
        public string Executable { get; }

        public string Pdb { get; }

        public string Source { get; }

        private static string Architecture { get; }
        private static string TestRoot { get; }

        static TestTarget()
        {
            Architecture = IntPtr.Size == 4 ? "x86" : "x64";

            DirectoryInfo info = new DirectoryInfo(Environment.CurrentDirectory);
            while (info.GetFiles(".gitignore").Length != 1)
                info = info.Parent;

            TestRoot = Path.Combine(info.FullName, "src", "TestTargets");
        }

        public TestTarget(string source)
        {
            Source = Path.Combine(TestRoot, source);
            if (!File.Exists(Source))
                throw new FileNotFoundException($"Could not find source file: {source}");

            Executable = Path.Combine(Path.GetDirectoryName(Source), "bin", Architecture, Path.ChangeExtension(source, ".exe"));
            Pdb = Path.ChangeExtension(Executable, ".pdb");

            if (!File.Exists(Executable) || !File.Exists(Pdb))
            {
                string buildTestAssets = Path.Combine(Path.GetDirectoryName(Source), "build_test_assets.cmd");
                throw new InvalidOperationException($"You must first generate test binaries and crash dumps using by running: {buildTestAssets}");
            }
        }

        private string BuildDumpName(GCMode gcmode, bool full)
        {
            string filename = Path.Combine(Path.GetDirectoryName(Executable), Path.GetFileNameWithoutExtension(Executable));

            string gc = gcmode == GCMode.Server ? "svr" : "wks";
            string dumpType = full ? "" : "_mini";
            filename = $"{filename}_{gc}{dumpType}.dmp";
            return filename;
        }

        public DataTarget LoadMiniDump(GCMode gc = GCMode.Workstation)
        {
            string path = BuildDumpName(gc, false);
            return DataTarget.LoadCrashDump(path);
        }

        public DataTarget LoadFullDump(GCMode gc = GCMode.Workstation)
        {
            string path = BuildDumpName(gc, true);
            return DataTarget.LoadCrashDump(path);
        }
    }
}