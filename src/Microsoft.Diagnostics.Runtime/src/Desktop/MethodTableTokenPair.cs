// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable 649

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct MethodTableTokenPair
    {
        public ulong MethodTable { get; }
        public uint Token { get; }

        public MethodTableTokenPair(ulong methodTable, uint token)
        {
            MethodTable = methodTable;
            Token = token;
        }
    }
}