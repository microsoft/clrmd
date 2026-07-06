// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class DacLibrary : IDisposable
    {
        private bool _disposed;
        private readonly ClrDataProcess _clrDataProcess;

        /// <summary>
        /// A single lock that serializes calls into stateful DAC entry points (stack walks,
        /// handle enumeration, etc.). The DAC is not thread-safe: even calls on different
        /// entities (e.g. two ClrThreads' stack walks) share internal DAC state and can
        /// corrupt each other when run concurrently. Most steady-state work hits ClrMD's
        /// own caches and never acquires this lock; only the cold-miss DAC enumeration
        /// paths take it.
        /// </summary>
        public object SyncRoot { get; } = new();

        public DacDataTarget DacDataTarget { get; }

        public RefCountedFreeLibrary? OwningLibrary { get; }

        public TargetProperties TargetProperties { get; }

        public ClrDataProcess CreateClrDataProcess() => new(this, _clrDataProcess);

        public unsafe DacLibrary(DataTarget dataTarget, string dacPath, ulong runtimeBaseAddress, ulong contractDescriptor, bool verifySignature)
        {
            if (dataTarget is null)
                throw new ArgumentNullException(nameof(dataTarget));

            if (dataTarget.ClrVersions.Length == 0)
                throw new ClrDiagnosticsException("Process is not a CLR process!");

            if (string.IsNullOrEmpty(dacPath))
                throw new ArgumentNullException(nameof(dacPath));

            // Resolve to a canonical absolute path so the signature-verification override callback and the
            // load below operate on a full path (callers may pass a relative or non-normalized path).
            dacPath = Path.GetFullPath(dacPath);

            TargetProperties = new TargetProperties(pointerSize: dataTarget.DataReader.PointerSize);

            IDisposable? fileLock = null;
            IntPtr dacLibrary;
            try
            {
                // VerifyDacDll uses WinVerifyTrust, which only exists on windows.  For non-windows platforms, we do
                // not verify signatures of the DAC.  We also do not download DACs from the internet on those platforms,
                // leaving it up to the consumer of ClrMD to safely procure the DAC.
                // A host can override the verification decision per-DAC via DataTargetOptions.DacSignatureVerificationOverride,
                // which takes priority over the VerifyDacOnWindows flag (verifySignature) passed down here. This lets a host
                // trust a DAC it procured securely (e.g. a cDAC bundled in a signed tool package) while verifying others.
                bool verify = verifySignature;
                if (dataTarget.Options.DacSignatureVerificationOverride is { } shouldVerify)
                    verify = shouldVerify(dacPath);

                if (verify && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (!AuthenticodeUtil.VerifyDacDll(dacPath, out fileLock))
                    {
                        throw new ClrDiagnosticsException("Failed to load dac: not properly signed.");
                    }
                }

                try
                {
                    dacLibrary = DataTarget.PlatformFunctions.LoadLibrary(dacPath);
                }
                catch (Exception e) when (e is DllNotFoundException or BadImageFormatException)
                {
                    throw new ClrDiagnosticsException("Failed to load dac library.", e);
                }
            }
            finally
            {
                fileLock?.Dispose();
            }

            // On non-Windows platforms, calling dlclose on the DAC library can crash the process
            // when inspecting the current process.  The DAC (libmscordaccore.so) shares internal
            // state with the running CLR (libcoreclr.so), and unloading it corrupts that state.
            // Suppress FreeLibrary when the target is the current process to avoid this.
            //
            // The cDAC (mscordaccore_universal) is a NativeAOT library that cannot be safely
            // unloaded on any platform, so never free it either.
#if NET6_0_OR_GREATER
            int currentPid = Environment.ProcessId;
#else
            int currentPid;
            using (Process p = Process.GetCurrentProcess())
                currentPid = p.Id;
#endif
            bool suppressFree = DotNetClrInfoProvider.IsCDacFileName(dacPath)
                                || (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                    && dataTarget.DataReader.ProcessId == currentPid);
            OwningLibrary = new RefCountedFreeLibrary(dacLibrary, suppressFree);

            IntPtr initAddr = DataTarget.PlatformFunctions.GetLibraryExport(dacLibrary, "DAC_PAL_InitializeDLL");
            if (initAddr == IntPtr.Zero)
                initAddr = DataTarget.PlatformFunctions.GetLibraryExport(dacLibrary, "PAL_InitializeDLL");

            if (initAddr != IntPtr.Zero)
            {
                IntPtr dllMain = DataTarget.PlatformFunctions.GetLibraryExport(dacLibrary, "DllMain");
                if (dllMain == IntPtr.Zero)
                    throw new ClrDiagnosticsException("Failed to obtain dac DllMain");

                delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr, int> main = (delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr, int>)dllMain;
                main(dacLibrary, 1, IntPtr.Zero);
            }

            IntPtr addr = DataTarget.PlatformFunctions.GetLibraryExport(dacLibrary, "CLRDataCreateInstance");
            if (addr == IntPtr.Zero)
                throw new ClrDiagnosticsException("Failed to obtain Dac CLRDataCreateInstance");

            DacDataTarget = new DacDataTarget(dataTarget, runtimeBaseAddress, contractDescriptor);

            delegate* unmanaged[Stdcall]<in Guid, IntPtr, out IntPtr, int> func = (delegate* unmanaged[Stdcall]<in Guid, IntPtr, out IntPtr, int>)addr;
            Guid guid = new("5c552ab6-fc09-4cb3-8e36-22fa03c798b7");

#if NET6_0_OR_GREATER
            IntPtr iDacDataTarget = DacDataTargetCOM.CreateIDacDataTarget(DacDataTarget);
            int res = func(guid, iDacDataTarget, out nint iUnk);
            Marshal.Release(iDacDataTarget);
#else
            LegacyDacDataTargetWrapper wrapper = new(DacDataTarget);
            int res = func(guid, wrapper.IDacDataTarget, out nint iUnk);
            GC.KeepAlive(wrapper);
#endif

            unchecked
            {
                if ((uint)res == 0x80131c4f)
                    throw new ClrDiagnosticsException($"Failure loading DAC: CreateDacInstance failed 0x{res:x}, which usually indicates the dump file was taken incorrectly.", res);
                else if (res != 0)
                    throw new ClrDiagnosticsException($"Failure loading DAC: CreateDacInstance failed 0x{res:x}", res);
            }

            _clrDataProcess = new ClrDataProcess(this, iUnk);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DacLibrary()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    _clrDataProcess?.Dispose();

                OwningLibrary?.Release();

                _disposed = true;
            }
        }
    }
}