
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.Diagnostics.Runtime.ICorDebug;
using Microsoft.Diagnostics.Runtime.Utilities.Pdb;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    public static class Helpers
    {
        public static ClrMethod GetMethod(this ClrType type, string name)
        {
            return GetMethods(type, name).Single();
        }

        public static IEnumerable<ClrMethod> GetMethods(this ClrType type, string name)
        {
            return type.Methods.Where(m => m.Name == name);
        }

        public static HashSet<T> Unique<T>(this IEnumerable<T> self)
        {
            HashSet<T> set = new HashSet<T>();
            foreach (T t in self)
                set.Add(t);

            return set;
        }

        public static ClrAppDomain GetDomainByName(this ClrRuntime runtime, string domainName)
        {
            return runtime.AppDomains.Where(ad => ad.Name == domainName).Single();
        }

        public static ClrModule GetModule(this ClrRuntime runtime, string filename)
        {
            return (from module in runtime.Modules
                    let file = Path.GetFileName(module.FileName)
                    where file.Equals(filename, StringComparison.OrdinalIgnoreCase)
                    select module).Single();
        }


        public static ClrThread GetMainThread(this ClrRuntime runtime)
        {
            ClrThread thread = runtime.Threads.Where(t => !t.IsFinalizer).Single();
            return thread;
        }

        public static ClrStackFrame GetFrame(this ClrThread thread, string functionName)
        {
            return thread.StackTrace.Where(sf => sf.Method != null ? sf.Method.Name == functionName : false).Single();
        }

        public static ClrValue GetLocal(this ClrStackFrame frame, string name)
        {
            List<string> names = GetLocalNames(frame, frame.GetSourceLocation());
            int i = names.IndexOf(name);
            return frame.Locals[i];
        }

        private static FileAndLineNumber GetSourceLocation(this ClrStackFrame frame)
        {
            PdbReader reader = GetReaderForFrame(frame);
            if (reader == null)
                return new FileAndLineNumber();

            PdbFunction function = reader.GetFunctionFromToken(frame.Method.MetadataToken);
            int ilOffset = FindIlOffset(frame);

            return FindNearestLine(function, ilOffset);
        }
        private static FileAndLineNumber FindNearestLine(PdbFunction function, int ilOffset)
        {
            int distance = int.MaxValue;
            FileAndLineNumber nearest = new FileAndLineNumber();

            foreach (PdbSequencePointCollection sequenceCollection in function.SequencePoints)
            {
                foreach (PdbSequencePoint point in sequenceCollection.Lines)
                {
                    int dist = (int)Math.Abs(point.Offset - ilOffset);
                    if (dist < distance)
                    {
                        nearest.File = sequenceCollection.File.Name;
                        nearest.Line = (int)point.LineBegin;
                        nearest.ILOffset = point.Offset;
                    }
                }
            }

            return nearest;
        }

        private static int FindIlOffset(ClrStackFrame frame)
        {
            ulong ip = frame.InstructionPointer;
            int last = -1;
            foreach (ILToNativeMap item in frame.Method.ILOffsetMap)
            {
                if (item.StartAddress > ip)
                    return last;

                if (ip <= item.EndAddress)
                    return item.ILOffset;

                last = item.ILOffset;
            }

            return last;
        }


        private static PdbReader GetReaderForFrame(ClrStackFrame frame)
        {
            ClrModule module = frame.Method?.Type?.Module;
            PdbInfo info = module?.Pdb;
            
            if (info != null)
            {
                string pdbPath = frame.Runtime.DataTarget.SymbolLocator.FindPdb(info);
                if (pdbPath != null)
                    return new PdbReader(pdbPath);
            }

            return null;
        }
        

        struct FileAndLineNumber
        {
            public string File;
            public int Line;
            public uint ILOffset;
        }

        private static List<string> GetLocalNames(ClrStackFrame frame, FileAndLineNumber info)
        {
            List<string> localNames;
            string pdbPath = frame.Runtime.DataTarget.SymbolLocator.FindPdb(frame.Module.Pdb);
            if (pdbPath == null)
            {
                localNames = new List<string>(from i in Enumerable.Range(0, frame.Locals.Count) select string.Format("local_{0}", i));
            }
            else
            {
                PdbReader pdb = new PdbReader(pdbPath);

                PdbFunction function = pdb.GetFunctionFromToken(frame.Method.MetadataToken);
                PdbScope scope = function.FindScopeByILOffset(info.ILOffset);

                localNames = new List<string>(scope.GetRecursiveSlots().Select(s => s.Name));
            }

            return localNames;
        }

        private static IEnumerable<PdbSlot> GetRecursiveSlots(this PdbScope scope, List<PdbSlot> results = null)
        {
            if (results == null)
                results = new List<PdbSlot>();

            foreach (PdbSlot slot in scope.Slots)
            {
                while (results.Count <= slot.Slot)
                    results.Add(null);

                results[(int)slot.Slot] = slot;
            }

            foreach (PdbScope innerScope in scope.Scopes)
                innerScope.GetRecursiveSlots(results);

            return results;
        }

        private static List<string> GetParameterNames(ClrStackFrame frame)
        {
            IMetadataImport imd = frame.Module.MetadataImport;
            List<string> paramNames = new List<string>(frame.Arguments.Count);
            IntPtr paramEnum = IntPtr.Zero;
            uint fetched = 0;
            int paramDef;
            imd.EnumParams(ref paramEnum, (int)frame.Method.MetadataToken, out paramDef, 1, out fetched);

            StringBuilder sb = new StringBuilder(64);
            while (fetched == 1)
            {
                int pmd;
                uint pulSequence, pchName, pdwAttr, pdwCPlusTypeFlag, pcchValue;
                IntPtr ppValue;

                imd.GetParamProps(paramDef, out pmd, out pulSequence, sb, (uint)sb.Capacity, out pchName, out pdwAttr, out pdwCPlusTypeFlag, out ppValue, out pcchValue);

                paramNames.Add(sb.ToString());
                sb.Clear();

                imd.EnumParams(ref paramEnum, (int)frame.Method.MetadataToken, out paramDef, 1, out fetched);
            }

            imd.CloseEnum(paramEnum);
            return paramNames;
        }

        public static string TestWorkingDirectory { get { return _userSetWorkingPath ?? _workingPath.Value; } set { Debug.Assert(!_workingPath.IsValueCreated); _userSetWorkingPath = value; } }

        #region Working Path Helpers
        static string _userSetWorkingPath = null;
        static Lazy<string> _workingPath = new Lazy<string>(() => CreateWorkingPath(), true);
        private static string CreateWorkingPath()
        {
            Random r = new Random();
            string path;
            do
            {
                path = Path.Combine(Environment.CurrentDirectory, TempRoot + r.Next().ToString());
            } while (Directory.Exists(path));

            Directory.CreateDirectory(path);
            return path;
        }


        internal static readonly string TempRoot = "clrmd_removeme_";
        #endregion
    }

    [TestClass]
    public class GlobalCleanup
    {

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            foreach (string directory in Directory.GetDirectories(Environment.CurrentDirectory))
                if (directory.Contains(Helpers.TempRoot))
                    Directory.Delete(directory, true);
        }
    }
}