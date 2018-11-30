// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal static class DumpNative
    {
        /// <summary>
        /// Remove the OS sign-extension from a target address.
        /// </summary>
        public static ulong ZeroExtendAddress(ulong addr)
        {
            // Since we only support debugging targets of the same bitness, we can presume that
            // the target dump process's bitness matches ours and strip the high bits.
            if (IntPtr.Size == 4)
                return addr &= 0x00000000ffffffff;

            return addr;
        }

        private const uint MINIDUMP_SIGNATURE = 0x504d444d;
        private const uint MINIDUMP_VERSION = 0xa793;
        private const uint MiniDumpWithFullMemoryInfo = 0x0002;

        public static bool IsMiniDump(IntPtr pbase)
        {
            MINIDUMP_HEADER header = (MINIDUMP_HEADER)Marshal.PtrToStructure(pbase, typeof(MINIDUMP_HEADER));

            return (header.Flags & MiniDumpWithFullMemoryInfo) == 0;
        }

        public static bool MiniDumpReadDumpStream(IntPtr pBase, MINIDUMP_STREAM_TYPE type, out IntPtr streamPointer, out uint cbStreamSize)
        {
            MINIDUMP_HEADER header = (MINIDUMP_HEADER)Marshal.PtrToStructure(pBase, typeof(MINIDUMP_HEADER));

            streamPointer = IntPtr.Zero;
            cbStreamSize = 0;

            // todo: throw dump format exception here:
            if (header.Singature != MINIDUMP_SIGNATURE || (header.Version & 0xffff) != MINIDUMP_VERSION)
                return false;

            int sizeOfDirectory = Marshal.SizeOf(typeof(MINIDUMP_DIRECTORY));
            long dirs = pBase.ToInt64() + (int)header.StreamDirectoryRva;
            for (int i = 0; i < (int)header.NumberOfStreams; ++i)
            {
                MINIDUMP_DIRECTORY dir = (MINIDUMP_DIRECTORY)Marshal.PtrToStructure(new IntPtr(dirs + i * sizeOfDirectory), typeof(MINIDUMP_DIRECTORY));
                if (dir.StreamType != type)
                    continue;

                streamPointer = new IntPtr(pBase.ToInt64() + (int)dir.Rva);
                cbStreamSize = dir.DataSize;
                return true;
            }

            return false;
        }

        // From ntxcapi_x.h, for example
        public const uint EXCEPTION_MAXIMUM_PARAMETERS = 15;
    }
}