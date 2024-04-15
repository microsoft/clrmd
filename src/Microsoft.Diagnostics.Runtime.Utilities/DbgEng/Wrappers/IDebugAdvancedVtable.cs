// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct IDebugAdvancedVtable
    {
        private readonly nint QueryInterface;
        private readonly nint AddRef;
        private readonly nint Release;

        public readonly delegate* unmanaged[Stdcall]<nint, byte*, int, int> GetThreadContext;
        public readonly IntPtr SetThreadContext;

        // IDebugAdvanced2 methods
        public readonly delegate* unmanaged[Stdcall]<nint, uint, void*, int, void*, int, int*, int> Request;
        public readonly IntPtr GetSourceFileInformation;
        public readonly IntPtr FindSourceFileAndToken;
        public readonly IntPtr GetSymbolInformation;
        public readonly IntPtr GetSystemObjectInformation;
    }
}