// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Please go to the ClrMD project page on github for full source and to report issues:
//    https://github.com/Microsoft/clrmd

using System;
using Microsoft.Diagnostics.Runtime;

class ClrStack
{
    static void Main(string[] args)
    {
        if (!TryParseArgs(args, out string dump, out bool dso))
        {
            Usage();
            return;
        }

        // Create the data target.  This tells us the versions of CLR loaded in the target process.
        using DataTarget dataTarget = DataTarget.LoadDump(dump);

        // Now check bitness of our program/target:
        bool isTarget64Bit = dataTarget.DataReader.PointerSize == 8;
        if (Environment.Is64BitProcess != isTarget64Bit)
            throw new Exception(string.Format("Architecture mismatch:  Process is {0} but target is {1}", Environment.Is64BitProcess ? "64 bit" : "32 bit", isTarget64Bit ? "64 bit" : "32 bit"));

        // Note I just take the first version of CLR in the process.  You can loop over every loaded
        // CLR to handle the SxS case where both desktop CLR and .Net Core are loaded in the process.
        ClrInfo version = dataTarget.ClrVersions[0];

        // Now that we have the DataTarget, the version of CLR, and the right dac, we create and return a
        // ClrRuntime instance.
        using ClrRuntime runtime = version.CreateRuntime();

        // Walk each thread in the process.
        foreach (ClrThread thread in runtime.Threads)
        {
            // The ClrRuntime.Threads will also report threads which have recently died, but their
            // underlying datastructures have not yet been cleaned up.  This can potentially be
            // useful in debugging (!threads displays this information with XXX displayed for their
            // OS thread id).  You cannot walk the stack of these threads though, so we skip them
            // here.
            if (!thread.IsAlive)
                continue;

            Console.WriteLine("Thread {0:X}:", thread.OSThreadId);
            Console.WriteLine("Stack: {0:X} - {1:X}", thread.StackBase, thread.StackLimit);

            // Each thread tracks a "last thrown exception".  This is the exception object which
            // !threads prints.  If that exception object is present, we will display some basic
            // exception data here.  Note that you can get the stack trace of the exception with
            // ClrHeapException.StackTrace (we don't do that here).
            ClrException? currException = thread.CurrentException;
            if (currException is ClrException ex)
                Console.WriteLine("Exception: {0:X} ({1}), HRESULT={2:X}", ex.Address, ex.Type.Name, ex.HResult);

            // Walk the stack of the thread and print output similar to !ClrStack.
            Console.WriteLine();
            Console.WriteLine("Managed Callstack:");
            foreach (ClrStackFrame frame in thread.EnumerateStackTrace())
            {
                // Note that CLRStackFrame currently only has three pieces of data: stack pointer,
                // instruction pointer, and frame name (which comes from ToString).  Future
                // versions of this API will allow you to get the type/function/module of the
                // method (instead of just the name).  This is not yet implemented.
                Console.WriteLine($"    {frame.StackPointer:x12} {frame.InstructionPointer:x12} {frame}");
            }

            // Print a !DumpStackObjects equivalent.
            if (dso)
            {
                // We'll need heap data to find objects on the stack.
                ClrHeap heap = runtime.Heap;

                // Walk each pointer aligned address on the stack.  Note that StackBase/StackLimit
                // is exactly what they are in the TEB.  This means StackBase > StackLimit on AMD64.
                ulong start = thread.StackBase;
                ulong stop = thread.StackLimit;

                // We'll walk these in pointer order.
                if (start > stop)
                {
                    ulong tmp = start;
                    start = stop;
                    stop = tmp;
                }

                Console.WriteLine();
                Console.WriteLine("Stack objects:");

                // Walk each pointer aligned address.  Ptr is a stack address.
                for (ulong ptr = start; ptr <= stop; ptr += (uint)IntPtr.Size)
                {
                    // Read the value of this pointer.  If we fail to read the memory, break.  The
                    // stack region should be in the crash dump.
                    if (!dataTarget.DataReader.ReadPointer(ptr, out ulong obj))
                        break;

                    // 003DF2A4 
                    // We check to see if this address is a valid object by simply calling
                    // GetObjectType.  If that returns null, it's not an object.
                    ClrType type = heap.GetObjectType(obj);
                    if (type == null)
                        continue;

                    // Don't print out free objects as there tends to be a lot of them on
                    // the stack.
                    if (!type.IsFree)
                        Console.WriteLine("{0,16:X} {1,16:X} {2}", ptr, obj, type.Name);
                }
            }

            Console.WriteLine();
            Console.WriteLine("----------------------------------");
            Console.WriteLine();
        }
    }

    public static bool TryParseArgs(string[] args, out string dump, out bool dso)
    {
        dump = null;
        dso = false;

        foreach (string arg in args)
        {
            if (arg == "-dso")
            {
                dso = true;
            }
            else if (dump == null)
            {
                dump = arg;
            }
            else
            {
                Console.WriteLine("Too many arguments.");
                return false;
            }
        }

        return dump != null;
    }


    public static void Usage()
    {
        string fn = System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        Console.WriteLine($"Usage: {fn} [-dso] crash.dmp");
    }
}
