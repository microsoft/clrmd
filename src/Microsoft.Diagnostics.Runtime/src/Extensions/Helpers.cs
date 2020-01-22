// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    internal static class Helpers
    {
        public static ClrType? GetTypeByName(this ClrModule module, string name)
        {
            foreach ((ulong mt, int _) in module.EnumerateTypeDefToMethodTableMap())
            {
                ClrType? type = module.AppDomain.Runtime.GetTypeByMethodTable(mt);
                if (type?.Name == name)
                    return type;
            }

            return null;
        }
    }
}
