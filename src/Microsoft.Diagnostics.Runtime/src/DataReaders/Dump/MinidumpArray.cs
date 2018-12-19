// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    ///  Minidumps have a common variable length list structure for modules and threads implemented
    ///  as an array.
    ///  MINIDUMP_MODULE_LIST, MINIDUMP_THREAD_LIST, and MINIDUMP_THREAD_EX_LIST are the three streams
    ///  which use this implementation.
    ///  Others are similar in idea, such as MINIDUMP_THREAD_INFO_LIST, but are not the
    ///  same implementation and will not work with this class.  Thus, although this class
    ///  is generic, it's currently tightly bound to the implementation of those three streams.
    ///  This is a var-args structure defined as:
    ///    ULONG32 NumberOfNodesInList;
    ///    T ListNodes[];
    /// </summary>
    internal class MinidumpArray<T>
    {
        protected MinidumpArray(DumpPointer streamPointer, MINIDUMP_STREAM_TYPE streamType)
        {
            if (streamType != MINIDUMP_STREAM_TYPE.ModuleListStream &&
                streamType != MINIDUMP_STREAM_TYPE.ThreadListStream &&
                streamType != MINIDUMP_STREAM_TYPE.ThreadExListStream)
            {
                throw new ClrDiagnosticsException("MinidumpArray does not support this stream type.", ClrDiagnosticsExceptionKind.CrashDumpError);
            }

            _streamPointer = streamPointer;
        }

        private DumpPointer _streamPointer;

        public uint Count => _streamPointer.ReadUInt32();

        public T GetElement(uint idx)
        {
            if (idx > Count)
            {
                // Since the callers here are internal, a request out of range means a
                // corrupted dump file.
                throw new ClrDiagnosticsException("Dump error: index " + idx + "is out of range.", ClrDiagnosticsExceptionKind.CrashDumpError);
            }

            // Although the Marshal.SizeOf(...) is not necessarily correct, it is nonetheless
            // how we're going to pull the bytes back from the dump in PtrToStructure
            // and so if it's wrong we have to fix it up anyhow.  This would have to be an incorrect
            //  code change on our side anyhow; these are public native structs whose size is fixed.
            // MINIDUMP_MODULE    : 0n108 bytes
            // MINIDUMP_THREAD    : 0n48 bytes
            // MINIDUMP_THREAD_EX : 0n64 bytes
            const uint OffsetOfArray = 4;
            uint offset = OffsetOfArray + idx * (uint)Marshal.SizeOf(typeof(T));

            T element = _streamPointer.PtrToStructure<T>(+offset);
            return element;
        }
    }
}