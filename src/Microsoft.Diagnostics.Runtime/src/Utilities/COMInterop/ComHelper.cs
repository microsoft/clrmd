// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// Base class for COM related objects in ClrMD.
    /// </summary>
    public abstract class COMHelper
    {
        protected const int S_OK = 0;
        protected const int E_INVALIDARG = unchecked((int)0x80070057);
        protected const int E_FAIL = unchecked((int)0x80004005);
        protected const int E_NOTIMPL = unchecked((int)0x80004001);
        protected const int E_NOINTERFACE = unchecked((int)0x80004002);

        protected readonly Guid IUnknownGuid = new Guid("00000000-0000-0000-C000-000000000046");

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate int AddRefDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate int ReleaseDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate int QueryInterfaceDelegate(IntPtr self, ref Guid guid, out IntPtr ptr);

        /// <summary>
        /// Release an IUnknown pointer.
        /// </summary>
        /// <param name="pUnk">A pointer to the IUnknown interface to release.</param>
        /// <returns>The result of pUnk->Release().</returns>
        public static unsafe int Release(IntPtr pUnk)
        {
            if (pUnk == IntPtr.Zero)
                return 0;

            IUnknownVTable* vtable = *(IUnknownVTable**)pUnk;

            ReleaseDelegate release = (ReleaseDelegate)Marshal.GetDelegateForFunctionPointer(vtable->Release, typeof(ReleaseDelegate));
            return release(pUnk);
        }
    }
}