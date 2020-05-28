// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class OSXFunctions : PlatformFunctions
    {
        private const string LibDl = "libdl.dylib";
        private const int RTLD_NOW = 2;

        public OSXFunctions()
        {
        }

        internal override unsafe bool GetFileVersion(string dll, out int major, out int minor, out int revision, out int patch)
        {
            major = minor = revision = patch = 0;
            return false;
        }

        public override bool TryGetWow64(IntPtr proc, out bool result)
        {
            result = false;
            return true;
        }

        public override IntPtr LoadLibrary(string filename) => dlopen(filename, RTLD_NOW);

        public override bool FreeLibrary(IntPtr module) => dlclose(module) == 0;

        public override IntPtr GetProcAddress(IntPtr module, string method) => dlsym(module, method);

        [DllImport(LibDl)]
        private static extern IntPtr dlopen(string filename, int flags);

        [DllImport(LibDl)]
        private static extern int dlclose(IntPtr module);

        [DllImport(LibDl)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);
    }
}