// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable 649

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct MethodTableTokenPair
    {
        public ulong MethodTable { get; set; }
        public uint Token { get; set; }

        public MethodTableTokenPair(ulong methodTable, uint token)
        {
            MethodTable = methodTable;
            Token = token;
        }
    }
}