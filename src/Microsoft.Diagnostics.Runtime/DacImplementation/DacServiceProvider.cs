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
        private readonly TargetProperties _target;

        private readonly DacLibrary? _dac;
        private readonly ClrDataProcess _process;
        private readonly SOSDac _sos;
        private readonly SOSDac6? _sos6;
        private readonly SOSDac8? _sos8;
        private readonly SosDac12? _sos12;
        private readonly ISOSDac13? _sos13;
        private readonly SosDac14? _sos14;
        private readonly ISOSDac16? _sos16;
        private readonly ISOSDac17? _sos17;

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
        private volatile IAbstractStressLog? _stressLog;

        private bool _disposed;
        private readonly object _serviceLock = new();

        public DacServiceProvider(ClrInfo clrInfo, DacLibrary library)
            : this(clrInfo, library, library.CreateClrDataProcess())
        {
        }

        /// <summary>
        /// Constructs a <see cref="DacServiceProvider"/> over an <c>IXCLRDataProcess</c> that the host
        /// (e.g. SOS) created and owns. ClrMD does not own or unload the underlying DAC module. The host
        /// retains its own reference to <paramref name="clrDataProcess"/>: this constructor adds a reference
        /// (balanced by a release on <see cref="Dispose"/>) so the host's reference is unaffected, but the
        /// host must keep the pointer alive for the lifetime of the resulting runtime.
        /// </summary>
        /// <param name="clrInfo">The runtime this DAC represents.</param>
        /// <param name="clrDataProcess">A host-owned <c>IXCLRDataProcess</c> pointer.</param>
        /// <param name="dacLock">
        /// The lock that serializes all DAC calls. Pass the host's lock to serialize the host's own DAC
        /// usage against ClrMD's; the DAC is not thread-safe and concurrent calls corrupt its state.
        /// </param>
        public DacServiceProvider(ClrInfo clrInfo, IntPtr clrDataProcess, object dacLock)
            : this(clrInfo, dac: null, CreateForeignProcess(clrInfo, clrDataProcess, dacLock))
        {
        }

        private DacServiceProvider(ClrInfo clrInfo, DacLibrary? dac, ClrDataProcess process)
        {
            _clrInfo = clrInfo;
            _dataReader = _clrInfo.DataTarget.DataReader;
            ThinLockLayout thinLockLayout = DacHeap.GetThinLockLayout(_clrInfo.Flavor, _clrInfo.Version);
            _target = new TargetProperties(thinLockLayout, _dataReader.PointerSize);

            _dac = dac;
            _process = process;
            try
            {
                _sos = _process.CreateSOSDacInterface() ?? throw new InvalidOperationException($"Could not create ISOSDacInterface.");
                _sos6 = _process.CreateSOSDacInterface6();
                _sos8 = _process.CreateSOSDacInterface8();
                _sos12 = _process.CreateSOSDacInterface12();
                _sos13 = _process.CreateSOSDacInterface13();
                _sos14 = _process.CreateSOSDacInterface14();
                _sos16 = _process.CreateSOSDacInterface16();
                _sos17 = _process.CreateSOSDacInterface17();
            }
            catch
            {
                // Construction failed after we took ownership of the process (and, on the host path, its
                // AddRef'd IXCLRDataProcess). Release everything created here so the pointer/interfaces do
                // not leak. Disposing _process releases the reference CreateForeignProcess added.
                SOSDac? sos = _sos;
                sos?.Dispose();
                _sos6?.Dispose();
                _sos8?.Dispose();
                _sos12?.Dispose();
                _sos13?.Dispose();
                _sos14?.Dispose();
                _sos16?.Dispose();
                _sos17?.Dispose();
                _process.Dispose();
                _dac?.Dispose();
                throw;
            }

            IsThreadSafe = _sos13 is not null || RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        private static ClrDataProcess CreateForeignProcess(ClrInfo clrInfo, IntPtr clrDataProcess, object dacLock)
        {
            if (clrDataProcess == IntPtr.Zero)
                throw new ArgumentNullException(nameof(clrDataProcess));
            if (dacLock is null)
                throw new ArgumentNullException(nameof(dacLock));

            // The ClrDataProcess wrapper's constructor does QueryInterface + Release(pUnknown), which is
            // reference-count-neutral on the object but consumes the reference identity we pass in. AddRef
            // here so the host keeps its own reference; the wrapper releases exactly this added reference
            // when the process is disposed, leaving the host's reference intact.
            Marshal.AddRef(clrDataProcess);
            try
            {
                // This TargetProperties feeds only pointer-size address translation (ClrDataProcess/SosDac12);
                // its ThinLockLayout is never consumed here, so the conservative Legacy layout is used. The
                // canonical layout is computed once per runtime in the DacServiceProvider constructor.
                TargetProperties target = new(ThinLockLayout.Legacy, clrInfo.DataTarget.DataReader.PointerSize);
                return new ClrDataProcess(dacLock, target, clrDataProcess);
            }
            catch
            {
                // The wrapper's QueryInterface failed (or another failure occurred) before it took ownership
                // of our added reference; release it so the host pointer's refcount is left unchanged.
                Marshal.Release(clrDataProcess);
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            // Serialize teardown against in-flight DAC reads: every DAC entry point takes
            // _sos.SyncRoot (== _dac.SyncRoot), so disposing the native/COM wrappers under
            // that same lock guarantees no reader is mid-call when we free them.
            lock (_sos.SyncRoot)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _sos13?.LockedFlush();
                // Dispose every COM wrapper (releasing its interface into the DAC module) BEFORE freeing the
                // module via _dac. The wrappers no longer hold a reference to the module's RefCountedFreeLibrary,
                // so the module stays loaded only because _dac owns the single reference; releasing a wrapper
                // after FreeLibrary would call into unloaded code. _moduleHelper holds MetadataImport/ClrDataModule
                // wrappers, so it must be disposed here too, before _dac.
                _process.Dispose();
                _sos.Dispose();
                _sos6?.Dispose();
                _sos8?.Dispose();
                _sos12?.Dispose();
                _sos13?.Dispose();
                _sos14?.Dispose();
                _sos16?.Dispose();
                _sos17?.Dispose();
                _moduleHelper?.Dispose();
                _dac?.Dispose();
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
                    return _runtime ??= new DacRuntime(_clrInfo, _process, _sos, _sos13, _target);
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
                        return _heapHelper = new DacHeap(_sos, _sos8, _sos12, _sos16, _dataReader, _target, data, mts);

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
                    _moduleHelper ??= new(_sos, _target);
                    return _typeHelper ??= new DacTypeHelpers(_process, _sos, _sos6, _sos8, _sos14, _dataReader, _moduleHelper, _target, _clrInfo.Flavor, _clrInfo.Version?.Major ?? 0);
                }
            }

            if (serviceType == typeof(IAbstractClrNativeHeaps))
            {
                IAbstractClrNativeHeaps? n = _nativeHeaps;
                if (n is not null)
                    return n;
                lock (_serviceLock)
                    return _nativeHeaps ??= new DacNativeHeaps(_clrInfo, _sos, _sos13, _dataReader, _target);
            }

            if (serviceType == typeof(IAbstractModuleHelpers))
            {
                DacModuleHelpers? m = _moduleHelper;
                if (m is not null)
                    return m;
                lock (_serviceLock)
                    return _moduleHelper ??= new DacModuleHelpers(_sos, _target);
            }

            if (serviceType == typeof(IAbstractComHelpers))
            {
                IAbstractComHelpers? c = _com;
                if (c is not null)
                    return c;
                lock (_serviceLock)
                    return _com ??= new DacComHelpers(_sos, _target);
            }

            if (serviceType == typeof(IAbstractLegacyThreadPool))
            {
                IAbstractLegacyThreadPool? p = _threadPool;
                if (p is not null)
                    return p;
                lock (_serviceLock)
                    return _threadPool ??= new DacLegacyThreadPool(_sos, _target);
            }

            if (serviceType == typeof(IAbstractMethodLocator))
            {
                IAbstractMethodLocator? l = _methodLocator;
                if (l is not null)
                    return l;
                lock (_serviceLock)
                    return _methodLocator ??= new DacMethodLocator(_sos, _dataReader, _target);
            }

            if (serviceType == typeof(IAbstractThreadHelpers))
            {
                IAbstractThreadHelpers? th = _threadHelper;
                if (th is not null)
                    return th;
                lock (_serviceLock)
                    return _threadHelper ??= new DacThreadHelpers(_process, _sos, _dataReader, _target);
            }

            if (serviceType == typeof(IAbstractStressLog))
            {
                if (_sos17 is null)
                    return null;

                IAbstractStressLog? s = _stressLog;
                if (s is not null)
                    return s;
                lock (_serviceLock)
                    return _stressLog ??= new DacStressLog(_sos, _sos17, _target);
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
                else
                {
                    _process.Flush();
                }
            }
        }
    }
}