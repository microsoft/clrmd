// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal class DacServiceProvider : IServiceProvider, IDisposable, IAbstractDacController
    {
        private readonly ClrInfo _clrInfo;
        private readonly IDataReader _dataReader;

        private readonly DacLibrary _dac;
        private readonly ClrDataProcess _process;
        private readonly SOSDac _sos;
        private readonly SOSDac6? _sos6;
        private readonly SOSDac8? _sos8;
        private readonly SosDac12? _sos12;
        private readonly ISOSDac13? _sos13;
        private readonly SosDac14? _sos14;
        private readonly ISOSDac16? _sos16;

        private IAbstractClrNativeHeaps? _nativeHeaps;
        private IAbstractComHelpers? _com;
        private IAbstractHeap? _heapHelper;
        private IAbstractLegacyThreadPool? _threadPool;
        private IAbstractMethodLocator? _methodLocator;
        private DacModuleHelpers? _moduleHelper;
        private IAbstractRuntime? _runtime;
        private IAbstractThreadHelpers? _threadHelper;
        private IAbstractTypeHelpers? _typeHelper;

        private bool _disposed;

        public DacServiceProvider(ClrInfo clrInfo, DacLibrary library)
        {
            _clrInfo = clrInfo;
            _dataReader = _clrInfo.DataTarget.DataReader;

            _dac = library;
            _process = library.CreateClrDataProcess();
            _sos = _process.CreateSOSDacInterface() ?? throw new InvalidOperationException($"Could not create ISOSDacInterface.");
            _sos6 = _process.CreateSOSDacInterface6();
            _sos8 = _process.CreateSOSDacInterface8();
            _sos12 = _process.CreateSOSDacInterface12();
            _sos13 = _process.CreateSOSDacInterface13();
            _sos14 = _process.CreateSOSDacInterface14();
            _sos16 = _process.CreateSOSDacInterface16();

            library.DacDataTarget.SetMagicCallback(_process.Flush);
            IsThreadSafe = _sos13 is not null || RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        public void Dispose()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            _disposed = true;
            _sos13?.LockedFlush();
            _process.Dispose();
            _sos.Dispose();
            _sos6?.Dispose();
            _sos8?.Dispose();
            _sos12?.Dispose();
            _sos13?.Dispose();
            _sos14?.Dispose();
            _sos16?.Dispose();
            _dac.Dispose();
            _moduleHelper?.Dispose();
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IAbstractRuntime))
                return _runtime ??= new DacRuntime(_clrInfo, _process, _sos, _sos13);

            if (serviceType == typeof(IAbstractHeap))
            {
                IAbstractHeap? heap = _heapHelper;
                if (heap is not null)
                    return heap;

                if (_sos.GetGCHeapData(out GCInfo data) && _sos.GetCommonMethodTables(out CommonMethodTables mts) && mts.ObjectMethodTable != 0)
                    return _heapHelper = new DacHeap(_sos, _sos8, _sos12, _sos16, _dataReader, data, mts);

                return null;
            }

            if (serviceType == typeof(IAbstractTypeHelpers))
            {
                _moduleHelper ??= new(_sos);
                return _typeHelper ??= new DacTypeHelpers(_process, _sos, _sos6, _sos8, _sos14, _dataReader, _moduleHelper);
            }

            if (serviceType == typeof(IAbstractClrNativeHeaps))
                return _nativeHeaps ??= new DacNativeHeaps(_clrInfo, _sos, _sos13, _dataReader);

            if (serviceType == typeof(IAbstractModuleHelpers))
                return _moduleHelper ??= new DacModuleHelpers(_sos);

            if (serviceType == typeof(IAbstractComHelpers))
                return _com ??= new DacComHelpers(_sos);

            if (serviceType == typeof(IAbstractLegacyThreadPool))
                return _threadPool ??= new DacLegacyThreadPool(_sos);

            if (serviceType == typeof(IAbstractMethodLocator))
                return _methodLocator ??= new DacMethodLocator(_sos);

            if (serviceType == typeof(IAbstractThreadHelpers))
                return _threadHelper ??= new DacThreadHelpers(_process, _sos, _dataReader);

            if (serviceType == typeof(IAbstractDacController))
                return this;

            if (serviceType == typeof(DacLibrary))
                return _dac;

            return null;
        }

        public bool IsThreadSafe { get; }

        public bool CanFlush => true;

        public void Flush()
        {
            if (!CanFlush)
                throw new InvalidOperationException($"This version of CLR does not support the Flush operation.");

            _moduleHelper?.Flush();
            if (_sos13 is not null)
            {
                _sos13.LockedFlush();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // IXClrDataProcess::Flush is unfortunately not wrapped with DAC_ENTER.  This means that
                // when it starts deleting memory, it's completely unsynchronized with parallel reads
                // and writes, leading to heap corruption and other issues.  This means that in order to
                // properly clear dac data structures, we need to trick the dac into entering the critical
                // section for us so we can call Flush safely then.

                // To accomplish this, we set a hook in our implementation of IDacDataTarget::ReadVirtual
                // which will call IXClrDataProcess::Flush if the dac tries to read the address set by
                // MagicCallbackConstant.  Additionally we make sure this doesn't interfere with other
                // reads by 1) Ensuring that the address is in kernel space, 2) only calling when we've
                // entered a special context.

                _dac.DacDataTarget.EnterMagicCallbackContext();
                try
                {
                    _sos.GetWorkRequestData(DacDataTarget.MagicCallbackConstant, out _);
                }
                finally
                {
                    _dac.DacDataTarget.ExitMagicCallbackContext();
                }
            }
            else
            {
                // On Linux/MacOS, skip the above workaround because calling Flush() in the DAC data target's
                // ReadVirtual function can cause a SEGSIGV because of an access of freed memory causing the
                // tool/app running CLRMD to crash. On Windows, it would be caught by the SEH try/catch handler
                // in DAC enter/leave code.

                _process.Flush();
            }
        }
    }
}