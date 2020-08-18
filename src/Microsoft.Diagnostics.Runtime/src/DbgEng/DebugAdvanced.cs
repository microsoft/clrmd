// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DbgEng
{
    internal unsafe sealed class DebugAdvanced : CallableCOMWrapper
    {
        internal static readonly Guid IID_IDebugAdvanced = new Guid("f2df5f53-071f-47bd-9de6-5734c3fed689");

        public DebugAdvanced(RefCountedFreeLibrary library, IntPtr pUnk, DebugSystemObjects sys)
            : base(library, IID_IDebugAdvanced, pUnk)
        {
            _sys = sys;
            SuppressRelease();
        }

        private ref readonly IDebugAdvancedVTable VTable => ref Unsafe.AsRef<IDebugAdvancedVTable>(_vtable);

        public HResult GetThreadContext(Span<byte> context)
        {
            InitDelegate(ref _getThreadContext, VTable.GetThreadContext);

            using IDisposable holder = _sys.Enter();
            fixed (byte* ptr = context)
                return _getThreadContext(Self, ptr, context.Length);
        }

        private readonly DebugSystemObjects _sys;

        private GetThreadContextDelegate? _getThreadContext;
        private delegate HResult GetThreadContextDelegate(IntPtr self, byte* context, int contextSize);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct IDebugAdvancedVTable
    {
        public readonly IntPtr GetThreadContext;
        public readonly IntPtr SetThreadContext;
    }
}
