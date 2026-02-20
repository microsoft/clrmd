// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime;

class ClrStack
{
    static void Main(string[] args)
    {
        if (!TryParseArgs(args, out string? dump, out bool dso))
        {
            Console.WriteLine("Usage: ClrStack [-dso] crash.dmp");
            return;
        }

        // Create the data target.  This tells us the versions of CLR loaded in the target process.
        using DataTarget dataTarget = DataTarget.LoadDump(dump!);

        // Now check bitness of our program/target:
        bool isTarget64Bit = dataTarget.DataReader.PointerSize == 8;
        if (Environment.Is64BitProcess != isTarget64Bit)
            throw new InvalidOperationException($"Architecture mismatch:  Process is {(Environment.Is64BitProcess ? "64 bit" : "32 bit")} but target is {(isTarget64Bit ? "64 bit" : "32 bit")}");

        using ClrRuntime runtime = dataTarget.ClrVersions[0].CreateRuntime();

        foreach (ClrThread thread in runtime.Threads)
        {
            if (!thread.IsAlive)
                continue;

            Console.WriteLine("Thread {0:X}:", thread.OSThreadId);
            Console.WriteLine("Stack: {0:X} - {1:X}", thread.StackBase, thread.StackLimit);

            // Each thread tracks a "last thrown exception".
            if (thread.CurrentException is ClrException ex)
                Console.WriteLine("Exception: {0:X} ({1}), HRESULT={2:X}", ex.Address, ex.Type.Name, ex.HResult);

            Console.WriteLine();
            Console.WriteLine("Managed Callstack:");
            foreach (ClrStackFrame frame in thread.EnumerateStackTrace())
                Console.WriteLine($"    {frame.StackPointer:x12} {frame.InstructionPointer:x12} {frame}");

            // Print a !DumpStackObjects equivalent.
            if (dso)
            {
                ClrHeap heap = runtime.Heap;

                // StackBase/StackLimit is exactly what they are in the TEB.
                // This means StackBase > StackLimit on AMD64.
                ulong start = thread.StackBase;
                ulong stop = thread.StackLimit;

                if (start > stop)
                    (start, stop) = (stop, start);

                Console.WriteLine();
                Console.WriteLine("Stack objects:");

                for (ulong ptr = start; ptr <= stop; ptr += (uint)IntPtr.Size)
                {
                    if (!dataTarget.DataReader.ReadPointer(ptr, out ulong obj))
                        break;

                    ClrType? type = heap.GetObjectType(obj);
                    if (type is null)
                        continue;

                    if (!type.IsFree)
                        Console.WriteLine("{0,16:X} {1,16:X} {2}", ptr, obj, type.Name);
                }
            }

            Console.WriteLine();
            Console.WriteLine("----------------------------------");
            Console.WriteLine();
        }
    }

    public static bool TryParseArgs(string[] args, out string? dump, out bool dso)
    {
        dump = null;
        dso = false;

        foreach (string arg in args)
        {
            if (arg == "-dso")
            {
                dso = true;
            }
            else if (dump is null)
            {
                dump = arg;
            }
            else
            {
                Console.WriteLine("Too many arguments.");
                return false;
            }
        }

        return dump is not null;
    }
}
