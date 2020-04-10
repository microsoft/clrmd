// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DbgEng
{
    internal unsafe sealed class DebugSystemObjects : CallableCOMWrapper
    {
        internal static readonly Guid IID_DebugSystemObjects3 = new Guid("e9676e2f-e286-4ea3-b0f9-dfe5d9fc330e");

        public DebugSystemObjects(RefCountedFreeLibrary library, IntPtr pUnk)
            : base(library, IID_DebugSystemObjects3, pUnk)
        {
            SuppressRelease();
        }

        private ref readonly IDebugSystemObjects3VTable VTable => ref Unsafe.AsRef<IDebugSystemObjects3VTable>(_vtable);

#pragma warning disable CA1822
        public IDisposable Enter() => new SystemHolder();
#pragma warning restore CA1822

        public uint GetProcessId()
        {
            InitDelegate(ref _getProcessId, VTable.GetCurrentProcessSystemId);

            using IDisposable holder = Enter();
            HResult hr = _getProcessId(Self, out uint id);
            return hr ? id : 0;
        }

        private HResult SetCurrentSystemId(int id)
        {
            InitDelegate(ref _setSystemId, VTable.SetCurrentSystemId);

            HResult hr = _setSystemId(Self, id);
            DebugOnly.Assert(hr);
            return hr;
        }

        public HResult SetCurrentThread(uint id)
        {
            InitDelegate(ref _setCurrentThread, VTable.SetCurrentThreadId);

            using IDisposable holder = Enter();
            HResult hr = _setCurrentThread(Self, id);
            DebugOnly.Assert(hr);
            return hr;
        }

        public int GetNumberThreads()
        {
            InitDelegate(ref _getNumberThreads, VTable.GetNumberThreads);

            using IDisposable holder = Enter();
            HResult hr = _getNumberThreads(Self, out int count);
            DebugOnly.Assert(hr);
            return count;
        }

        internal void Init()
        {
            InitDelegate(ref _getSystemId, VTable.GetCurrentSystemId);
            HResult hr = _getSystemId(Self, out int id);
            DebugOnly.Assert(hr);
            _systemId = id;
        }

        public uint[] GetThreadIds()
        {
            using IDisposable holder = Enter();

            int count = GetNumberThreads();
            if (count == 0)
                return Array.Empty<uint>();

            InitDelegate(ref _getThreadIdsByIndex, VTable.GetThreadIdsByIndex);

            uint[] result = new uint[count];
            fixed (uint* pResult = result)
            {
                if (_getThreadIdsByIndex(Self, 0, count, null, pResult))
                    return result;

                return Array.Empty<uint>();
            }
        }

        public uint GetThreadIdBySystemId(uint sysId)
        {
            InitDelegate(ref _getThreadIdBySystemId, VTable.GetThreadIdBySystemId);

            using IDisposable holder = Enter();
            HResult hr = _getThreadIdBySystemId(Self, sysId, out uint result);
            DebugOnly.Assert(hr);
            return result;
        }

        private int _systemId = -1;

        private GetCurrentProcessSystemIdDelegate? _getProcessId;
        private GetCurrentSystemIdDelegate? _getSystemId;
        private SetCurrentSystemIdDelegate? _setSystemId;
        private SetCurrentThreadIdDelegate? _setCurrentThread;
        private GetNumberThreadsDelegate? _getNumberThreads;
        private GetThreadIdsByIndexDelegate? _getThreadIdsByIndex;
        private GetThreadIdBySystemIdDelegate? _getThreadIdBySystemId;

        private delegate HResult GetCurrentProcessSystemIdDelegate(IntPtr self, out uint pid);
        private delegate HResult GetCurrentSystemIdDelegate(IntPtr self, out int id);
        private delegate HResult SetCurrentSystemIdDelegate(IntPtr self, int id);
        private delegate HResult SetCurrentThreadIdDelegate(IntPtr self, uint id);
        private delegate HResult GetNumberThreadsDelegate(IntPtr self, out int count);
        private delegate HResult GetThreadIdsByIndexDelegate(IntPtr self, int start, int count, int* ids, uint* systemIds);
        private delegate HResult GetThreadIdBySystemIdDelegate(IntPtr self, uint sysId, out uint id);

        private class SystemHolder : IDisposable
        {
            private static readonly object _sync = new object();

            public SystemHolder()
            {
                Monitor.Enter(_sync);
            }

            public void Dispose()
            {
                Monitor.Exit(_sync);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct IDebugSystemObjects3VTable
    {
        public readonly IntPtr GetEventThread;
        public readonly IntPtr GetEventProcess;
        public readonly IntPtr GetCurrentThreadId;
        public readonly IntPtr SetCurrentThreadId;
        public readonly IntPtr GetCurrentProcessId;
        public readonly IntPtr SetCurrentProcessId;
        public readonly IntPtr GetNumberThreads;
        public readonly IntPtr GetTotalNumberThreads;
        public readonly IntPtr GetThreadIdsByIndex;
        public readonly IntPtr GetThreadIdByProcessor;
        public readonly IntPtr GetCurrentThreadDataOffset;
        public readonly IntPtr GetThreadIdByDataOffset;
        public readonly IntPtr GetCurrentThreadTeb;
        public readonly IntPtr GetThreadIdByTeb;
        public readonly IntPtr GetCurrentThreadSystemId;
        public readonly IntPtr GetThreadIdBySystemId;
        public readonly IntPtr GetCurrentThreadHandle;
        public readonly IntPtr GetThreadIdByHandle;
        public readonly IntPtr GetNumberProcesses;
        public readonly IntPtr GetProcessIdsByIndex;
        public readonly IntPtr GetCurrentProcessDataOffset;
        public readonly IntPtr GetProcessIdByDataOffset;
        public readonly IntPtr GetCurrentProcessPeb;
        public readonly IntPtr GetProcessIdByPeb;
        public readonly IntPtr GetCurrentProcessSystemId;
        public readonly IntPtr GetProcessIdBySystemId;
        public readonly IntPtr GetCurrentProcessHandle;
        public readonly IntPtr GetProcessIdByHandle;
        public readonly IntPtr GetCurrentProcessExecutableName;
        public readonly IntPtr GetCurrentProcessUpTime;
        public readonly IntPtr GetImplicitThreadDataOffset;
        public readonly IntPtr SetImplicitThreadDataOffset;
        public readonly IntPtr GetImplicitProcessDataOffset;
        public readonly IntPtr SetImplicitProcessDataOffset;
        public readonly IntPtr GetEventSystem;
        public readonly IntPtr GetCurrentSystemId;
        public readonly IntPtr SetCurrentSystemId;
        public readonly IntPtr GetNumberSystems;
        public readonly IntPtr GetSystemIdsByIndex;
        public readonly IntPtr GetTotalNumberThreadsAndProcesses;
        public readonly IntPtr GetCurrentSystemServer;
        public readonly IntPtr GetSystemByServer;
        public readonly IntPtr GetCurrentSystemServerName;
    }
}