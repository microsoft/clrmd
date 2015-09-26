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
    class ExceptionTestData
    {
        public readonly string OuterExceptionMessage = "IOE Message";
        public readonly string OuterExceptionType = "System.InvalidOperationException";
    }

    class TestTargets
    {
        public static TestTarget NestedException = new TestTarget("NestedException.cs");
        public static ExceptionTestData NestedExceptionData = new ExceptionTestData();
        public static TestTarget GCHandles = new TestTarget("GCHandles.cs");
        public static TestTarget Types = new TestTarget("Types.cs");
        public static TestTarget AppDomains = new TestTarget("AppDomains.cs", NestedException);
    }

    class TestTarget
    {
        static TestTarget _sharedLibrary = new TestTarget("SharedLibrary.cs", true);

        private bool _isLibrary;
        private string _source;
        private string _executable;
        private object _sync = new object();
        private string _miniDumpPath;
        private string _fullDumpPath;

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

        public DataTarget LoadMiniDump()
        {
            WriteCrashDumps();

            return DataTarget.LoadCrashDump(_miniDumpPath);
        }

        public DataTarget LoadFullDump()
        {
            WriteCrashDumps();

            return DataTarget.LoadCrashDump(_fullDumpPath);
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

        private void WriteCrashDumps()
        {
            if (_fullDumpPath != null)
                return;

            string executable = Executable;
            DebuggerStartInfo info = new DebuggerStartInfo();
            using (Debugger dbg = info.LaunchProcess(executable, Helpers.TestWorkingDirectory))
            {
                dbg.SecondChanceExceptionEvent += delegate (Debugger d, EXCEPTION_RECORD64 ex)
                {
                    if (ex.ExceptionCode == (uint)ExceptionTypes.Clr)
                        WriteDumps(dbg, executable);
                };

                DEBUG_STATUS status;
                do
                {
                    status = dbg.ProcessEvents(0xffffffff);
                } while (status != DEBUG_STATUS.NO_DEBUGGEE);

                Assert.IsNotNull(_miniDumpPath);
                Assert.IsNotNull(_fullDumpPath);
            }
        }

        private void WriteDumps(Debugger dbg, string exe)
        {
            string dump = GetMiniDumpName(exe);
            dbg.WriteDumpFile(dump, DEBUG_DUMP.SMALL);
            _miniDumpPath = dump;

            dump = GetFullDumpName(exe);
            dbg.WriteDumpFile(dump, DEBUG_DUMP.DEFAULT);
            _fullDumpPath = dump;

            dbg.TerminateProcess();
        }

        private static string GetMiniDumpName(string executable)
        {
            string basePath = Path.Combine(Path.GetDirectoryName(executable), Path.GetFileNameWithoutExtension(executable));
            return basePath + "_mini.dmp";
        }
        private static string GetFullDumpName(string executable)
        {
            string basePath = Path.Combine(Path.GetDirectoryName(executable), Path.GetFileNameWithoutExtension(executable));
            return basePath + "_full.dmp";
        }
    }
}
