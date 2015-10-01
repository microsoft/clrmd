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
        public static TestTarget NestedException = new TestTarget("NestedException.cs");
        public static ExceptionTestData NestedExceptionData = new ExceptionTestData();
        public static TestTarget GCHandles = new TestTarget("GCHandles.cs");
        public static TestTarget Types = new TestTarget("Types.cs");
        public static TestTarget AppDomains = new TestTarget("AppDomains.cs", NestedException);
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

        public TestTarget(string source)
        {
            _source = Path.Combine(Environment.CurrentDirectory, "Targets", source);
            _isLibrary = false;
        }

        public TestTarget(string source, bool isLibrary)
        {
            _source = Path.Combine(Environment.CurrentDirectory, "Targets", source);
            _isLibrary = isLibrary;

            if (_isLibrary)
                _executable = CompileCSharp(_source, true);
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
            WriteCrashDumps(gc);

            return DataTarget.LoadCrashDump(_miniDumpPath[(int)gc]);
        }

        public DataTarget LoadFullDump(GCMode gc = GCMode.Workstation)
        {
            WriteCrashDumps(gc);

            return DataTarget.LoadCrashDump(_fullDumpPath[(int)gc]);
        }

        void CompileSource()
        {
            if (_executable != null)
                return;

            _executable = CompileCSharp(_source, _isLibrary);
        }


        private static string CompileCSharp(string source, bool isLibrary)
        {
            string extension = isLibrary ? "dll" : "exe";
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
            cp.CompilerOptions = IntPtr.Size == 4 ? "/platform:x86" : "/platform:amd64";

            cp.IncludeDebugInformation = true;
            cp.OutputAssembly = Path.Combine(Helpers.TestWorkingDirectory, Path.ChangeExtension(Path.GetFileNameWithoutExtension(source), extension));
            CompilerResults cr = provider.CompileAssemblyFromFile(cp, source);

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
