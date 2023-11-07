// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    internal class DacServiceProvider : IServiceProvider, IDisposable
    {
        private readonly ClrInfo _clrInfo;
        private readonly DacLibrary _library;

        private readonly ClrDataProcess _dac;
        private readonly SOSDac _sos;
        private readonly SOSDac6? _sos6;
        private readonly SOSDac8? _sos8;
        private readonly SosDac12? _sos12;
        private readonly ISOSDac13? _sos13;

        private IAbstractClrNativeHeaps? _nativeHeaps;
        private IAbstractComHelpers? _com;
        private IAbstractHeapProvider? _heap;
        private IAbstractLegacyThreadPool? _threadPool;
        private IAbstractModuleProvider? _modules;
        private IAbstractRuntime? _runtime;
        private IAbstractTypeProvider? _types;

        private IDataReader DataReader => _clrInfo.DataTarget.DataReader;

        public DacServiceProvider(ClrInfo clrInfo, DacLibrary library)
        {
            _clrInfo = clrInfo;
            _library = library;

            _library = library;

            _dac = library.DacPrivateInterface;
            _sos = library.SOSDacInterface;
            _sos6 = library.SOSDacInterface6;
            _sos8 = library.SOSDacInterface8;
            _sos12 = library.SOSDacInterface12;
            _sos13 = library.SOSDacInterface13;
        }

        public void Dispose()
        {
            _runtime?.Flush();
            _dac.Dispose();
            _sos.Dispose();
            _sos6?.Dispose();
            _sos8?.Dispose();
            _sos12?.Dispose();
            _sos13?.Dispose();
            _library.Dispose();
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IAbstractRuntime))
                return _runtime ??= new DacRuntime(_clrInfo, _library);

            if (serviceType == typeof(IAbstractHeapProvider))
            {
                IAbstractHeapProvider? heap = _heap;
                if (heap is not null)
                    return heap;

                if (_sos.GetGCHeapData(out GCInfo data) && _sos.GetCommonMethodTables(out CommonMethodTables mts) && mts.ObjectMethodTable != 0)
                    return _heap = new DacHeap(_sos, _sos8, _sos12, DataReader, data, mts);

                return null;
            }

            if (serviceType == typeof(IAbstractTypeProvider))
                return _types ??= new DacTypeHelpers(_dac, _sos, _sos6, _sos8, DataReader);

            if (serviceType == typeof(IAbstractClrNativeHeaps))
                return _nativeHeaps ??= new DacNativeHeaps(_clrInfo, _sos, _sos13, DataReader);

            if (serviceType == typeof(IAbstractModuleProvider))
                return _modules ??= new DacModuleHelpers(_sos);

            if (serviceType == typeof(IAbstractComHelpers))
                return _com ??= new DacComHelpers(_sos);

            if (serviceType == typeof(IAbstractLegacyThreadPool))
                return _threadPool ??= new DacLegacyThreadPool(_sos);

            return null;
        }
    }
}