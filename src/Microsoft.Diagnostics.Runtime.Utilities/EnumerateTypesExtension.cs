// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

#nullable enable

namespace Microsoft.Diagnostics.Runtime
{
    // We don't actually have a way to enumerate all "types" in the process.  ClrHeap.EnumerateTypes
    // was an algorithm designed to help you find constructed types.  This was removed in ClrMD 2.0
    // because it's incredibly slow.  This extension method reimplements that functionality, but
    // you should read through its implementation to be sure you understand what it's doing.
    public static class EnumerateTypesExtension
    {
        /// <summary>
        /// Enumerates types with constructed method tables in all modules.
        /// </summary>
        /// <param name="heap"></param>
        /// <returns></returns>
        public static IEnumerable<ClrType> EnumerateTypes(this ClrHeap heap)
        {
            if (heap is null)
                throw new ArgumentNullException(nameof(heap));

            // The ClrHeap actually doesn't know anything about 'types' in the strictest sense, that's
            // all tracked by the runtime.  First, grab the runtime object:

            ClrRuntime runtime = heap.Runtime;

            // Now we loop through every module and grab every constructed MethodTable
            foreach (ClrModule module in runtime.EnumerateModules())
            {
                foreach ((ulong mt, int _) in  module.EnumerateTypeDefToMethodTableMap())
                {
                    // Now try to construct a type for mt.  This may fail if the type was only partially
                    // loaded, dump inconsistency, and in some odd corner cases like transparent proxies:
                    ClrType? type = runtime.GetTypeByMethodTable(mt);

                    if (type != null)
                        yield return type;
                }
            }
        }
    }
}
