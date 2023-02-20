using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;
using static Microsoft.Diagnostics.Runtime.DacInterface.SOSDac;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    /// <summary>
    /// This is an undocumented, untested, and unsupported interface.  Do not use directly.
    /// </summary>
    public sealed unsafe class SOSDac13 : CallableCOMWrapper
    {
        internal static readonly Guid IID_ISOSDac13 = new("3176a8ed-597b-4f54-a71f-83695c6a8c5d");

        public SOSDac13(DacLibrary library, IntPtr ptr)
            : base(library?.OwningLibrary, IID_ISOSDac13, ptr)
        {
        }

        private ref readonly ISOSDac13VTable VTable => ref Unsafe.AsRef<ISOSDac13VTable>(_vtable);

        public HResult TraverseLoaderHeap(ulong heap, LoaderHeapKind kind, LoaderHeapTraverse callback)
        {
            HResult hr = VTable.TraverseLoaderHeap(Self, heap, kind, Marshal.GetFunctionPointerForDelegate(callback));
            GC.KeepAlive(callback);
            return hr;
        }

        /// <summary>
        /// The type of the underlying loader heap.
        /// </summary>
        public enum LoaderHeapKind
        {
            /// <summary>
            /// A LoaderHeap in the CLR codebase.
            /// </summary>
            LoaderHeapKindNormal = 0,

            /// <summary>
            /// An ExplicitControlLoaderHeap in the CLR codebase.
            /// </summary>
            LoaderHeapKindExplicitControl = 1
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly unsafe struct ISOSDac13VTable
        {
            public readonly delegate* unmanaged[Stdcall]<IntPtr, ClrDataAddress, LoaderHeapKind, IntPtr, int> TraverseLoaderHeap;
        }
    }
}
