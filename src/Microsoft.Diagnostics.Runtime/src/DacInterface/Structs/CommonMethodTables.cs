// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CommonMethodTables
    {
        public readonly ulong ArrayMethodTable;
        public readonly ulong StringMethodTable;
        public readonly ulong ObjectMethodTable;
        public readonly ulong ExceptionMethodTable;
        public readonly ulong FreeMethodTable;

        internal bool Validate()
        {
            return ArrayMethodTable != 0 &&
                StringMethodTable != 0 &&
                ObjectMethodTable != 0 &&
                ExceptionMethodTable != 0 &&
                FreeMethodTable != 0;
        }
    }
}