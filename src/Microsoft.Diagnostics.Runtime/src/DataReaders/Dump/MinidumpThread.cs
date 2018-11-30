// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// Raw MINIDUMP_THREAD structure imported from DbgHelp.h
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal class MINIDUMP_THREAD
    {
        public uint ThreadId;

        // 0 if thread is not suspended.
        public uint SuspendCount;

        public uint PriorityClass;
        public uint Priority;

        // Target Address of Teb (Thread Environment block)
        private ulong _teb;
        public ulong Teb => DumpNative.ZeroExtendAddress(_teb);

        /// <summary>
        /// Describes the memory location of the thread's raw stack.
        /// </summary>
        public MINIDUMP_MEMORY_DESCRIPTOR Stack;

        public MINIDUMP_LOCATION_DESCRIPTOR ThreadContext;

        public virtual bool HasBackingStore()
        {
            return false;
        }

        public virtual MINIDUMP_MEMORY_DESCRIPTOR BackingStore
        {
            get => throw new MissingMemberException("MINIDUMP_THREAD has no backing store!");
            set => throw new MissingMemberException("MINIDUMP_THREAD has no backing store!");
        }
    }
}