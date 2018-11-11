// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime
{
    public class DacLibrary
    {
        private readonly IntPtr _library;
        private SOSDac _sos;
        private int _refCount;

        internal DacDataTargetWrapper DacDataTarget { get; }

        public ClrDataProcess DacPrivateInterface { get; }

        public SOSDac SOSDacInterface
        {
            get
            {
                if (_sos == null)
                    _sos = DacPrivateInterface.GetSOSDacInterface();

                return _sos;
            }
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
            _refCount = 1;
            DacPrivateInterface = new ClrDataProcess(this, pUnk);
        }

        public DacLibrary(DataTarget dataTarget, string dacDll)
        {
            _refCount = 1;
            if (dataTarget.ClrVersions.Count == 0)
                throw new ClrDiagnosticsException(String.Format("Process is not a CLR process!"));

            _library = DataTarget.PlatformFunctions.LoadLibrary(dacDll);
            if (_library == IntPtr.Zero)
                throw new ClrDiagnosticsException("Failed to load dac: " + dacDll);

            IntPtr initAddr = DataTarget.PlatformFunctions.GetProcAddress(_library, "DAC_PAL_InitializeDLL");
            if (initAddr != IntPtr.Zero)
            {
                IntPtr dllMain = DataTarget.PlatformFunctions.GetProcAddress(_library, "DllMain");
                DllMain main = (DllMain)Marshal.GetDelegateForFunctionPointer(dllMain, typeof(DllMain));
                int result = main(_library, 1, IntPtr.Zero);
            }


            IntPtr addr = DataTarget.PlatformFunctions.GetProcAddress(_library, "CLRDataCreateInstance");
            DacDataTarget = new DacDataTargetWrapper(dataTarget);

            CreateDacInstance func = (CreateDacInstance)Marshal.GetDelegateForFunctionPointer(addr, typeof(CreateDacInstance));
            Guid guid = new Guid("5c552ab6-fc09-4cb3-8e36-22fa03c798b7");
            int res = func(ref guid, DacDataTarget.IDacDataTarget, out IntPtr iUnk);

            if (res != 0)
                throw new ClrDiagnosticsException("Failure loading DAC: CreateDacInstance failed 0x" + res.ToString("x"), ClrDiagnosticsException.HR.DacError);


            DacPrivateInterface = new ClrDataProcess(this, iUnk);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DllMain(IntPtr instance, int reason, IntPtr reserved);


        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int PAL_Initialize();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateDacInstance(ref Guid riid,
                               IntPtr dacDataInterface,
                               out IntPtr ppObj);

        public DacLibrary AddRef()
        {
            Interlocked.Increment(ref _refCount);
            return this;
        }

        public void Release()
        {
            int newRefCount = Interlocked.Decrement(ref _refCount);
            Debug.Assert(newRefCount >= 0);
            if (newRefCount != 0)
                return;
            if (_library != IntPtr.Zero)
                DataTarget.PlatformFunctions.FreeLibrary(_library);
        }
    }
}
