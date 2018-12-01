// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct COR_HEAPOBJECT
    {
        public ulong address; // The address (in heap) of the object.
        public ulong size; // The total size of the object.
        public COR_TYPEID type; // The fully instantiated type of the object.
    }
}