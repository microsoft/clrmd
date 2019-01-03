// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Desktop;
using Microsoft.Diagnostics.Runtime.ICorDebug;

namespace Microsoft.Diagnostics.Runtime
{
    internal abstract class RuntimeBase : ClrRuntime
    {
        private const int c_maxStackDepth = 1024 * 1024 * 1024; // 1gb

        private static readonly ulong[] s_emptyPointerArray = new ulong[0];
        protected ClrDataProcess _dacInterface;
        private MemoryReader _cache;
        protected IDataReader _dataReader;
        protected DataTarget _dataTarget;

        protected ICorDebugProcess _corDebugProcess;
        internal ICorDebugProcess CorDebugProcess
        {
            get
            {
                if (_corDebugProcess == null)
                    _corDebugProcess = CLRDebugging.CreateICorDebugProcess(ClrInfo.ModuleInfo.ImageBase, DacLibrary.DacDataTarget, _dataTarget.FileLoader);

                return _corDebugProcess;
            }
        }

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
            ulong mask = (ulong)(PointerSize - 1);
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
        internal abstract uint GetTlsSlot();
        internal abstract uint GetThreadTypeIndex();

        internal abstract ClrAppDomain GetAppDomainByAddress(ulong addr);

        protected bool Request(uint id, ulong param, byte[] output)
        {
            byte[] input = BitConverter.GetBytes(param);

            return Request(id, input, output);
        }

        protected bool Request(uint id, uint param, byte[] output)
        {
            byte[] input = BitConverter.GetBytes(param);

            return Request(id, input, output);
        }

        protected bool Request(uint id, byte[] input, byte[] output)
        {
            uint inSize = 0;
            if (input != null)
                inSize = (uint)input.Length;

            uint outSize = 0;
            if (output != null)
                outSize = (uint)output.Length;

            int result = _dacInterface.Request(id, inSize, input, outSize, output);

            return result >= 0;
        }

        protected I Request<I, T>(uint id, byte[] input)
            where T : struct, I
            where I : class
        {
            byte[] output = GetByteArrayForStruct<T>();

            if (!Request(id, input, output))
                return null;

            return ConvertStruct<I, T>(output);
        }

        protected I Request<I, T>(uint id, ulong param)
            where T : struct, I
            where I : class
        {
            byte[] output = GetByteArrayForStruct<T>();

            if (!Request(id, param, output))
                return null;

            return ConvertStruct<I, T>(output);
        }

        protected I Request<I, T>(uint id, uint param)
            where T : struct, I
            where I : class
        {
            byte[] output = GetByteArrayForStruct<T>();

            if (!Request(id, param, output))
                return null;

            return ConvertStruct<I, T>(output);
        }

        protected I Request<I, T>(uint id)
            where T : struct, I
            where I : class
        {
            byte[] output = GetByteArrayForStruct<T>();

            if (!Request(id, null, output))
                return null;

            return ConvertStruct<I, T>(output);
        }

        protected bool RequestStruct<T>(uint id, ref T t)
            where T : struct
        {
            byte[] output = GetByteArrayForStruct<T>();

            if (!Request(id, null, output))
                return false;

            GCHandle handle = GCHandle.Alloc(output, GCHandleType.Pinned);
            t = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return true;
        }

        protected bool RequestStruct<T>(uint id, ulong addr, ref T t)
            where T : struct
        {
            byte[] input = new byte[sizeof(ulong)];
            byte[] output = GetByteArrayForStruct<T>();

            WriteValueToBuffer(addr, input, 0);

            if (!Request(id, input, output))
                return false;

            GCHandle handle = GCHandle.Alloc(output, GCHandleType.Pinned);
            t = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return true;
        }

        protected ulong[] RequestAddrList(uint id, int length)
        {
            byte[] bytes = new byte[length * sizeof(ulong)];
            if (!Request(id, null, bytes))
                return null;

            ulong[] result = new ulong[length];
            for (uint i = 0; i < length; ++i)
                result[i] = BitConverter.ToUInt64(bytes, (int)(i * sizeof(ulong)));

            return result;
        }

        protected ulong[] RequestAddrList(uint id, ulong param, int length)
        {
            byte[] bytes = new byte[length * sizeof(ulong)];
            if (!Request(id, param, bytes))
                return null;

            ulong[] result = new ulong[length];
            for (uint i = 0; i < length; ++i)
                result[i] = BitConverter.ToUInt64(bytes, (int)(i * sizeof(ulong)));

            return result;
        }

        protected static string BytesToString(byte[] output)
        {
            int len = 0;
            while (len < output.Length && (output[len] != 0 || output[len + 1] != 0))
                len += 2;

            if (len > output.Length)
                len = output.Length;

            return Encoding.Unicode.GetString(output, 0, len);
        }

        protected byte[] GetByteArrayForStruct<T>()
            where T : struct
        {
            return new byte[Marshal.SizeOf(typeof(T))];
        }

        protected I ConvertStruct<I, T>(byte[] bytes)
            where I : class
            where T : I
        {
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            I result = (I)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return result;
        }

        protected int WriteValueToBuffer(IntPtr ptr, byte[] buffer, int offset)
        {
            ulong value = (ulong)ptr.ToInt64();
            for (int i = offset; i < offset + IntPtr.Size; ++i)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }

            return offset + IntPtr.Size;
        }

        protected int WriteValueToBuffer(int value, byte[] buffer, int offset)
        {
            for (int i = offset; i < offset + sizeof(int); ++i)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }

