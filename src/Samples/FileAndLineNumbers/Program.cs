using Microsoft.Diagnostics.Runtime.Tests;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.Runtime.Utilities.Pdb;

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
                    }
                }
            }
        }
    }
}

struct FileAndLineNumber
{
    public string File;
    public int Line;
}

static class Extensions
{
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