// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct HandleData
    {
        public readonly ClrDataAddress AppDomain;
        public readonly ClrDataAddress Handle;
        public readonly ClrDataAddress Secondary;
        public readonly uint Type;
        public readonly uint StrongReference;

        // For RefCounted Handles
        public readonly uint RefCount;
        public readonly uint JupiterRefCount;
        public readonly uint IsPegged;
    }
}