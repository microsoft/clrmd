// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    ///  If the dump doesn't have memory contents, we can try to load the file
    ///  off disk and report as if memory contents were present.
    ///  Run through loader to simplify getting the in-memory layout correct, rather than using a FileStream
    ///  and playing around with trying to mimic the loader.
    /// </summary>
    internal class LoadedFileMemoryLookups
    {
        private readonly Dictionary<string, SafeLoadLibraryHandle> _files;

        public LoadedFileMemoryLookups()
        {
            _files = new Dictionary<string, SafeLoadLibraryHandle>();
        }

        public unsafe void GetBytes(string fileName, ulong offset, IntPtr destination, uint bytesRequested, ref uint bytesWritten)
        {
            bytesWritten = 0;
            IntPtr file;
            // Did we already attempt to load this file?
            // Only makes one attempt to load a file.
            if (!_files.ContainsKey(fileName))
            {
                //TODO: real code here to get the relocations right without loading would be nice, but
                // that's a significant amount of code - especially if you intend to compensate for linker bugs.
                // The easiest way to accomplish this would be to build on top of dbgeng.dll which already
                // does all this for you.  Then you can also use dbghelp & get all your module and symbol
                // loading for free, with full integration with symbol servers.
                //
                // In the meantime, this  doesn't actually exec any code from the module
                // we load.  Mdbg should be done loading modules for itself, so if we happen to load some
                // module in common with mdbg we'll be fine because this call will be second.
                // Lifetime issues could be important if we load some module here and do not release it back
                // to the OS before mdbg loads it subsequently to execute it.
                // Also, note that rebasing will not be correct, so raw assembly addresses will be relative
                // to the base address of the module in mdbg's process, not the base address in the dump.
                file = WindowsFunctions.NativeMethods.LoadLibraryEx(fileName, 0, WindowsFunctions.NativeMethods.LoadLibraryFlags.DontResolveDllReferences);
                _files[fileName] = new SafeLoadLibraryHandle(file);
                //TODO: Attempted file load order is NOT guaranteed, so the uncertainty will make output order non-deterministic.
                // Find/create an appropriate global verbosity setting.
            }
            else
            {
                file = _files[fileName].BaseAddress;
            }

            // Did we actually succeed loading this file?
            if (!file.Equals(IntPtr.Zero))
            {
                file = new IntPtr((byte*)file.ToPointer() + offset);
                InternalGetBytes(file, destination, bytesRequested, ref bytesWritten);
            }
        }

        private unsafe void InternalGetBytes(IntPtr src, IntPtr dest, uint bytesRequested, ref uint bytesWritten)
        {
            // Do the raw copy.
            byte* pSrc = (byte*)src.ToPointer();
            byte* pDest = (byte*)dest.ToPointer();
            for (bytesWritten = 0; bytesWritten < bytesRequested; bytesWritten++)
            {
                pDest[bytesWritten] = pSrc[bytesWritten];
            }
        }
    }
}