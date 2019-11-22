// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime
{
    internal abstract class RuntimeBase : ClrRuntime
    {
        private const int c_maxStackDepth = 1024 * 1024 * 1024; // 1gb

        protected ClrDataProcess _dacInterface;
        private MemoryReader _cache;
        protected IDataReader _dataReader;
        protected DataTarget _dataTarget;

        public RuntimeBase(ClrInfo info, DataTarget dataTarget, DacLibrary lib)
        {
            Debug.Assert(lib != null);
            Debug.Assert(lib.InternalDacPrivateInterface != null);

            ClrInfo = info;
            _dataTarget = dataTarget;
            DacLibrary = lib;
            _dacInterface = DacLibrary.InternalDacPrivateInterface;
            InitApi();

            _dacInterface.Flush();

            IGCInfo data = GetGCInfo();
            if (data != null)
            {
                ServerGC = data.ServerMode;
                HeapCount = data.HeapCount;
                CanWalkHeap = data.GCStructuresValid;
            }

            _dataReader = dataTarget.DataReader;
        }

        public override DataTarget DataTarget => _dataTarget;
        
        public IDataReader DataReader => _dataReader;

        protected abstract void InitApi();

        public override int PointerSize => IntPtr.Size;

        protected internal bool CanWalkHeap { get; protected set; }

        internal MemoryReader MemoryReader
        {
            get
            {
                if (_cache == null)
                    _cache = new MemoryReader(DataReader, 0x200);
                return _cache;
            }
            set => _cache = value;
        }

        internal bool GetHeaps(out SubHeap[] heaps)
        {
            heaps = new SubHeap[HeapCount];
            Dictionary<ulong, ulong> allocContexts = GetAllocContexts();
            if (ServerGC)
            {
                ulong[] heapList = GetServerHeapList();
                if (heapList == null)
                    return false;

                bool succeeded = false;
                for (int i = 0; i < heapList.Length; ++i)
                {
                    IHeapDetails heap = GetSvrHeapDetails(heapList[i]);
                    if (heap == null)
                        continue;

                    heaps[i] = new SubHeap(heap, i, allocContexts);
                    if (heap.EphemeralAllocContextPtr != 0)
                        heaps[i].AllocPointers[heap.EphemeralAllocContextPtr] = heap.EphemeralAllocContextLimit;

                    succeeded = true;
                }

                return succeeded;
            }

            {
                IHeapDetails heap = GetWksHeapDetails();
                if (heap == null)
                    return false;

                heaps[0] = new SubHeap(heap, 0, allocContexts);
                heaps[0].AllocPointers[heap.EphemeralAllocContextPtr] = heap.EphemeralAllocContextLimit;

                return true;
            }
        }

        internal Dictionary<ulong, ulong> GetAllocContexts()
        {
            Dictionary<ulong, ulong> ret = new Dictionary<ulong, ulong>();

            // Give a max number of threads to walk to ensure no infinite loops due to data
            // inconsistency.
            int max = 1024;

            IThreadData thread = GetThread(GetFirstThread());

            while (max-- > 0 && thread != null)
            {
                if (thread.AllocPtr != 0)
                    ret[thread.AllocPtr] = thread.AllocLimit;

                if (thread.Next == 0)
                    break;

                thread = GetThread(thread.Next);
            }

            return ret;
        }

        private struct StackRef
        {
            public ulong Address;
            public ulong Object;

            public StackRef(ulong stackPtr, ulong objRef)
            {
                Address = stackPtr;
                Object = objRef;
            }
        }

        public override IEnumerable<ulong> EnumerateFinalizerQueueObjectAddresses()
        {
            if (GetHeaps(out SubHeap[] heaps))
            {
                foreach (SubHeap heap in heaps)
                {
                    foreach (ulong objAddr in GetPointersInRange(heap.FQRootsStart, heap.FQRootsStop))
                    {
                        if (objAddr != 0)
                            yield return objAddr;
                    }
                }
            }
        }

        internal virtual IEnumerable<ClrRoot> EnumerateStackReferences(ClrThread thread, bool includeDead)
        {
            ulong stackBase = thread.StackBase;
            ulong stackLimit = thread.StackLimit;
            if (stackLimit <= stackBase)
            {
                ulong tmp = stackLimit;
                stackLimit = stackBase;
                stackBase = tmp;
            }

            // Ensure sensible limits on stack walking.  If there's more than 1gb of stack space then either the target
            // process is a really strange program, or we've somehow read the wrong base/limit.
            if (stackBase == 0 || stackLimit - stackBase > c_maxStackDepth)
                yield break;
            
            ClrAppDomain domain = GetAppDomainByAddress(thread.AppDomain);
            ClrHeap heap = Heap;
            MemoryReader cache = MemoryReader;
            cache.EnsureRangeInCache(stackBase);
            for (ulong stackPtr = stackBase; stackPtr < stackLimit; stackPtr += (uint)PointerSize)
            {
                if (cache.ReadPtr(stackPtr, out ulong objRef))
                {
                    // If the value isn't pointer aligned, it cannot be a managed pointer.
                    if (heap.IsInHeap(objRef))
                    {
                        if (heap.ReadPointer(objRef, out ulong mt))
                        {
                            ClrType type = null;

                            if (mt > 1024)
                                type = heap.GetObjectType(objRef);

                            if (type != null && !type.IsFree)
                                yield return new LocalVarRoot(stackPtr, objRef, type, domain, thread, false, true, false, null);
                        }
                    }
                }
            }
        }

        internal abstract ulong GetFirstThread();
        internal abstract IThreadData GetThread(ulong addr);
        internal abstract IHeapDetails GetSvrHeapDetails(ulong addr);
        internal abstract IHeapDetails GetWksHeapDetails();
        internal abstract ulong[] GetServerHeapList();
        internal abstract IThreadStoreData GetThreadStoreData();
        internal abstract ISegmentData GetSegmentData(ulong addr);
        internal abstract IGCInfo GetGCInfo();
        internal abstract IMethodTableData GetMethodTableData(ulong addr);
        internal abstract IMethodTableCollectibleData GetMethodTableCollectibleData(ulong addr);
        internal abstract uint GetTlsSlot();
        internal abstract uint GetThreadTypeIndex();

        internal abstract ClrAppDomain GetAppDomainByAddress(ulong addr);

        public bool ReadMemory(ulong address, Span<byte> buffer, out int bytesRead) => _dataReader.ReadMemory(address, buffer, out bytesRead);


        public unsafe bool ReadPrimitive<T>(ulong addr, out T value) where T: unmanaged
        {
            Span<byte> buffer = stackalloc byte[sizeof(T)];
            if (ReadMemory(addr, buffer, out int read) && read == buffer.Length)
            {
                value = MemoryMarshal.Read<T>(buffer);

                return true;
            }

            value = default;
            return false;
        }

        public override bool ReadPointer(ulong addr, out ulong value)
        {
            Span<byte> buffer = stackalloc byte[IntPtr.Size];
            if (ReadMemory(addr, buffer, out int read) && read == buffer.Length)
            {
                value = buffer.AsPointer();
                return true;
            }

            value = 0;
            return false;
        }

        public bool ReadString(ulong addr, out string value)
        {
            value = ((DesktopGCHeap)Heap).GetStringContents(addr);
            return value != null;
        }


        internal IEnumerable<ulong> GetPointersInRange(ulong start, ulong stop)
        {
            // Possible we have empty list, or inconsistent data.
            if (start >= stop)
                yield break;

            // TODO: rewrite
            for (ulong ptr = start; ptr < stop; ptr += (uint)IntPtr.Size)
            {
                if (!ReadPointer(ptr, out ulong obj))
                    break;

                yield return obj;
            }
        }
    }
}