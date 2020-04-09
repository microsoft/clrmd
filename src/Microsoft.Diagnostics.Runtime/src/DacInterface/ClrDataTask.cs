// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    internal unsafe class ClrDataTask : CallableCOMWrapper
    {
        private static readonly Guid IID_IXCLRDataTask = new Guid("A5B0BEEA-EC62-4618-8012-A24FFC23934C");

        public ClrDataTask(DacLibrary library, IntPtr pUnk)
            : base(library.OwningLibrary, IID_IXCLRDataTask, pUnk)
        {
        }

        private ref readonly ClrDataTaskVTable VTable => ref Unsafe.AsRef<ClrDataTaskVTable>(_vtable);

        public ClrStackWalk? CreateStackWalk(DacLibrary library, uint flags)
        {
            CreateStackWalkDelegate create = Marshal.GetDelegateForFunctionPointer<CreateStackWalkDelegate>(VTable.CreateStackWalk);
            if (!create(Self, flags, out IntPtr pUnk))
                return null;

            GC.KeepAlive(create);
            return new ClrStackWalk(library, pUnk);
        }

        private delegate HResult CreateStackWalkDelegate(IntPtr self, uint flags, out IntPtr stackwalk);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct ClrDataTaskVTable
    {
        private readonly IntPtr GetProcess;
        private readonly IntPtr GetCurrentAppDomain;
        private readonly IntPtr GetUniqueID;
        private readonly IntPtr GetFlags;
        private readonly IntPtr IsSameObject;
        private readonly IntPtr GetManagedObject;
        private readonly IntPtr GetDesiredExecutionState;
        private readonly IntPtr SetDesiredExecutionState;
        public readonly IntPtr CreateStackWalk;
        private readonly IntPtr GetOSThreadID;
        private readonly IntPtr GetContext;
        private readonly IntPtr SetContext;
        private readonly IntPtr GetCurrentExceptionState;
        private readonly IntPtr Request;
        private readonly IntPtr GetName;
        private readonly IntPtr GetLastExceptionState;
    }
}