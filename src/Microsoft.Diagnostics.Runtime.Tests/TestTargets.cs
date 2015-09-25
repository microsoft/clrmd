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
    }

    class TestTarget
    {
        private string _source;
        private string _executable;
        private object _sync = new object();
        private string _miniDumpPath;
        private string _fullDumpPath;

        public string Executable { get { Init();  return _executable; } }
        public string Pdb { get { Init(); return Path.ChangeExtension(_executable, "pdb"); } }

        public string Source { get { return _source; } }

        public TestTarget(string source)
        {
            _source = Path.Combine(Environment.CurrentDirectory, "Targets", source);
        }

        public DataTarget LoadMiniDump()
        {
            Init();

            return DataTarget.LoadCrashDump(_miniDumpPath);
        }

        public DataTarget LoadFullDump()
        {
            Init();

            return DataTarget.LoadCrashDump(_fullDumpPath);
        }

        void Init()
        {
            if (_executable != null)
                return;

            lock (_sync)
            {
                if (_executable != null)
                    return;

                string executable = CompileCSharp(_source);
                WriteCrashDumps(executable);

                _executable = executable;
            }
        }


        private static string CompileCSharp(string source)
        {
            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerParameters cp = new CompilerParameters();
            cp.ReferencedAssemblies.Add("system.dll");
            cp.GenerateExecutable = true;
            cp.GenerateInMemory = false;
            cp.CompilerOptions = IntPtr.Size == 4 ? "/platform:x86" : "/platform:amd64";

            cp.IncludeDebugInformation = true;
            cp.OutputAssembly = Path.Combine(Helpers.TestWorkingDirectory, Path.ChangeExtension(Path.GetFileNameWithoutExtension(source), "exe"));
            CompilerResults cr = provider.CompileAssemblyFromFile(cp, source);

            Assert.AreEqual(0, cr.Errors.Count);

            return cr.PathToAssembly;
        }

        private void WriteCrashDumps(string executable)
        {
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
