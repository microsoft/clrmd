// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    public readonly struct NativeCodeHeader
    {
        public readonly ulong GCInfo;
        public readonly ulong EHInfo;
        public readonly ulong MethodStart;
        public readonly uint MethodSize;
    }
}