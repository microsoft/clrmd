// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime
{
    public sealed class DacLibrary : IDisposable
    {
        private bool _disposed;
        private SOSDac _sos;

        internal DacDataTargetWrapper DacDataTarget { get; }

        internal RefCountedFreeLibrary OwningLibrary { get; }

        internal ClrDataProcess InternalDacPrivateInterface { get; }

        public ClrDataProcess DacPrivateInterface => new ClrDataProcess(InternalDacPrivateInterface);

        internal SOSDac GetSOSInterfaceNoAddRef()
        {
            if (_sos == null)
                _sos = InternalDacPrivateInterface.GetSOSDacInterface();

            return _sos;
        }

        public SOSDac SOSDacInterface
        {
            get
            {
                var sos = GetSOSInterfaceNoAddRef();

                return sos != null ? new SOSDac(sos) : null;
            }
        }

        public T GetInterface<T>(ref Guid riid)
            where T : CallableCOMWrapper
        {
            var pUnknown = InternalDacPrivateInterface.QueryInterface(ref riid);
            if (pUnknown == IntPtr.Zero)
                return null;

            var t = (T)Activator.CreateInstance(typeof(T), this, pUnknown);
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

        internal DacLibrary(DataTargetImpl dataTarget, IntPtr pUnk)
        {
            InternalDacPrivateInterface = new ClrDataProcess(this, pUnk);
        }

        public DacLibrary(DataTarget dataTarget, string dacDll)
        {
            if (dataTarget.ClrVersions.Count == 0)
                throw new ClrDiagnosticsException("Process is not a CLR process!");

            var dacLibrary = DataTarget.PlatformFunctions.LoadLibrary(dacDll);
            if (dacLibrary == IntPtr.Zero)
                throw new ClrDiagnosticsException("Failed to load dac: " + dacLibrary);

            OwningLibrary = new RefCountedFreeLibrary(dacLibrary);
            dataTarget.AddDacLibrary(this);

            var initAddr = DataTarget.PlatformFunctions.GetProcAddress(dacLibrary, "DAC_PAL_InitializeDLL");
            if (initAddr == IntPtr.Zero)
                initAddr = DataTarget.PlatformFunctions.GetProcAddress(dacLibrary, "PAL_InitializeDLL");

            if (initAddr != IntPtr.Zero)
            {
                var dllMain = DataTarget.PlatformFunctions.GetProcAddress(dacLibrary, "DllMain");
                var main = (DllMain)Marshal.GetDelegateForFunctionPointer(dllMain, typeof(DllMain));
                var result = main(dacLibrary, 1, IntPtr.Zero);
            }

            var addr = DataTarget.PlatformFunctions.GetProcAddress(dacLibrary, "CLRDataCreateInstance");
            DacDataTarget = new DacDataTargetWrapper(dataTarget);

            var func = (CreateDacInstance)Marshal.GetDelegateForFunctionPointer(addr, typeof(CreateDacInstance));
            var guid = new Guid("5c552ab6-fc09-4cb3-8e36-22fa03c798b7");
            var res = func(ref guid, DacDataTarget.IDacDataTarget, out var iUnk);

            if (res != 0)
                throw new ClrDiagnosticsException("Failure loading DAC: CreateDacInstance failed 0x" + res.ToString("x"), ClrDiagnosticsException.HR.DacError);

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

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                InternalDacPrivateInterface?.Dispose();
                _sos?.Dispose();
                OwningLibrary?.Release();

                _disposed = true;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DllMain(IntPtr instance, int reason, IntPtr reserved);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int PAL_Initialize();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateDacInstance(
            ref Guid riid,
            IntPtr dacDataInterface,
            out IntPtr ppObj);
    }
}