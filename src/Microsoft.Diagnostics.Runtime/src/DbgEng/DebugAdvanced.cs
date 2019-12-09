// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DbgEng
{
    internal unsafe sealed class DebugAdvanced : CallableCOMWrapper
    {
        internal static Guid IID_IDebugAdvanced = new Guid("f2df5f53-071f-47bd-9de6-5734c3fed689");
        private IDebugAdvancedVTable* VTable => (IDebugAdvancedVTable*)_vtable;
        public DebugAdvanced(RefCountedFreeLibrary library, IntPtr pUnk, DebugSystemObjects sys)
            : base(library, ref IID_IDebugAdvanced, pUnk)
        {
            _sys = sys;
            SuppressRelease();
        }

        public bool GetThreadContext(Span<byte> context)
        {
            InitDelegate(ref _getThreadContext, VTable->GetThreadContext);

            using IDisposable holder = _sys.Enter();
            fixed (byte* ptr = context)
                return _getThreadContext(Self, ptr, context.Length) >= 0;
        }

        private GetThreadContextDelegate? _getThreadContext;
        private readonly DebugSystemObjects _sys;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetThreadContextDelegate(IntPtr self, byte* context, int contextSize);
    }

#pragma warning disable CS0169
#pragma warning disable CS0649
#pragma warning disable IDE0051
#pragma warning disable CA1823

    internal struct IDebugAdvancedVTable
    {
        public readonly IntPtr GetThreadContext;
        public readonly IntPtr SetThreadContext;
    }
}

