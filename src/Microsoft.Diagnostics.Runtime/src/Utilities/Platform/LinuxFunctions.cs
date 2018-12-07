using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class LinuxFunctions : PlatformFunctions
    {
        public override bool GetFileVersion(string dll, out int major, out int minor, out int revision, out int patch)
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
        {
            IntPtr h;

            try
            {
                h = DlV2.dlopen(filename, RTLD_NOW);
            }
            catch (DllNotFoundException)
            {
                h = DlV1.dlopen(filename, RTLD_NOW);
            }

            if (h != default)
                return h;

            string m;
            try
            {
                var p = DlV2.dlerror();
                m = p == default ? "Unknown error." : Marshal.PtrToStringAnsi(p);
            }
            catch (DllNotFoundException)
            {
                var p = DlV1.dlerror();
                m = p == default ? "Unknown error." : Marshal.PtrToStringAnsi(p);
            }

            throw new InvalidOperationException($"Error loading library {filename}", new Exception(m));
        }

        public override bool FreeLibrary(IntPtr module)
        {
            try
            {
                return DlV2.dlclose(module) == 0;
            }
            catch (DllNotFoundException)
            {
                return DlV1.dlclose(module) == 0;
            }
        }

        public override IntPtr GetProcAddress(IntPtr module, string method)
        {
            try
            {
                return DlV2.dlsym(module, method);
            }
            catch (DllNotFoundException)
            {
                return DlV1.dlsym(module, method);
            }
        }

        //const int RTLD_LOCAL  = 0x000;
        //const int RTLD_LAZY   = 0x001;
        const int RTLD_NOW = 0x002;
        //const int RTLD_GLOBAL = 0x100;

        internal static class DlV1
        {
            [DllImport("dl")]
            internal static extern IntPtr dlopen(string filename, int flags);

            [DllImport("dl")]
            internal static extern int dlclose(IntPtr module);

            [DllImport("dl")]
            internal static extern IntPtr dlsym(IntPtr handle, string symbol);

            [DllImport("dl")]
            internal static extern IntPtr dlerror();
        }

        internal static class DlV2
        {
            [DllImport("libdl.so.2")]
            internal static extern IntPtr dlopen(string filename, int flags);

            [DllImport("libdl.so.2")]
            internal static extern int dlclose(IntPtr module);

            [DllImport("libdl.so.2")]
            internal static extern IntPtr dlsym(IntPtr handle, string symbol);

            [DllImport("libdl.so.2")]
            internal static extern IntPtr dlerror();
        }

        // NOTE: IOV_MAX is 1024 or something
        // libc.so.6

        [DllImport("c", EntryPoint = "process_vm_readv", SetLastError=true)]
        internal static extern IntPtr ProcessVmReadV(
            int pid,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
            iovec[] localIoVec,
            ulong localIovCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]
            iovec[] remoteIoVec,
            ulong remoteIoVecCount,
            ulong flags = 0);

        [DllImport("c", EntryPoint = "process_vm_writev", SetLastError=true)]
        internal static extern IntPtr ProcessVmWriteV(
            int pid,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
            iovec localIoVec,
            ulong localIoVecCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]
            iovec remoteIoVec,
            ulong remoteIoVecCount,
            ulong flags = 0);

        [DllImport("c", EntryPoint = "ptrace")]
        internal static extern long PTrace(
            PTraceRequest request,
            int pid,
            IntPtr address,
            IntPtr data = default
        );
    }
}