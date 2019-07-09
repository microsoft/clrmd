// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class LinuxFunctions : PlatformFunctions
    {
        private const string LibDlGlibc = "libdl.so.2";
        private const string LibDl = "libdl.so";

        private readonly Func<string, IntPtr> _loadLibrary;
        private readonly Func<IntPtr, bool> _freeLibrary;
        private readonly Func<IntPtr, string, IntPtr> _getExport;

        private delegate bool TryGetExport(IntPtr handle, string name, out IntPtr address);

        public LinuxFunctions()
        {
            Type nativeLibraryType = Type.GetType("System.Runtime.InteropServices.NativeLibrary, System.Runtime.InteropServices", throwOnError: false);
            if (nativeLibraryType != null)
            {
                // .NET Core 3.0+
                _loadLibrary = (Func<string, IntPtr>)nativeLibraryType.GetMethod("Load", new Type[] { typeof(string) })?.CreateDelegate(typeof(Func<string, IntPtr>));

                var freeLibrary = (Action<IntPtr>)nativeLibraryType.GetMethod("Free", new Type[] { typeof(IntPtr) })?.CreateDelegate(typeof(Action<IntPtr>));
                if (freeLibrary != null)
                {
                    _freeLibrary = ptr => { freeLibrary(ptr); return true; };
                }

                var tryGetExport = (TryGetExport)nativeLibraryType.GetMethod("TryGetExport", new Type[] { typeof(IntPtr), typeof(string), typeof(IntPtr).MakeByRefType() })
                    ?.CreateDelegate(typeof(TryGetExport));
                if (tryGetExport != null)
                {
                    _getExport = (IntPtr handle, string name) => {
                        tryGetExport(handle, name, out IntPtr address);
                        return address;
                    };
                }
            }
            if (_loadLibrary == null ||
                _freeLibrary == null ||
                _getExport == null)
            {
                // On glibc based Linux distributions, 'libdl.so' is a symlink provided by development packages.
                // To work on production machines, we fall back to 'libdl.so.2' which is the actual library name.
                bool useGlibcDl = false;
                try
                {
                    dlopen("/", 0);
                }
                catch (DllNotFoundException)
                {
                    try
                    {
                        dlopen_glibc("/", 0);
                        useGlibcDl = true;
                    }
                    catch (DllNotFoundException)
                    { }
                }

                if (useGlibcDl)
                {
                    _loadLibrary = filename => dlopen_glibc(filename, RTLD_NOW);
                    _freeLibrary = ptr => dlclose_glibc(ptr) == 0;
                    _getExport = dlsym_glibc;
                }
                else
                {
                    _loadLibrary = filename => dlopen(filename, RTLD_NOW);
                    _freeLibrary = ptr => dlclose(ptr) == 0;
                    _getExport = dlsym;
                }
            }
        }

        internal override bool GetFileVersion(string dll, out int major, out int minor, out int revision, out int patch)
        {
            //TODO

            major = minor = revision = patch = 0;
            return true;
        }

        public override bool TryGetWow64(IntPtr proc, out bool result)
        {
            result = false;
            return true;
        }

        public override IntPtr LoadLibrary(string filename)
            => _loadLibrary(filename);

        public override bool FreeLibrary(IntPtr module)
            => _freeLibrary(module);

        public override IntPtr GetProcAddress(IntPtr module, string method)
            => _getExport(module, method);

        [DllImport(LibDlGlibc, EntryPoint = nameof(dlopen))]
        private static extern IntPtr dlopen_glibc(string filename, int flags);

        [DllImport(LibDlGlibc, EntryPoint = nameof(dlclose))]
        private static extern int dlclose_glibc(IntPtr module);

        [DllImport(LibDlGlibc, EntryPoint = nameof(dlsym))]
        private static extern IntPtr dlsym_glibc(IntPtr handle, string symbol);

        [DllImport(LibDl)]
        private static extern IntPtr dlopen(string filename, int flags);
        [DllImport(LibDl)]
        private static extern int dlclose(IntPtr module);
        [DllImport(LibDl)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport("libc")]
        public static extern int symlink(string file, string symlink);

        private const int RTLD_NOW = 2;
    }
}