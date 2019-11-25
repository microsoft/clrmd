using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.DbgEng
{
    internal unsafe sealed class DebugAdvanced : CallableCOMWrapper
    {
        internal static Guid IID_IDebugAdvanced = new Guid("27fe5639-8407-4f47-8364-ee118fb08ac8");
        private IDebugAdvancedVTable* VTable => (IDebugAdvancedVTable*)_vtable;
        public DebugAdvanced(RefCountedFreeLibrary library, IntPtr pUnk)
            : base(library, ref IID_IDebugAdvanced, pUnk)
        {
        }

        public bool GetThreadContext(Span<byte> context)
        {
            InitDelegate(ref _getThreadContext, VTable->GetThreadContext);

            fixed (byte *ptr = context)
                return _getThreadContext(Self, ptr, context.Length) >= 0;
        }

        private GetThreadContextDelegate _getThreadContext;
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetThreadContextDelegate(IntPtr self, byte *context, int contextSize);
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

