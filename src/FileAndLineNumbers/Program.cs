using Microsoft.Diagnostics.Runtime.Tests;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.Runtime.Utilities.Pdb;
using Microsoft.Diagnostics.Runtime.ICorDebug;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        Helpers.TestWorkingDirectory = Environment.CurrentDirectory;

        using (DataTarget dt = TestTargets.NestedException.LoadFullDump())
        {
            dt.SymbolLocator.SymbolPath += ";" + Environment.CurrentDirectory;
            ClrRuntime runtime = dt.ClrVersions.Single().CreateRuntime();

            foreach (ClrThread thread in runtime.Threads)
            {
                Console.WriteLine("Thread {0:x}:", thread.OSThreadId);
                
                foreach (ClrStackFrame frame in thread.StackTrace)
                {
                    if (frame.Kind == ClrStackFrameType.Runtime)
                    {
                        Console.WriteLine("{0,12:x} {1,12:x} {2}", frame.InstructionPointer, frame.StackPointer, frame.DisplayString);
                    }
                    else
                    {
                        FileAndLineNumber info = frame.GetSourceLocation();
                        Console.WriteLine("{0,12:x} {1,12:x} {2} [{3} @ {4}]", frame.InstructionPointer, frame.StackPointer, frame.DisplayString, info.File, info.Line);
                        if (frame.Arguments.Count > 0)
                        {
                            List<string> paramNames = GetParameterNames(frame);

                            Console.WriteLine("Arguments:");
                            for (int i = 0; i < frame.Arguments.Count; i++)
                                Console.WriteLine("    {0} = {1}", paramNames[i], frame.Arguments[i]);

                            Console.WriteLine();
                        }


                        if (frame.Locals.Count > 0)
                        {
                            List<string> localNames = GetLocalNames(frame, info);

                            Console.WriteLine("Locals:");
                            for (int i = 0; i < frame.Locals.Count; i++)
                                Console.WriteLine("    {0} = {1}", localNames[i], frame.Locals[i]);

                            Console.WriteLine();
                        }
                    }
                }
            }
        }
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
}

struct FileAndLineNumber
{
    public string File;
    public int Line;
    public uint ILOffset;
}

static class Extensions
{
    public static IEnumerable<PdbSlot> GetRecursiveSlots(this PdbScope scope, List<PdbSlot> results = null)
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

    static Dictionary<PdbInfo, PdbReader> s_pdbReaders = new Dictionary<PdbInfo, PdbReader>();
    public static FileAndLineNumber GetSourceLocation(this ClrStackFrame frame)
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

        PdbReader reader = null;
        if (info != null)
        {
            if (!s_pdbReaders.TryGetValue(info, out reader))
            {
                SymbolLocator locator = GetSymbolLocator(module);
                string pdbPath = locator.FindPdb(info);
                if (pdbPath != null)
                    reader = new PdbReader(pdbPath);

                s_pdbReaders[info] = reader;
            }
        }

        return reader;
    }

    private static SymbolLocator GetSymbolLocator(ClrModule module)
    {
        return module.Runtime.DataTarget.SymbolLocator;
    }
}