            return offset + sizeof(int);
        }

        protected int WriteValueToBuffer(uint value, byte[] buffer, int offset)
        {
            for (int i = offset; i < offset + sizeof(int); ++i)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }

            return offset + sizeof(int);
        }

        protected int WriteValueToBuffer(ulong value, byte[] buffer, int offset)
        {
            for (int i = offset; i < offset + sizeof(ulong); ++i)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }

            return offset + sizeof(ulong);
        }

        public override bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            return _dataReader.ReadMemory(address, buffer, bytesRequested, out bytesRead);
        }

        private readonly byte[] _dataBuffer = new byte[8];

        public bool ReadByte(ulong addr, out byte value)
        {
            // todo: There's probably a more efficient way to implement this if ReadVirtual accepted an "out byte"
            //       "out dword", "out long", etc.
            value = 0;
            if (!ReadMemory(addr, _dataBuffer, 1, out int read))
                return false;

            Debug.Assert(read == 1);

            value = _dataBuffer[0];
            return true;
        }

        public bool ReadByte(ulong addr, out sbyte value)
        {
            value = 0;
            if (!ReadMemory(addr, _dataBuffer, 1, out int read))
                return false;

            Debug.Assert(read == 1);

            value = (sbyte)_dataBuffer[0];
            return true;
        }

        public bool ReadDword(ulong addr, out int value)
        {
            value = 0;
            if (!ReadMemory(addr, _dataBuffer, sizeof(int), out int read))
                return false;

            Debug.Assert(read == 4);

            value = BitConverter.ToInt32(_dataBuffer, 0);
            return true;
        }

        public bool ReadDword(ulong addr, out uint value)
        {
            value = 0;
            if (!ReadMemory(addr, _dataBuffer, sizeof(uint), out int read))
                return false;

            Debug.Assert(read == 4);

            value = BitConverter.ToUInt32(_dataBuffer, 0);
            return true;
        }

        public bool ReadFloat(ulong addr, out float value)
        {
            value = 0;
            if (!ReadMemory(addr, _dataBuffer, sizeof(float), out int read))
                return false;

            Debug.Assert(read == sizeof(float));

            value = BitConverter.ToSingle(_dataBuffer, 0);
            return true;
        }

        public bool ReadFloat(ulong addr, out double value)
        {
            value = 0;
            if (!ReadMemory(addr, _dataBuffer, sizeof(double), out int read))
                return false;

            Debug.Assert(read == sizeof(double));

            value = BitConverter.ToDouble(_dataBuffer, 0);
            return true;
        }

        public bool ReadString(ulong addr, out string value)
        {
            value = ((DesktopGCHeap)Heap).GetStringContents(addr);
            return value != null;
        }

        public bool ReadShort(ulong addr, out short value)
        {
            value = 0;
            if (!ReadMemory(addr, _dataBuffer, sizeof(short), out int read))
                return false;

            Debug.Assert(read == sizeof(short));

            value = BitConverter.ToInt16(_dataBuffer, 0);
            return true;
        }

        public bool ReadShort(ulong addr, out ushort value)
        {
            value = 0;
            if (!ReadMemory(addr, _dataBuffer, sizeof(ushort), out int read))
                return false;

            Debug.Assert(read == sizeof(ushort));

            value = BitConverter.ToUInt16(_dataBuffer, 0);
            return true;
        }

        public bool ReadQword(ulong addr, out ulong value)
        {
            value = 0;
            if (!ReadMemory(addr, _dataBuffer, sizeof(ulong), out int read))
                return false;

            Debug.Assert(read == sizeof(ulong));

            value = BitConverter.ToUInt64(_dataBuffer, 0);
            return true;
        }

        public bool ReadQword(ulong addr, out long value)
        {
            value = 0;
            if (!ReadMemory(addr, _dataBuffer, sizeof(long), out int read))
                return false;

            Debug.Assert(read == sizeof(long));

            value = BitConverter.ToInt64(_dataBuffer, 0);
            return true;
        }

        public override bool ReadPointer(ulong addr, out ulong value)
        {
            int ptrSize = PointerSize;
            if (!ReadMemory(addr, _dataBuffer, ptrSize, out int read))
            {
                value = 0xcccccccc;
                return false;
            }

            Debug.Assert(read == ptrSize);

            if (ptrSize == 4)
                value = BitConverter.ToUInt32(_dataBuffer, 0);
            else
                value = BitConverter.ToUInt64(_dataBuffer, 0);

            return true;
        }

        internal IEnumerable<ulong> GetPointersInRange(ulong start, ulong stop)
        {
            // Possible we have empty list, or inconsistent data.
            if (start >= stop)
                return s_emptyPointerArray;

            // Enumerate individually if we have too many.
            ulong count = (stop - start) / (ulong)IntPtr.Size;
            if (count > 4096)
                return EnumeratePointersInRange(start, stop);

            ulong[] array = new ulong[count];
            byte[] tmp = new byte[(int)count * IntPtr.Size];
            if (!ReadMemory(start, tmp, tmp.Length, out int read))
                return s_emptyPointerArray;

            if (IntPtr.Size == 4)
                for (uint i = 0; i < array.Length; ++i)
                    array[i] = BitConverter.ToUInt32(tmp, (int)(i * IntPtr.Size));
            else
                for (uint i = 0; i < array.Length; ++i)
                    array[i] = BitConverter.ToUInt64(tmp, (int)(i * IntPtr.Size));

            return array;
        }

        private IEnumerable<ulong> EnumeratePointersInRange(ulong start, ulong stop)
        {
            for (ulong ptr = start; ptr < stop; ptr += (uint)IntPtr.Size)
            {
                if (!ReadPointer(ptr, out ulong obj))
                    break;

                yield return obj;
            }
        }
    }
}