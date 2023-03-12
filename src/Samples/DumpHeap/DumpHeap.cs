// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine($"Usage: dumpheap.exe [crashdump]");
            Environment.Exit(1);
        }

        Dictionary<ulong, (int Count, ulong Size, string Name)> stats = new Dictionary<ulong, (int Count, ulong Size, string Name)>();

        using DataTarget dataTarget = DataTarget.LoadDump(args[0]);
        foreach (ClrInfo clr in dataTarget.ClrVersions)
        {
            using ClrRuntime runtime = clr.CreateRuntime();
            ClrHeap heap = runtime.Heap;

            Console.WriteLine("{0,16} {1,16} {2,8} {3}", "Object", "MethodTable", "Size", "Type");
            foreach (ClrObject obj in heap.EnumerateObjects())
            {
                Console.WriteLine($"{obj.Address:x16} {obj.Type.MethodTable:x16} {obj.Size,8:D} {obj.Type.Name}");

                if (!stats.TryGetValue(obj.Type.MethodTable, out (int Count, ulong Size, string Name) item))
                    item = (0, 0, obj.Type.Name);

                stats[obj.Type.MethodTable] = (item.Count + 1, item.Size + obj.Size, item.Name);
            }

            Console.WriteLine("\nStatistics:");
            var sorted = from i in stats
                         orderby i.Value.Size ascending
                         select new
                         {
                             i.Key,
                             i.Value.Name,
                             i.Value.Size,
                             i.Value.Count
                         };

            Console.WriteLine("{0,16} {1,12} {2,12}\t{3}", "MethodTable", "Count", "Size", "Type");
            foreach (var item in sorted)
                Console.WriteLine($"{item.Key:x16} {item.Count,12:D} {item.Size,12:D}\t{item.Name}");

            Console.WriteLine($"Total {sorted.Sum(x => x.Count):0} objects");

            #region >>> Above code sample output below, similiar to !sos.dumpheap
            //            Object MethodTable     Size Type
            //000001d1445a1000 000001d13e08bf90       24 Free
            //000001d1445a1018 000001d13e08bf90       24 Free
            //000001d1445a1030 000001d13e08bf90       24 Free
            //000001d1445a1048 00007ffb05430638      152 System.RuntimeType + RuntimeTypeCache
            //000001d1445a10e0 00007ffb0552eed0       56 System.RuntimeType + RuntimeTypeCache + MemberInfoCache < System.Reflection.RuntimeConstructorInfo >
            //000001d1445a1118 00007ffb0552ec60      104 System.Reflection.RuntimeConstructorInfo
            //000001d1445a1180 00007ffb0552f078       32 System.Reflection.RuntimeConstructorInfo[]
            //...
            //000001d5745a3030 000001d13e08bf90       32 Free
            //000001d5745a3050 00007ffb05316610     8184 System.Object[]

            //Statistics:
            //     MethodTable        Count         Size Type
            //00007ffb0577d1f8            1           24  Microsoft.Extensions.Logging.Configuration.LoggerProviderConfigurationFactory
            //00007ffb05a26c00            1           24  Microsoft.Extensions.Options.OptionsCache < Microsoft.Extensions.Logging.Console.ConsoleLoggerOptions >
            //00007ffb05a49c78            1           24  Microsoft.Extensions.Options.OptionsMonitor < Microsoft.Extensions.Logging.Console.ConsoleLoggerOptions >
            //00007ffb05a49dc0            1           24  Microsoft.Extensions.Primitives.ChangeToken + ChangeTokenRegistration < System.String >
            //00007ffb05a49e70            1           24  Microsoft.Extensions.Primitives.ChangeToken + ChangeTokenRegistration < System.String >
            //00007ffb05a4be00            1           24  Microsoft.Extensions.Logging.NullExternalScopeProvider
            //00007ffb05a4cf90            1           24  Microsoft.Extensions.Configuration.ConfigurationBinder +<> c
            //00007ffb05a4cd80            1           24  Microsoft.Extensions.Configuration.BinderOptions
            //...
            //00007ffb053ef090         2010       111364  System.Int32[]
            //00007ffb053d2aa8         2224       126352  System.SByte[]
            //00007ffb05413058         1297       260138  System.Char[]
            //00007ffb05316610         2160       331512  System.Object[]
            //00007ffb053d2360          841       558172  System.Byte[]
            //00007ffb053d1e18         8366       718666  System.String
            //Total 82567 objects
            #endregion
        }
    }
}
