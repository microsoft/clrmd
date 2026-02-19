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
        private static readonly Lazy<TestTarget> _appDomains = new(() => new TestTarget("AppDomains", frameworkOnly: true));
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
                string artifacts = Path.Combine(curr, ".test_artifacts");
                if (Directory.Exists(artifacts))
                    return artifacts;

                curr = Path.GetDirectoryName(curr);
            }

            return null;
        }
    }

    public class TestTarget
    {
        public string Source { get; }

        /// <summary>
        /// The name of this test target (e.g. "Types", "Arrays").
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Whether this target only supports .NET Framework (e.g. AppDomains).
        /// </summary>
        public bool FrameworkOnly { get; }

        private static string TestRoot { get; }

        static TestTarget()
        {
            DirectoryInfo info = new(Environment.CurrentDirectory);
            while (info.GetFiles(".gitignore").Length != 1)
                info = info.Parent;

            TestRoot = Path.Combine(info.FullName, "src", "TestTargets");
        }

        public TestTarget(string name, bool frameworkOnly = false)
        {
            Name = name;
            FrameworkOnly = frameworkOnly;

            Source = Path.Combine(TestRoot, name, name + ".cs");
            if (!File.Exists(Source))
                throw new FileNotFoundException($"Could not find source file: {name}.cs");
        }

        /// <summary>
        /// Gets the directory containing the test target's project file.
        /// </summary>
        private string ProjectDir => Path.Combine(TestRoot, Name);

        /// <summary>
        /// Gets the output directory for built targets of a given architecture.
        /// </summary>
        private static string GetOutputDir(string architecture) => Path.Combine(TestRoot, "bin", architecture);

        /// <summary>
        /// Gets the path to the executable for a given architecture and framework.
        /// </summary>
        public string GetExecutable(string architecture, bool isFramework)
        {
            string dir = GetOutputDir(architecture);
            return Path.Combine(dir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Name + ".exe" : Name);
        }

        /// <summary>
        /// Gets the PDB path for a given architecture.
        /// </summary>
        public string GetPdb(string architecture)
        {
            string dir = GetOutputDir(architecture);
            return Path.Combine(dir, Name + ".pdb");
        }

        /// <summary>
        /// Legacy properties for backward compatibility.
        /// </summary>
        public string Executable => GetExecutable(DumpGenerator.GetArchitecture(), FrameworkOnly);
        public string Pdb => GetPdb(DumpGenerator.GetArchitecture());

        /// <summary>
        /// Builds the dump file name for a given configuration.
        /// </summary>
        public string BuildDumpName(GCMode gcmode, bool full, string architecture = null, bool? isFramework = null)
        {
            architecture ??= DumpGenerator.GetArchitecture();
            bool framework = isFramework ?? FrameworkOnly;

            string dir = GetOutputDir(architecture);
            string gc = gcmode == GCMode.Server ? "svr" : "wks";
            string dumpType = full ? string.Empty : "_mini";
            string fwSuffix = framework ? "_net48" : string.Empty;
            return Path.Combine(dir, $"{Name}_{gc}{dumpType}{fwSuffix}.dmp");
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

        /// <summary>
        /// Loads a full dump, generating it on-demand if it doesn't exist.
        /// </summary>
        public DataTarget LoadFullDump(GCMode gc = GCMode.Workstation) => LoadFullDump(gc, DumpGenerator.GetArchitecture());

        public DataTarget LoadFullDump(GCMode gc, string architecture, bool? isFramework = null)
        {
            bool framework = isFramework ?? FrameworkOnly;
            string dumpPath = BuildDumpName(gc, full: true, architecture: architecture, isFramework: framework);

            DumpGenerator.EnsureDump(Name, ProjectDir, dumpPath, architecture, framework, gc, full: true);
            return LoadDump(dumpPath);
        }

        /// <summary>
        /// Loads a mini dump, generating it on-demand if it doesn't exist.
        /// </summary>
        public DataTarget LoadMinidump(GCMode gc = GCMode.Workstation) => LoadMinidump(gc, DumpGenerator.GetArchitecture());

        public DataTarget LoadMinidump(GCMode gc, string architecture, bool? isFramework = null)
        {
            bool framework = isFramework ?? FrameworkOnly;
            string dumpPath = BuildDumpName(gc, full: false, architecture: architecture, isFramework: framework);

            DumpGenerator.EnsureDump(Name, ProjectDir, dumpPath, architecture, framework, gc, full: false);
            return LoadDump(dumpPath);
        }

        public DataTarget LoadFullDumpWithDbgEng(GCMode gc = GCMode.Workstation)
        {
            string architecture = DumpGenerator.GetArchitecture();
            bool framework = FrameworkOnly;
            string dumpPath = BuildDumpName(gc, full: true, architecture: architecture, isFramework: framework);

            DumpGenerator.EnsureDump(Name, ProjectDir, dumpPath, architecture, framework, gc, full: true);

            Utilities.DbgEng.DbgEngIDataReader dbgengReader = new(dumpPath);
            return new DataTarget(new CustomDataTarget(dbgengReader, null));
        }
    }
}
