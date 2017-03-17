// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Native
{
    internal class NativeRuntime : RuntimeBase
    {
        private ISOSNative _sos;
        private Lazy<NativeHeap> _heap;
        private ClrThread[] _threads;
        private NativeModule[] _modules;
        private NativeAppDomain _domain;
        private int _dacRawVersion;

        private ISOSNativeSerializedExceptionSupport _sosNativeSerializedExceptionSupport;

        public NativeRuntime(ClrInfo info, DataTargetImpl dt, DacLibrary lib)
            : base(info, dt, lib)
        {
            byte[] tmp = new byte[sizeof(int)];

            if (!Request(DacRequests.VERSION, null, tmp))
                throw new ClrDiagnosticsException("Failed to request dac version.", ClrDiagnosticsException.HR.DacError);

            _dacRawVersion = BitConverter.ToInt32(tmp, 0);
            if (_dacRawVersion != 10 && _dacRawVersion != 11)
                throw new ClrDiagnosticsException("Unsupported dac version.", ClrDiagnosticsException.HR.DacError);

            _heap = new Lazy<NativeHeap>(() => new NativeHeap(this, NativeModules));
        }

        public override ClrMethod GetMethodByHandle(ulong methodHandle)
        {
            return null;
        }

        protected override void InitApi()
        {
            if (_sos == null)
            {
                var dac = _library.DacInterface;
                if (!(dac is ISOSNative))
                    throw new ClrDiagnosticsException("This version of mrt100 is too old.", ClrDiagnosticsException.HR.DataRequestError);

                _sos = (ISOSNative)dac;
            }

            _sosNativeSerializedExceptionSupport = _library.DacInterface as ISOSNativeSerializedExceptionSupport;
        }

        public override ClrHeap Heap => _heap.Value;

        [Obsolete]
        public override ClrHeap GetHeap() => _heap.Value;

        public override int PointerSize
        {
            get { return IntPtr.Size; }
        }

        public override IList<ClrAppDomain> AppDomains
        {
            get
            {
                return new ClrAppDomain[] { GetRhAppDomain() };
            }
        }

        public override ClrAppDomain SharedDomain
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override ClrAppDomain SystemDomain
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override IList<ClrThread> Threads
        {
            get
            {
                if (_threads == null)
                    InitThreads();

                return _threads;
            }
        }

        public override IEnumerable<ClrHandle> EnumerateHandles()
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<ClrMemoryRegion> EnumerateMemoryRegions()
        {
            throw new NotImplementedException();
        }

        public override ClrMethod GetMethodByAddress(ulong ip)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            OnRuntimeFlushed();
            throw new NotImplementedException();
        }
        
        [Obsolete]
        override public ClrThreadPool GetThreadPool() { throw new NotImplementedException(); }

        internal ClrAppDomain GetRhAppDomain()
        {
            if (_domain == null)
                _domain = new NativeAppDomain(this, NativeModules);

            return _domain;
        }

        internal NativeModule[] NativeModules
        {
            get
            {
                if (_modules != null)
                    return _modules;

                List<ModuleInfo> modules = new List<ModuleInfo>(DataTarget.EnumerateModules());
                modules.Sort((x, y) => x.ImageBase.CompareTo(y.ImageBase));

                if (_sos.GetModuleList(0, null, out int count) < 0)
                {
                    _modules = ConvertModuleList(modules);
                    return _modules;
                }

                ulong[] ptrs = new ulong[count];
                if (_sos.GetModuleList(count, ptrs, out count) < 0)
                {
                    _modules = ConvertModuleList(modules);
                    return _modules;
                }

                Array.Sort(ptrs);

                int i = 0, j = 0;
                while (i < modules.Count && j < ptrs.Length)
                {
                    ModuleInfo info = modules[i];
                    ulong addr = ptrs[j];
                    if (info.ImageBase <= addr && addr < info.ImageBase + info.FileSize)
                    {
                        i++;
                        j++;
                    }
                    else if (addr < info.ImageBase)
                    {
                        j++;
                    }
                    else if (addr >= info.ImageBase + info.FileSize)
                    {
                        modules.RemoveAt(i);
                    }
                }

                modules.RemoveRange(i, modules.Count - i);
                _modules = ConvertModuleList(modules);
                return _modules;
            }
        }

        private NativeModule[] ConvertModuleList(List<ModuleInfo> modules)
        {
            NativeModule[] result = new NativeModule[modules.Count];

            int i = 0;
            foreach (var module in modules)
                result[i++] = new NativeModule(this, module);

            return result;
        }

        internal unsafe IList<ClrRoot> EnumerateStackRoots(ClrThread thread)
        {
            int contextSize;

            var plat = _dataReader.GetArchitecture();
            if (plat == Architecture.Amd64)
                contextSize = 0x4d0;
            else if (plat == Architecture.X86)
                contextSize = 0x2d0;
            else if (plat == Architecture.Arm)
                contextSize = 0x1a0;
            else
                throw new InvalidOperationException("Unexpected architecture.");

            byte[] context = new byte[contextSize];
            _dataReader.GetThreadContext(thread.OSThreadId, 0, (uint)contextSize, context);

            var walker = new NativeStackRootWalker(_heap.Value, GetRhAppDomain(), thread);
            THREADROOTCALLBACK del = new THREADROOTCALLBACK(walker.Callback);
            IntPtr callback = Marshal.GetFunctionPointerForDelegate(del);

            fixed (byte* b = &context[0])
            {
                IntPtr ctx = new IntPtr(b);
                _sos.TraverseStackRoots(thread.Address, ctx, contextSize, callback, IntPtr.Zero);
            }
            GC.KeepAlive(del);

            return walker.Roots;
        }

        internal IList<ClrRoot> EnumerateStaticRoots(bool resolveStatics)
        {
            var walker = new NativeStaticRootWalker(this, resolveStatics);
            STATICROOTCALLBACK del = new STATICROOTCALLBACK(walker.Callback);
            IntPtr ptr = Marshal.GetFunctionPointerForDelegate(del);
            _sos.TraverseStaticRoots(ptr);
            GC.KeepAlive(del);

            return walker.Roots;
        }

        internal IEnumerable<ClrRoot> EnumerateHandleRoots()
        {
            var walker = new NativeHandleRootWalker(this, _dacRawVersion != 10);
            HANDLECALLBACK callback = new HANDLECALLBACK(walker.RootCallback);
            IntPtr ptr = Marshal.GetFunctionPointerForDelegate(callback);
            _sos.TraverseHandleTable(ptr, IntPtr.Zero);
            GC.KeepAlive(callback);

            return walker.Roots;
        }

        private void InitThreads()
        {
            IThreadStoreData tsData = GetThreadStoreData();
            List<ClrThread> threads = new List<ClrThread>(tsData.Count);

            ulong addr = tsData.FirstThread;
            IThreadData thread = GetThread(tsData.FirstThread);
            for (int i = 0; thread != null; i++)
            {
                threads.Add(new NativeThread(this, thread, addr, tsData.Finalizer == addr));

                addr = thread.Next;
                thread = GetThread(addr);
            }

            _threads = threads.ToArray();
        }

        public override IEnumerable<ClrException> EnumerateSerializedExceptions()
        {
            if (_sosNativeSerializedExceptionSupport == null)
            {
                return new ClrException[0];
            }

            var exceptionById = new Dictionary<ulong, NativeException>();
            ISerializedExceptionEnumerator serializedExceptionEnumerator = _sosNativeSerializedExceptionSupport.GetSerializedExceptions();
            while (serializedExceptionEnumerator.HasNext())
            {
                ISerializedException serializedException = serializedExceptionEnumerator.Next();

                //build the stack frames
                IList<ClrStackFrame> stackFrames = new List<ClrStackFrame>();
                ISerializedStackFrameEnumerator serializedStackFrameEnumerator = serializedException.StackFrames;
                while (serializedStackFrameEnumerator.HasNext())
                {
                    ISerializedStackFrame serializedStackFrame = serializedStackFrameEnumerator.Next();

                    NativeModule nativeModule = _heap.Value.GetModuleFromAddress(serializedStackFrame.IP);
                    string symbolName = null;
                    if (nativeModule != null)
                    {
                        if (nativeModule.Pdb != null)
                        {
                            try
                            {
                                ISymbolResolver resolver = this.DataTarget.SymbolProvider.GetSymbolResolver(nativeModule.Pdb.FileName, nativeModule.Pdb.Guid, nativeModule.Pdb.Revision);

                                if (resolver != null)
                                {
                                    symbolName = resolver.GetSymbolNameByRVA((uint)(serializedStackFrame.IP - nativeModule.ImageBase));
                                }
                                else
                                {
                                    Trace.WriteLine($"Unable to find symbol resolver for PDB [Filename:{nativeModule.Pdb.FileName}, GUID:{nativeModule.Pdb.Guid}, Revision:{nativeModule.Pdb.Revision}]");
                                }
                            }
                            catch (Exception e)
                            {
                                Trace.WriteLine($"Error in finding the symbol resolver for PDB [Filename:{nativeModule.Pdb.FileName}, GUID:{nativeModule.Pdb.Guid}, Revision:{nativeModule.Pdb.Revision}]: {e.Message}");
                                Trace.WriteLine("Check previous traces for additional information");
                            }
                        }
                        else
                        {
                            Trace.WriteLine(String.Format("PDB not found for IP {0}, Module={1}", serializedStackFrame.IP, nativeModule.Name));
                        }
                    }
                    else
                    {
                        Trace.WriteLine(String.Format("Unable to resolve module for IP {0}", serializedStackFrame.IP));
                    }

                    stackFrames.Add(new NativeHeap.NativeStackFrame(serializedStackFrame.IP, symbolName, nativeModule));
                }

                //create a new exception and populate the fields

                exceptionById.Add(serializedException.ExceptionId, new NativeException(
                    _heap.Value.GetTypeByMethodTable(serializedException.ExceptionEEType),
                    serializedException.ExceptionCCWPtr,
                    serializedException.HResult,
                    serializedException.ThreadId,
                    serializedException.ExceptionId,
                    serializedException.InnerExceptionId,
                    serializedException.NestingLevel,
                    stackFrames));
            }

            var usedAsInnerException = new HashSet<ulong>();

            foreach (var nativeException in exceptionById.Values)
            {
                if (nativeException.InnerExceptionId > 0)
                {
                    if (exceptionById.TryGetValue(nativeException.InnerExceptionId, out NativeException innerException))
                    {
                        nativeException.SetInnerException(innerException);
                        usedAsInnerException.Add(innerException.ExceptionId);
                    }
                }
            }

            return exceptionById.Keys.Except(usedAsInnerException).Select(id => exceptionById[id]).Cast<ClrException>().ToList();
        }

        #region Native Implementation

        internal override ClrAppDomain GetAppDomainByAddress(ulong addr)
        {
            return _domain;
        }

        internal override ulong GetFirstThread()
        {
            IThreadStoreData tsData = GetThreadStoreData();
            if (tsData == null)
                return 0;
            return tsData.FirstThread;
        }

        internal override IThreadData GetThread(ulong addr)
        {
            if (addr == 0)
                return null;

            if (_sos.GetThreadData(addr, out NativeThreadData data) < 0)
                return null;

            return data;
        }

        internal override IHeapDetails GetSvrHeapDetails(ulong addr)
        {
            if (_sos.GetGCHeapDetails(addr, out NativeHeapDetails data) < 0)
                return null;

            return data;
        }

        internal override IHeapDetails GetWksHeapDetails()
        {
            if (_sos.GetGCHeapStaticData(out NativeHeapDetails data) < 0)
                return null;

            return data;
        }

        internal override ulong[] GetServerHeapList()
        {
            if (_sos.GetGCHeapList(0, null, out int count) < 0)
                return null;

            ulong[] items = new ulong[count];
            if (_sos.GetGCHeapList(items.Length, items, out count) < 0)
                return null;

            return items;
        }

        internal override IThreadStoreData GetThreadStoreData()
        {
            if (_sos.GetThreadStoreData(out NativeThreadStoreData data) < 0)
                return null;

            return data;
        }

        internal override ISegmentData GetSegmentData(ulong addr)
        {
            if (addr == 0)
                return null;

            if (_sos.GetGCHeapSegment(addr, out NativeSegementData data) < 0)
                return null;

            return data;
        }

        internal override IMethodTableData GetMethodTableData(ulong eetype)
        {
            if (_sos.GetEETypeData(eetype, out NativeMethodTableData data) < 0)
                return null;

            return data;
        }

        internal ulong GetFreeType()
        {
            // Can't return 0 on error here, as that would make values of 0 look like a
            // valid method table.  Instead, return something that won't likely be the value
            // in the methodtable.
            if (_sos.GetFreeEEType(out ulong free) < 0)
                return ulong.MaxValue - 42;

            return free;
        }

        internal override IGCInfo GetGCInfo()
        {
            if (_sos.GetGCHeapData(out LegacyGCInfo info) < 0)
                return null;

            return info;
        }
        #endregion

        internal override uint GetTlsSlot()
        {
            throw new NotImplementedException();
        }

        internal override uint GetThreadTypeIndex()
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<int> EnumerateGCThreads()
        {
            throw new NotImplementedException();
        }

        public override IList<ClrModule> Modules
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override CcwData GetCcwDataByAddress(ulong addr)
        {
            throw new NotImplementedException();
        }
    }
}
