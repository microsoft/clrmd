// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Implementation;
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
        private static readonly Lazy<TestTarget> _arrays = new(() => new TestTarget("Arrays"));
        private static readonly Lazy<TestTarget> _clrObjects = new(() => new TestTarget("ClrObjects"));
        private static readonly Lazy<TestTarget> _gcroot = new(() => new TestTarget("GCRoot"));
        private static readonly Lazy<TestTarget> _gcroot2 = new(() => new TestTarget("GCRoot2"));
        private static readonly Lazy<TestTarget> _nestedException = new(() => new TestTarget("NestedException"));
        private static readonly Lazy<TestTarget> _nestedTypes = new(() => new TestTarget("NestedTypes"));
        private static readonly Lazy<TestTarget> _gcHandles = new(() => new TestTarget("GCHandles"));
        private static readonly Lazy<TestTarget> _types = new(() => new TestTarget("Types"));
        private static readonly Lazy<TestTarget> _appDomains = new(() => new TestTarget("AppDomains"));
        private static readonly Lazy<TestTarget> _finalizationQueue = new(() => new TestTarget("FinalizationQueue"));
        private static readonly Lazy<TestTarget> _byReference = new(() => new TestTarget("ByReference"));

        public static TestTarget GCRoot => _gcroot.Value;
        public static TestTarget GCRoot2 => _gcroot2.Value;
        public static TestTarget NestedException => _nestedException.Value;
        public static TestTarget NestedTypes => _nestedTypes.Value;
        public static ExceptionTestData NestedExceptionData => new();
        public static TestTarget GCHandles => _gcHandles.Value;
        public static TestTarget Types => _types.Value;
        public static TestTarget AppDomains => _appDomains.Value;
        public static TestTarget FinalizationQueue => _finalizationQueue.Value;
        public static TestTarget ClrObjects => _clrObjects.Value;
        public static TestTarget Arrays => _arrays.Value;
        public static TestTarget ByReference => _byReference.Value;

        public static string GetTestArtifactFolder()
        {
            string curr = Environment.CurrentDirectory;
            while (curr != null)
            {
                string artifacts = Path.Combine(curr, "test_artifacts");
                if (Directory.Exists(artifacts))
                    return artifacts;

                curr = Path.GetDirectoryName(curr);
            }

            return null;
        }
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

            DirectoryInfo info = new(Environment.CurrentDirectory);
            while (info.GetFiles(".gitignore").Length != 1)
                info = info.Parent;

            TestRoot = Path.Combine(info.FullName, "src", "TestTargets");
        }

        public TestTarget(string name)
        {
            Source = Path.Combine(TestRoot, name, name + ".cs");
            if (!File.Exists(Source))
                throw new FileNotFoundException($"Could not find source file: {name}.cs");

            Executable = Path.Combine(TestRoot, "bin", Architecture, name + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : null));
            Pdb = Path.ChangeExtension(Executable, ".pdb");

            if (!File.Exists(Executable) || !File.Exists(Pdb))
            {
                string buildTestAssets = Path.Combine(TestRoot, "TestTargets.csproj");
                throw new InvalidOperationException($"You must first generate test binaries and crash dumps using by running: dotnet build {buildTestAssets}");
            }
        }

        private static DataTarget LoadDump(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                DataTarget dataTarget = DataTarget.LoadDump(path);
                bool symTrace = CustomDataTarget.GetTraceEnvironmentVariable();
                dataTarget.FileLocator = SymbolGroup.CreateFromSymbolPath(string.Empty, trace: symTrace, null);
                return dataTarget;
            }
            else
            {
                return DataTarget.LoadDump(path);
            }
        }

        public string BuildDumpName(GCMode gcmode, bool full)
        {
            string fileName = Path.Combine(Path.GetDirectoryName(Executable), Path.GetFileNameWithoutExtension(Executable));

            string gc = gcmode == GCMode.Server ? "svr" : "wks";
            string dumpType = full ? string.Empty : "_mini";
            fileName = $"{fileName}_{gc}{dumpType}.dmp";
            return fileName;
        }

        public DataTarget LoadMinidump(GCMode gc = GCMode.Workstation) => LoadDump(BuildDumpName(gc, false));

        public DataTarget LoadFullDump(GCMode gc = GCMode.Workstation) => LoadDump(BuildDumpName(gc, true));

        public DataTarget LoadFullDumpWithDbgEng(GCMode gc = GCMode.Workstation)
        {
            string dumpPath = BuildDumpName(gc, true);
            Utilities.DbgEng.DbgEngIDataReader dbgengReader = new(dumpPath);
            return new DataTarget(new CustomDataTarget(dbgengReader, null));
        }
    }
}
