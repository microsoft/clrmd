// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime;

if (args.Length != 2)
{
    Console.WriteLine("Usage: DumpDict [dump] [objref]");
    return 1;
}

if (!ulong.TryParse(args[1], System.Globalization.NumberStyles.HexNumber, null, out ulong objAddr))
{
    Console.WriteLine($"Could not parse object ref '{args[1]}'.");
    return 1;
}

using DataTarget dataTarget = DataTarget.LoadDump(args[0]);
using ClrRuntime runtime = dataTarget.ClrVersions.Single().CreateRuntime();
ClrHeap heap = runtime.Heap;

ClrObject obj = heap.GetObject(objAddr);

if (!obj.IsValid)
{
    Console.WriteLine("Invalid object {0:X}", obj.Address);
    return 1;
}

if (!obj.Type!.Name!.StartsWith("System.Collections.Generic.Dictionary"))
{
    Console.WriteLine($"Error: Expected object {obj.Address:X} to be a dictionary, instead it's of type '{obj.Type.Name}'.");
    return 1;
}

// Get the entries field.
ClrArray entries = obj.ReadObjectField("_entries").AsArray();

Console.WriteLine("{0,8} {1,16} : {2}", "hash", "key", "value");
for (int i = 0; i < entries.Length; ++i)
{
    ClrValueType entry = entries.GetStructValue(i);

    int hashCode = entry.ReadField<int>("hashCode");
    ClrObject key = entry.ReadObjectField("key");
    ClrObject value = entry.ReadObjectField("value");

    Console.WriteLine($"{hashCode:x} {key} -> {value}");
}

return 0;
