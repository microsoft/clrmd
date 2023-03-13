// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Please go to the ClrMD project page on github for full source and to report issues:
//    https://github.com/Microsoft/clrmd

using System;
using Microsoft.Diagnostics.Runtime;
using System.IO;
using System.Linq;

static class Program
{

    static void Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.WriteLine("Usage: DumpDict [dump] [objref]");
            Environment.Exit(1);
        }

        if (ulong.TryParse(args[2], System.Globalization.NumberStyles.HexNumber, null, out ulong objAddr))
        {
            Console.WriteLine($"Could not parse object ref '{args[2]}'.");
            Environment.Exit(1);
        }

        string dumpFileName = args[0];
        string dacPath = args[1];

        using DataTarget dataTarget = DataTarget.LoadDump(dumpFileName);
        using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
        ClrHeap heap = runtime.Heap;

        ClrObject obj = heap.GetObject(objAddr);

        if (!obj.IsValidObject)
        {
            Console.WriteLine("Invalid object {0:X}", obj);
            return;
        }

        if (!obj.Type.Name.StartsWith("System.Collections.Generic.Dictionary"))
        {
            Console.WriteLine("Error: Expected object {0:X} to be a dictionary, instead it's of type '{1}'.");
            return;
        }

        // Get the entries field.
        ClrArray entries = obj.ReadObjectField("entries").AsArray();

        Console.WriteLine("{0,8} {1,16} : {2}", "hash", "key", "value");
        for (int i = 0; i < entries.Length; ++i)
        {
            ClrObject entry = entries.GetObjectValue(i);

            // TODO: Need to handle non-object references
            int hashCode = entry.ReadField<int>("hashCode");
            ClrObject key = entry.ReadObjectField("key");
            ClrObject value = entry.ReadObjectField("value");

            Console.WriteLine($"{hashCode:x} {key} -> {value}");
        }
    }
}
