using Microsoft.CSharp;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    public class TestTargets
    {
        static Lazy<TestTarget> _nestedException = new Lazy<TestTarget>(() => new TestTarget("NestedException.cs"));
        static Lazy<TestTarget> _gcHandles = new Lazy<TestTarget>(() => new TestTarget("GCHandles.cs"));
        static Lazy<TestTarget> _types = new Lazy<TestTarget>(() => new TestTarget("Types.cs"));
        static Lazy<TestTarget> _appDomains = new Lazy<TestTarget>(() => new TestTarget("AppDomains.cs", NestedException));

        public static TestTarget NestedException { get { return _nestedException.Value; } }
        public static ExceptionTestData NestedExceptionData = new ExceptionTestData();
        public static TestTarget GCHandles { get { return _gcHandles.Value; } }
        public static TestTarget Types { get { return _types.Value; } }
        public static TestTarget AppDomains { get { return _appDomains.Value; } }
    }

    public class TestTarget
    {
        static TestTarget _sharedLibrary = new TestTarget("SharedLibrary.cs", true);

        private bool _isLibrary;
        private string _source;
        private string _executable;
        private object _sync = new object();
        private string[] _miniDumpPath = new string[2];
        private string[] _fullDumpPath = new string[2];

        public string Executable
        {
            get
            {
                if (_executable == null)
                    CompileSource();
                return _executable;
            }
        }

        public string Pdb
        {
            get
            {
                return Path.ChangeExtension(Executable, "pdb");
            }
        }

        public string Source { get { return _source; } }
        
        public TestTarget(string source, bool isLibrary = false)
        {
            _source = Path.Combine(Environment.CurrentDirectory, "Targets", source);
            _isLibrary = isLibrary;
        }

        public TestTarget(string source, params TestTarget[] required)
        {
            _source = Path.Combine(Environment.CurrentDirectory, "Targets", source);
            _isLibrary = false;

            foreach (var item in required)
                item.CompileSource();
        }

        public DataTarget LoadMiniDump(GCMode gc = GCMode.Workstation)
        {
            string path = GetMiniDumpName(Executable, gc);
            if (File.Exists(path))
                return DataTarget.LoadCrashDump(path);

            WriteCrashDumps(gc);

            return DataTarget.LoadCrashDump(_miniDumpPath[(int)gc]);
        }

        public DataTarget LoadFullDump(GCMode gc = GCMode.Workstation)
        {
            string path = GetFullDumpName(Executable, gc);
            if (File.Exists(path))
                return DataTarget.LoadCrashDump(path);

            WriteCrashDumps(gc);

            return DataTarget.LoadCrashDump(_fullDumpPath[(int)gc]);
        }

        void CompileSource()
        {
            if (_executable != null)
                return;

            // Don't recompile if it's there.
            string destination = GetOutputAssembly();
            if (!File.Exists(destination))
                _executable = CompileCSharp(_source, destination, _isLibrary);
            else
                _executable = destination;
        }

        private string GetOutputAssembly()
        {
            string extension = _isLibrary ? "dll" : "exe";
            return Path.Combine(Helpers.TestWorkingDirectory, Path.ChangeExtension(Path.GetFileNameWithoutExtension(_source), extension));
        }

        private static string CompileCSharp(string source, string destination, bool isLibrary)
        {
            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerParameters cp = new CompilerParameters();
            cp.ReferencedAssemblies.Add("system.dll");
            
            if (isLibrary)
            {
                cp.GenerateExecutable = false;
            }
            else
            {
                cp.GenerateExecutable = true;
                cp.ReferencedAssemblies.Add(_sharedLibrary.Executable);
            }

            cp.GenerateInMemory = false;
            cp.CompilerOptions = IntPtr.Size == 4 ? "/platform:x86" : "/platform:x64";

            cp.IncludeDebugInformation = true;
            cp.OutputAssembly = destination;
            CompilerResults cr = provider.CompileAssemblyFromFile(cp, source);

            if (cr.Errors.Count > 0 && System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debugger.Break();

            Assert.AreEqual(0, cr.Errors.Count);

            return cr.PathToAssembly;
        }

        private void WriteCrashDumps(GCMode gc)
        {
            if (_fullDumpPath[(int)gc] != null)
                return;

            string executable = Executable;
            DebuggerStartInfo info = new DebuggerStartInfo();
            if (gc == GCMode.Server)
                info.SetEnvironmentVariable("COMPlus_BuildFlavor", "svr");

            using (Debugger dbg = info.LaunchProcess(executable, Helpers.TestWorkingDirectory))
            {
                dbg.SecondChanceExceptionEvent += delegate (Debugger d, EXCEPTION_RECORD64 ex)
                {
                    if (ex.ExceptionCode == (uint)ExceptionTypes.Clr)
                        WriteDumps(dbg, executable, gc);
                };

                DEBUG_STATUS status;
                do
                {
                    status = dbg.ProcessEvents(0xffffffff);
                } while (status != DEBUG_STATUS.NO_DEBUGGEE);

                Assert.IsNotNull(_miniDumpPath[(int)gc]);
                Assert.IsNotNull(_fullDumpPath[(int)gc]);
            }
        }

        private void WriteDumps(Debugger dbg, string exe, GCMode gc)
        {
            string dump = GetMiniDumpName(exe, gc);
            dbg.WriteDumpFile(dump, DEBUG_DUMP.SMALL);
            _miniDumpPath[(int)gc] = dump;

            dump = GetFullDumpName(exe, gc);
            dbg.WriteDumpFile(dump, DEBUG_DUMP.DEFAULT);
            _fullDumpPath[(int)gc] = dump;

            dbg.TerminateProcess();
        }

        private static string GetMiniDumpName(string executable, GCMode gc)
        {
            string basePath = Path.Combine(Path.GetDirectoryName(executable), Path.GetFileNameWithoutExtension(executable));
            basePath += gc == GCMode.Workstation ? "_wks" : "_svr";
            basePath += "_mini.dmp";
            return basePath;
        }
        private static string GetFullDumpName(string executable, GCMode gc)
        {
            string basePath = Path.Combine(Path.GetDirectoryName(executable), Path.GetFileNameWithoutExtension(executable));
            basePath += gc == GCMode.Workstation ? "_wks" : "_svr";
            basePath += "_full.dmp";
            return basePath;
        }
    }
}
