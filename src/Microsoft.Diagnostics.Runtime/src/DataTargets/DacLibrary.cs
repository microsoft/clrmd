// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
    public sealed class DacLibrary : IDisposable
    {
        private bool _disposed;
        private SOSDac? _sos;

        internal DacDataTargetWrapper DacDataTarget { get; }

        public RefCountedFreeLibrary? OwningLibrary { get; }

        internal ClrDataProcess InternalDacPrivateInterface { get; }

        public ClrDataProcess DacPrivateInterface => new ClrDataProcess(this, InternalDacPrivateInterface);

        private SOSDac GetSOSInterfaceNoAddRef()
        {
            if (_sos is null)
            {
                _sos = InternalDacPrivateInterface.GetSOSDacInterface();
                if (_sos is null)
                    throw new InvalidOperationException("This runtime does not support ISOSDac.");
            }

            return _sos;
        }

        public SOSDac SOSDacInterface
        {
            get
            {
                SOSDac sos = GetSOSInterfaceNoAddRef();
                return new SOSDac(this, sos);
            }
        }

        public SOSDac6? SOSDacInterface6 => InternalDacPrivateInterface.GetSOSDacInterface6();

        public T? GetInterface<T>(in Guid riid)
            where T : CallableCOMWrapper
        {
            IntPtr pUnknown = InternalDacPrivateInterface.QueryInterface(riid);
            if (pUnknown == IntPtr.Zero)
                return null;

            T t = (T)Activator.CreateInstance(typeof(T), this, pUnknown)!;
            return t;
        }

        internal static IntPtr TryGetDacPtr(object ix)
        {
            if (!(ix is IntPtr pUnk))
            {
                if (Marshal.IsComObject(ix))
                    pUnk = Marshal.GetIUnknownForObject(ix);
                else
                    pUnk = IntPtr.Zero;
            }

            if (pUnk == IntPtr.Zero)
                throw new ArgumentException("clrDataProcess not an instance of IXCLRDataProcess");

            return pUnk;
        }

        public DacLibrary(DataTarget dataTarget, IntPtr pClrDataProcess)
        {
            if (dataTarget is null)
                throw new ArgumentNullException(nameof(dataTarget));

            if (pClrDataProcess == IntPtr.Zero)
                throw new ArgumentNullException(nameof(pClrDataProcess));

            InternalDacPrivateInterface = new ClrDataProcess(this, pClrDataProcess);
            DacDataTarget = new DacDataTargetWrapper(dataTarget);
        }

        public DacLibrary(DataTarget dataTarget, string dacDll)
        {
            if (dataTarget is null)
                throw new ArgumentNullException(nameof(dataTarget));

            if (dataTarget.ClrVersions.Length == 0)
                throw new ClrDiagnosticsException("Process is not a CLR process!");

            IntPtr dacLibrary = DataTarget.PlatformFunctions.LoadLibrary(dacDll);
            if (dacLibrary == IntPtr.Zero)
                throw new ClrDiagnosticsException("Failed to load dac: " + dacLibrary);

            OwningLibrary = new RefCountedFreeLibrary(dacLibrary);

            IntPtr initAddr = DataTarget.PlatformFunctions.GetProcAddress(dacLibrary, "DAC_PAL_InitializeDLL");
            if (initAddr == IntPtr.Zero)
                initAddr = DataTarget.PlatformFunctions.GetProcAddress(dacLibrary, "PAL_InitializeDLL");

            if (initAddr != IntPtr.Zero)
            {
                IntPtr dllMain = DataTarget.PlatformFunctions.GetProcAddress(dacLibrary, "DllMain");
                if (dllMain == IntPtr.Zero)
                    throw new ClrDiagnosticsException("Failed to obtain Dac DllMain");

                DllMain main = Marshal.GetDelegateForFunctionPointer<DllMain>(dllMain);
                main(dacLibrary, 1, IntPtr.Zero);
            }

            IntPtr addr = DataTarget.PlatformFunctions.GetProcAddress(dacLibrary, "CLRDataCreateInstance");
            if (addr == IntPtr.Zero)
                throw new ClrDiagnosticsException("Failed to obtain Dac CLRDataCreateInstance");

            DacDataTarget = new DacDataTargetWrapper(dataTarget);

            CreateDacInstance func = Marshal.GetDelegateForFunctionPointer<CreateDacInstance>(addr);
            Guid guid = new Guid("5c552ab6-fc09-4cb3-8e36-22fa03c798b7");
            int res = func(guid, DacDataTarget.IDacDataTarget, out IntPtr iUnk);

            if (res != 0)
                throw new ClrDiagnosticsException($"Failure loading DAC: CreateDacInstance failed 0x{res:x}", res);

            InternalDacPrivateInterface = new ClrDataProcess(this, iUnk);
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
                InternalDacPrivateInterface?.Dispose();
                _sos?.Dispose();
                OwningLibrary?.Release();

                _disposed = true;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int DllMain(IntPtr instance, int reason, IntPtr reserved);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int PAL_Initialize();

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CreateDacInstance(
            in Guid riid,
            IntPtr dacDataInterface,
            out IntPtr ppObj);
    }
}