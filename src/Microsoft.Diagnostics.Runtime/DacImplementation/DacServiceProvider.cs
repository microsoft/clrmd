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

        // These service singletons are read on a lock-free fast path in GetService and
        // published under _serviceLock, so they must be volatile to give the fast-path
        // read an acquire barrier (otherwise a partially-constructed instance could be
        // observed on a weak memory model).
        private volatile IAbstractClrNativeHeaps? _nativeHeaps;
        private volatile IAbstractComHelpers? _com;
        private volatile IAbstractHeap? _heapHelper;
        private volatile IAbstractLegacyThreadPool? _threadPool;
        private volatile IAbstractMethodLocator? _methodLocator;
        private volatile DacModuleHelpers? _moduleHelper;
        private volatile IAbstractRuntime? _runtime;
        private volatile IAbstractThreadHelpers? _threadHelper;
        private volatile IAbstractTypeHelpers? _typeHelper;

        private bool _disposed;
        private readonly object _serviceLock = new();

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

            // Serialize teardown against in-flight DAC reads: every DAC entry point takes
            // _sos.SyncRoot (== _dac.SyncRoot), so disposing the native/COM wrappers under
            // that same lock guarantees no reader is mid-call when we free them.
            lock (_sos.SyncRoot)
            {
                if (_disposed)
                    return;

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
        }

        public object? GetService(Type serviceType)
        {
            // Fast path: return already-initialized services without locking.
            // Lock only when we need to construct a new service singleton so two
            // racing callers can't each produce their own DacRuntime / DacHeap /
            // DacTypeHelpers (each with its own caches, which would silently diverge).
            if (serviceType == typeof(IAbstractRuntime))
            {
                IAbstractRuntime? r = _runtime;
                if (r is not null)
                    return r;
                lock (_serviceLock)
                    return _runtime ??= new DacRuntime(_clrInfo, _process, _sos, _sos13, _dac.TargetProperties);
            }

            if (serviceType == typeof(IAbstractHeap))
            {
                IAbstractHeap? heap = _heapHelper;
                if (heap is not null)
                    return heap;

                lock (_serviceLock)
                {
                    heap = _heapHelper;
                    if (heap is not null)
                        return heap;

                    GCInfo data = default;
                    CommonMethodTables mts = default;
                    bool initialized;
                    lock (_sos.SyncRoot)
                    {
                        initialized = _sos.GetGCHeapData(out data) && _sos.GetCommonMethodTables(out mts) && !mts.ObjectMethodTable.IsNull;
                    }

                    if (initialized)
                        return _heapHelper = new DacHeap(_sos, _sos8, _sos12, _sos16, _dataReader, _dac.TargetProperties, data, mts);

                    return null;
                }
            }

            if (serviceType == typeof(IAbstractTypeHelpers))
            {
                IAbstractTypeHelpers? t = _typeHelper;
                if (t is not null)
                    return t;
                lock (_serviceLock)
                {
                    _moduleHelper ??= new(_sos, _dac.TargetProperties);
                    return _typeHelper ??= new DacTypeHelpers(_process, _sos, _sos6, _sos8, _sos14, _dataReader, _moduleHelper, _dac.TargetProperties);
                }
            }

            if (serviceType == typeof(IAbstractClrNativeHeaps))
            {
                IAbstractClrNativeHeaps? n = _nativeHeaps;
                if (n is not null)
                    return n;
                lock (_serviceLock)
                    return _nativeHeaps ??= new DacNativeHeaps(_clrInfo, _sos, _sos13, _dataReader, _dac.TargetProperties);
            }

            if (serviceType == typeof(IAbstractModuleHelpers))
            {
                DacModuleHelpers? m = _moduleHelper;
                if (m is not null)
                    return m;
                lock (_serviceLock)
                    return _moduleHelper ??= new DacModuleHelpers(_sos, _dac.TargetProperties);
            }

            if (serviceType == typeof(IAbstractComHelpers))
            {
                IAbstractComHelpers? c = _com;
                if (c is not null)
                    return c;
                lock (_serviceLock)
                    return _com ??= new DacComHelpers(_sos, _dac.TargetProperties);
            }

            if (serviceType == typeof(IAbstractLegacyThreadPool))
            {
                IAbstractLegacyThreadPool? p = _threadPool;
                if (p is not null)
                    return p;
                lock (_serviceLock)
                    return _threadPool ??= new DacLegacyThreadPool(_sos, _dac.TargetProperties);
            }

            if (serviceType == typeof(IAbstractMethodLocator))
            {
                IAbstractMethodLocator? l = _methodLocator;
                if (l is not null)
                    return l;
                lock (_serviceLock)
                    return _methodLocator ??= new DacMethodLocator(_sos, _dataReader, _dac.TargetProperties);
            }

            if (serviceType == typeof(IAbstractThreadHelpers))
            {
                IAbstractThreadHelpers? th = _threadHelper;
                if (th is not null)
                    return th;
                lock (_serviceLock)
                    return _threadHelper ??= new DacThreadHelpers(_process, _sos, _dataReader, _dac.TargetProperties);
            }

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

            // The DAC is not thread-safe: Flush tears down internal DAC structures
            // (handle tables, stack-walk state, type caches). Concurrent DAC reads
            // observe truncated/empty enumerations during the flush window. Serialize
            // on the same lock the readers take (SOSDac.SyncRoot is DacLibrary.SyncRoot).
            lock (_sos.SyncRoot)
            {
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
                        _sos.GetWorkRequestData(ClrDataAddress.FromTargetAddress(DacDataTarget.MagicCallbackConstant, _dac.TargetProperties), out _);
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
}