// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public unsafe struct RuntimeInfo
    {
        public const string SymbolValue = "DotNetRuntimeInfo";
        public const string SignatureValue = "DotNetRuntimeInfo\0";
        public const int SignatureValueLength = 18;

        public fixed byte Signature[18];
        public int Version;
        public fixed byte RuntimeModuleIndex[24];
        public fixed byte DacModuleIndex[24];
        public fixed byte DbiModuleIndex[24];
    }
}