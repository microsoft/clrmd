// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime
{
    internal sealed class DacLibrary : IDisposable
    {
        private bool _disposed;
        private readonly ClrDataProcess _clrDataProcess;

        public DacDataTarget DacDataTarget { get; }

        public RefCountedFreeLibrary? OwningLibrary { get; }

        public ClrDataProcess CreateClrDataProcess() => new(this, _clrDataProcess);

        public unsafe DacLibrary(DataTarget dataTarget, string dacPath, ulong runtimeBaseAddress)
        {
            if (dataTarget is null)
                throw new ArgumentNullException(nameof(dataTarget));

            if (dataTarget.ClrVersions.Length == 0)
                throw new ClrDiagnosticsException("Process is not a CLR process!");

            IntPtr dacLibrary;
            try
            {
                dacLibrary = DataTarget.PlatformFunctions.LoadLibrary(dacPath);
            }
            catch (Exception e) when (e is DllNotFoundException or BadImageFormatException)
            {
                throw new ClrDiagnosticsException("Failed to load dac: " + e.Message, e);
            }

            OwningLibrary = new RefCountedFreeLibrary(dacLibrary);

            IntPtr initAddr = DataTarget.PlatformFunctions.GetLibraryExport(dacLibrary, "DAC_PAL_InitializeDLL");
            if (initAddr == IntPtr.Zero)
                initAddr = DataTarget.PlatformFunctions.GetLibraryExport(dacLibrary, "PAL_InitializeDLL");

            if (initAddr != IntPtr.Zero)
            {
                IntPtr dllMain = DataTarget.PlatformFunctions.GetLibraryExport(dacLibrary, "DllMain");
                if (dllMain == IntPtr.Zero)
                    throw new ClrDiagnosticsException("Failed to obtain Dac DllMain");

                delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr, int> main = (delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr, int>)dllMain;
                main(dacLibrary, 1, IntPtr.Zero);
            }

            IntPtr addr = DataTarget.PlatformFunctions.GetLibraryExport(dacLibrary, "CLRDataCreateInstance");
            if (addr == IntPtr.Zero)
                throw new ClrDiagnosticsException("Failed to obtain Dac CLRDataCreateInstance");

            DacDataTarget = new DacDataTarget(dataTarget, runtimeBaseAddress);

            delegate* unmanaged[Stdcall]<in Guid, IntPtr, out IntPtr, int> func = (delegate* unmanaged[Stdcall]<in Guid, IntPtr, out IntPtr, int>)addr;
            Guid guid = new("5c552ab6-fc09-4cb3-8e36-22fa03c798b7");

#if NET6_0_OR_GREATER
            IntPtr iDacDataTarget = DacDataTargetCOM.CreateIDacDataTarget(DacDataTarget);
            int res = func(guid, iDacDataTarget, out nint iUnk);
            Marshal.Release(iDacDataTarget);
#else
            LegacyDacDataTargetWrapper wrapper = new(DacDataTarget, DacDataTarget.RuntimeBaseAddress != 0);
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

        private void Dispose(bool _)
        {
            if (!_disposed)
            {
                _clrDataProcess?.Dispose();
                OwningLibrary?.Release();

                _disposed = true;
            }
        }
    }
}