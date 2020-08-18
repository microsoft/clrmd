// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

#nullable enable

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    // Static variables are not "roots" in the strictest sense.  They are always rooted, but the GC does not consider
    // them to be specifically roots.  ClrMD 1.1 would report these as roots because it's convenient to treat them as
    // roots when reporting to the user why an object is alive.  However, that code takes a very long time, and it
    // wasn't providing an accurate view of the runtime.  If you need those back or to find what object addresses
    // are you can use this method (or reimplement it yourself).
    public static class StaticRootsExtension
    {
        public static IEnumerable<(ClrStaticField Field, ClrObject Object)> EnumerateAllStaticVariables(this ClrRuntime runtime)
        {
            if (runtime is null)
                throw new ArgumentNullException(nameof(runtime));

            foreach (ClrModule module in runtime.EnumerateModules())
            {
                foreach ((ulong mt, int _) in module.EnumerateTypeDefToMethodTableMap())
                {
                    ClrType? type = runtime.GetTypeByMethodTable(mt);

                    if (type is null)
                        continue;

                    foreach (ClrStaticField field in type.StaticFields)
                    {
                        if (field.IsObjectReference)
                        {
                            foreach (ClrAppDomain domain in runtime.AppDomains)
                            {
                                ClrObject obj = field.ReadObject(domain);
                                if (obj.IsValid && !obj.IsNull)
                                    yield return (field, obj);
                            }
                        }
                    }
                }
            }
        }
    }
}
