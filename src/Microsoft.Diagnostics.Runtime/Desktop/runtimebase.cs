// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using Microsoft.Diagnostics.Runtime.ICorDebug;
using System.Threading;

#pragma warning disable 649

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal enum DesktopVersion
    {
        v2,
        v4,
        v45
    }

    struct DomainContainer
    {
        public List<ClrAppDomain> Domains;
        public DesktopAppDomain System;
        public DesktopAppDomain Shared;
    }

    abstract internal class DesktopRuntimeBase : RuntimeBase
    {
        #region Variables
        protected CommonMethodTables _commonMTs;
        private Dictionary<uint, ICorDebug.ICorDebugThread> _corDebugThreads;
        private ClrModule[] _moduleList = null;
        private Lazy<List<ClrThread>> _threads;
        private Lazy<DesktopGCHeap> _heap;
        private Lazy<DesktopThreadPool> _threadpool;
        private ErrorModule _errorModule;
        private Lazy<DomainContainer> _appDomains;
        private Lazy<Dictionary<ulong, uint>> _moduleSizes;
        private Dictionary<ulong, DesktopModule> _modules = new Dictionary<ulong, DesktopModule>();
        private Dictionary<string, DesktopModule> _moduleFiles = new Dictionary<string, DesktopModule>();
        private Lazy<ClrModule> _mscorlib;
        #endregion

        internal DesktopRuntimeBase(ClrInfo info, DataTargetImpl dt, DacLibrary lib)
            : base(info, dt, lib)
        {
            _heap = new Lazy<DesktopGCHeap>(CreateHeap);
            _threads = new Lazy<List<ClrThread>>(CreateThreadList);
            _appDomains = new Lazy<DomainContainer>(CreateAppDomainList);
            _threadpool = new Lazy<DesktopThreadPool>(CreateThreadPoolData);
            _moduleSizes = new Lazy<Dictionary<ulong, uint>>(() => _dataReader.EnumerateModules().ToDictionary(module => module.ImageBase, module => module.FileSize));
            _mscorlib = new Lazy<ClrModule>(GetMscorlib);
        }


        /// <summary>
        /// Flushes the dac cache.  This function MUST be called any time you expect to call the same function
        /// but expect different results.  For example, after walking the heap, you need to call Flush before
        /// attempting to walk the heap again.
        /// </summary>
        public override void Flush()
        {
            OnRuntimeFlushed();

            Revision++;
            _dacInterface.Flush();
            
            MemoryReader = null;
            _moduleList = null;
            _modules = new Dictionary<ulong, DesktopModule>();
            _moduleFiles = new Dictionary<string, DesktopModule>();
            _threads = new Lazy<List<ClrThread>>(CreateThreadList);
            _appDomains = new Lazy<DomainContainer>(CreateAppDomainList);
            _heap = new Lazy<DesktopGCHeap>(CreateHeap);
            _threadpool = new Lazy<DesktopThreadPool>(CreateThreadPoolData);
            _mscorlib = new Lazy<ClrModule>(GetMscorlib);
        }

        internal ulong GetModuleSize(ulong address)
        {
            _moduleSizes.Value.TryGetValue(address, out uint size);
            return size;
        }

        internal int Revision { get; set; }

        public ErrorModule ErrorModule
        {
            get
            {
                if (_errorModule == null)
                    _errorModule = new ErrorModule(this);

                return _errorModule;
            }
        }

        internal override IGCInfo GetGCInfo()
        {
            IGCInfo data = GetGCInfoImpl();
            if (data == null)
            {
                throw new ClrDiagnosticsException("This runtime is not initialized and contains no data.", ClrDiagnosticsException.HR.RuntimeUninitialized);
            }

            return data;
        }

        public override IEnumerable<ClrException> EnumerateSerializedExceptions()
        {
            return new ClrException[0];
        }

        public override IEnumerable<int> EnumerateGCThreads()
        {
            foreach (uint thread in _dataReader.EnumerateAllThreads())
            {
                ulong teb = _dataReader.GetThreadTeb(thread);
                int threadType = DesktopThread.GetTlsSlotForThread(this, teb);
                if ((threadType & (int)DesktopThread.TlsThreadType.ThreadType_GC) == (int)DesktopThread.TlsThreadType.ThreadType_GC)
                    yield return (int)thread;
            }
        }


        /// <summary>
        /// Returns the version of the target process (v2, v4, v45)
        /// </summary>
        internal abstract DesktopVersion CLRVersion { get; }

        internal abstract IGCInfo GetGCInfoImpl();

        /// <summary>
        /// Returns the pointer size of the target process.
        /// </summary>
        public override int PointerSize
        {
            get
            {
                return IntPtr.Size;
            }
        }

        /// <summary>
        /// Returns the MethodTable for an array of objects.
        /// </summary>
        public ulong ArrayMethodTable
        {
            get
            {
                return _commonMTs.ArrayMethodTable;
            }
        }


        public override CcwData GetCcwDataByAddress(ulong addr)
        {
            var ccw = GetCCWData(addr);
            if (ccw == null)
                return null;

            return new DesktopCCWData((DesktopGCHeap)_heap.Value, addr, ccw);
        }

        internal ICorDebugThread GetCorDebugThread(uint osid)
        {
            if (_corDebugThreads == null)
            {
                _corDebugThreads = new Dictionary<uint, ICorDebugThread>();

                ICorDebugProcess process = CorDebugProcess;
                if (process == null)
                    return null;

                process.EnumerateThreads(out ICorDebugThreadEnum threadEnum);

                ICorDebugThread[] threads = new ICorDebugThread[1];
                while (threadEnum.Next(1, threads, out uint fetched) == 0 && fetched == 1)
                {
                    try
                    {
                        threads[0].GetID(out uint id);
                        _corDebugThreads[id] = threads[0];
                    }
                    catch
                    {
                    }
                }
            }

            _corDebugThreads.TryGetValue(osid, out ICorDebugThread result);
            return result;
        }

        public override IList<ClrAppDomain> AppDomains => _appDomains.Value.Domains;
        public override IList<ClrThread> Threads => _threads.Value;

        private List<ClrThread> CreateThreadList()
        {
            IThreadStoreData threadStore = GetThreadStoreData();
            ulong finalizer = ulong.MaxValue - 1;
            if (threadStore != null)
                finalizer = threadStore.Finalizer;

            List<ClrThread> threads = new List<ClrThread>();

            ulong addr = GetFirstThread();
            IThreadData thread = GetThread(addr);

            // Ensure we don't hit an infinite loop
            HashSet<ulong> seen = new HashSet<ulong> { addr };
            while (thread != null)
            {
                threads.Add(new DesktopThread(this, thread, addr, addr == finalizer));
                addr = thread.Next;
                if (seen.Contains(addr))
                    break;

                seen.Add(addr);
                thread = GetThread(addr);
            }

            return threads;
        }

        public ulong ExceptionMethodTable { get { return _commonMTs.ExceptionMethodTable; } }
        public ulong ObjectMethodTable
        {
            get
            {
                return _commonMTs.ObjectMethodTable;
            }
        }

        /// <summary>
        /// Returns the MethodTable for string objects.
        /// </summary>
        public ulong StringMethodTable
        {
            get
            {
                return _commonMTs.StringMethodTable;
            }
        }

        /// <summary>
        /// Returns the MethodTable for free space markers.
        /// </summary>
        public ulong FreeMethodTable
        {
            get
            {
                return _commonMTs.FreeMethodTable;
            }
        }

        public override ClrHeap Heap => _heap.Value;

        [Obsolete]
        public override ClrHeap GetHeap() => _heap.Value;

        private DesktopGCHeap CreateHeap()
        {
            if (HasArrayComponentMethodTables)
                return new LegacyGCHeap(this);
            else
                return  new V46GCHeap(this);
        }

        public override ClrThreadPool ThreadPool => _threadpool.Value;

        [Obsolete]
        public override ClrThreadPool GetThreadPool() => _threadpool.Value;
        
        public ulong SystemDomainAddress => _appDomains.Value.System.Address;
        public ulong SharedDomainAddress => _appDomains.Value.Shared.Address;
        public override ClrAppDomain SystemDomain => _appDomains.Value.System;
        public override ClrAppDomain SharedDomain => _appDomains.Value.Shared;
        public bool IsSingleDomain => _appDomains.Value.Domains.Count == 1;

        public override ClrMethod GetMethodByHandle(ulong methodHandle)
        {
            if (methodHandle == 0)
                return null;

            IMethodDescData methodDesc = GetMethodDescData(methodHandle);
            if (methodDesc == null)
                return null;

            ClrType type = Heap.GetTypeByMethodTable(methodDesc.MethodTable);
            if (type == null)
                return null;

            return type.GetMethod(methodDesc.MDToken);
        }

        /// <summary>
        /// Enumerates regions of memory which CLR has allocated with a description of what data
        /// resides at that location.  Note that this does not return every chunk of address space
        /// that CLR allocates.
        /// </summary>
        /// <returns>An enumeration of memory regions in the process.</returns>
        public override IEnumerable<ClrMemoryRegion> EnumerateMemoryRegions()
        {
            // Enumerate GC Segment regions.
            IHeapDetails[] heaps;
            if (ServerGC)
            {
                heaps = new IHeapDetails[HeapCount];
                int i = 0;
                ulong[] heapList = GetServerHeapList();
                if (heapList != null)
                {
                    foreach (ulong addr in heapList)
                    {
                        heaps[i++] = GetSvrHeapDetails(addr);
                        if (i == heaps.Length)
                            break;
                    }
                }
                else
                {
                    heaps = new IHeapDetails[0];
                }
            }
            else
            {
                Debug.Assert(HeapCount == 1);
                heaps = new IHeapDetails[1];
                heaps[0] = GetWksHeapDetails();
            }

            HashSet<ulong> addresses = new HashSet<ulong>();
            for (int i = 0; i < heaps.Length; ++i)
            {
                // Small heap
                ISegmentData segment = GetSegmentData(heaps[i].FirstHeapSegment);
                while (segment != null)
                {
                    Debug.Assert(segment.Start < segment.Committed);

                    GCSegmentType type = (segment.Address == heaps[i].EphemeralSegment) ? GCSegmentType.Ephemeral : GCSegmentType.Regular;
                    yield return new MemoryRegion(this, segment.Start, segment.Committed - segment.Start, ClrMemoryRegionType.GCSegment, (uint)i, type);

                    if (segment.Committed <= segment.Reserved)
                        yield return new MemoryRegion(this, segment.Committed, segment.Reserved - segment.Committed, ClrMemoryRegionType.ReservedGCSegment, (uint)i, type);

                    if (segment.Address == segment.Next || segment.Address == 0)
                    {
                        break;
                    }
                    else
                    {
                        if (!addresses.Add(segment.Next))
                            break;

                        segment = GetSegmentData(segment.Next);
                    }
                }

                segment = GetSegmentData(heaps[i].FirstLargeHeapSegment);
                while (segment != null)
                {
                    Debug.Assert(segment.Start < segment.Committed);

                    yield return new MemoryRegion(this, segment.Start, segment.Committed - segment.Start, ClrMemoryRegionType.GCSegment, (uint)i, GCSegmentType.LargeObject);

                    if (segment.Committed <= segment.Reserved)
                        yield return new MemoryRegion(this, segment.Committed, segment.Reserved - segment.Committed, ClrMemoryRegionType.ReservedGCSegment, (uint)i, GCSegmentType.LargeObject);

                    if (segment.Address == segment.Next || segment.Address == 0)
                    {
                        break;
                    }
                    else
                    {
                        if (!addresses.Add(segment.Next))
                            break;

                        segment = GetSegmentData(segment.Next);
                    }
                }
            }

            // Enumerate handle table regions.
            HashSet<ulong> regions = new HashSet<ulong>();
            foreach (ClrHandle handle in EnumerateHandles())
            {
                if (!_dataReader.VirtualQuery(handle.Address, out VirtualQueryData vq))
                    continue;

                if (regions.Contains(vq.BaseAddress))
                    continue;

                regions.Add(vq.BaseAddress);
                yield return new MemoryRegion(this, vq.BaseAddress, vq.Size, ClrMemoryRegionType.HandleTableChunk, handle.AppDomain);
            }

            // Enumerate each AppDomain and Module specific heap.
            AppDomainHeapWalker adhw = new AppDomainHeapWalker(this);
            IAppDomainData ad = GetAppDomainData(SystemDomainAddress);
            foreach (MemoryRegion region in adhw.EnumerateHeaps(ad))
                yield return region;

            foreach (ulong module in EnumerateModules(ad))
                foreach (MemoryRegion region in adhw.EnumerateModuleHeaps(ad, module))
                    yield return region;

            ad = GetAppDomainData(SharedDomainAddress);
            foreach (MemoryRegion region in adhw.EnumerateHeaps(ad))
                yield return region;

            foreach (ulong module in EnumerateModules(ad))
                foreach (MemoryRegion region in adhw.EnumerateModuleHeaps(ad, module))
                    yield return region;

            IAppDomainStoreData ads = GetAppDomainStoreData();
            if (ads != null)
            {
                IList<ulong> appDomains = GetAppDomainList(ads.Count);
                if (appDomains != null)
                {
                    foreach (ulong addr in appDomains)
                    {
                        ad = GetAppDomainData(addr);
                        foreach (MemoryRegion region in adhw.EnumerateHeaps(ad))
                            yield return region;

                        foreach (ulong module in EnumerateModules(ad))
                            foreach (MemoryRegion region in adhw.EnumerateModuleHeaps(ad, module))
                                yield return region;
                    }
                }
            }


            // Enumerate each JIT code heap.
            regions.Clear();
            foreach (ICodeHeap jitHeap in EnumerateJitHeaps())
            {
                if (jitHeap.Type == CodeHeapType.Host)
                {
                    if (_dataReader.VirtualQuery(jitHeap.Address, out VirtualQueryData vq))
                        yield return new MemoryRegion(this, vq.BaseAddress, vq.Size, ClrMemoryRegionType.JitHostCodeHeap);
                    else
                        yield return new MemoryRegion(this, jitHeap.Address, 0, ClrMemoryRegionType.JitHostCodeHeap);
                }
                else if (jitHeap.Type == CodeHeapType.Loader)
                {
                    foreach (MemoryRegion region in adhw.EnumerateJitHeap(jitHeap.Address))
                        yield return region;
                }
            }
        }

        /// <summary>
        /// Converts an address into an AppDomain.
        /// </summary>
        internal override ClrAppDomain GetAppDomainByAddress(ulong address)
        {
            foreach (var ad in AppDomains)
                if (ad.Address == address)
                    return ad;

            return null;
        }

        public override ClrMethod GetMethodByAddress(ulong ip)
        {
            IMethodDescData mdData = GetMDForIP(ip);
            if (mdData == null)
                return null;

            return DesktopMethod.Create(this, mdData);
        }

        internal IEnumerable<IRWLockData> EnumerateLockData(ulong thread)
        {
            // add offset of the m_pHead (tagLockEntry) field
            thread += GetRWLockDataOffset();
            if (ReadPointer(thread, out ulong firstEntry))
            {
                ulong lockEntry = firstEntry;
                byte[] output = GetByteArrayForStruct<RWLockData>();
                do
                {
                    if (!ReadMemory(lockEntry, output, output.Length, out int read) || read != output.Length)
                        break;

                    IRWLockData result = ConvertStruct<IRWLockData, RWLockData>(output);
                    if (result != null)
                        yield return result;

                    if (result.Next == lockEntry)
                        break;

                    lockEntry = result.Next;
                } while (lockEntry != firstEntry);
            }
        }

        #region Internal Functions
        protected ClrThread GetThreadByStackAddress(ulong address)
        {
            Debug.Assert(address != 0 || _dataReader.IsMinidump);

            foreach (ClrThread thread in _threads.Value)
            {
                ulong min = thread.StackBase;
                ulong max = thread.StackLimit;

                if (min > max)
                {
                    ulong tmp = min;
                    min = max;
                    max = tmp;
                }

                if (min <= address && address <= max)
                    return thread;
            }

            return null;
        }

        internal uint GetExceptionMessageOffset()
        {
            if (PointerSize == 8)
                return 0x20;

            return 0x10;
        }

        internal uint GetStackTraceOffset()
        {
            if (PointerSize == 8)
                return 0x40;

            return 0x20;
        }

        internal ClrThread GetThreadFromThinlockID(uint threadId)
        {
            ulong thread = GetThreadFromThinlock(threadId);
            if (thread == 0)
                return null;
            
            foreach (var clrThread in _threads.Value)
                if (clrThread.Address == thread)
                    return clrThread;

            return null;
        }

        public override IList<ClrModule> Modules
        {
            get
            {
                if (_moduleList == null)
                {
                    if (!_appDomains.IsValueCreated)
                    {
                        var value = _appDomains.Value;
                    }

                    _moduleList = UniqueModules(_modules.Values).ToArray();
                }

                return _moduleList;
            }
        }

        public ClrModule Mscorlib => _mscorlib.Value;

        private ClrModule GetMscorlib()
        {
            ClrModule mscorlib = null;
            string moduleName = ClrInfo.Flavor == ClrFlavor.Core
                                    ? "system.private.corelib"
                                    : "mscorlib";


            foreach (ClrModule module in _modules.Values)
                if (module.Name.ToLowerInvariant().Contains(moduleName))
                {
                    mscorlib = module;
                    break;
                }

            if (mscorlib == null)
            {
                IAppDomainStoreData ads = GetAppDomainStoreData();
                IAppDomainData sharedDomain = GetAppDomainData(ads.SharedDomain);
                foreach (ulong assembly in GetAssemblyList(ads.SharedDomain, sharedDomain.AssemblyCount))
                {
                    string name = GetAssemblyName(assembly);
                    if (name.ToLowerInvariant().Contains(moduleName))
                    {
                        IAssemblyData assemblyData = GetAssemblyData(ads.SharedDomain, assembly);
                        ulong module = GetModuleList(assembly, assemblyData.ModuleCount).Single();
                        mscorlib = GetModule(module);
                    }
                }
            }

            return mscorlib;
        }

        static IEnumerable<ClrModule> UniqueModules(Dictionary<ulong, DesktopModule>.ValueCollection self)
        {
            HashSet<DesktopModule> set = new HashSet<DesktopModule>();

            foreach (DesktopModule value in self)
            {
                if (set.Contains(value))
                    continue;

                set.Add(value);
                yield return value;
            }
        }

        internal IEnumerable<ulong> EnumerateModules(IAppDomainData appDomain)
        {
            if (appDomain != null)
            {
                ulong[] assemblies = GetAssemblyList(appDomain.Address, appDomain.AssemblyCount);
                if (assemblies != null)
                {
                    foreach (ulong assembly in assemblies)
                    {
                        IAssemblyData data = GetAssemblyData(appDomain.Address, assembly);
                        if (data == null)
                            continue;

                        ulong[] moduleList = GetModuleList(assembly, (int)data.ModuleCount);
                        if (moduleList != null)
                            foreach (ulong module in moduleList)
                                yield return module;
                    }
                }
            }
        }


        private DesktopThreadPool CreateThreadPoolData()
        {
            IThreadPoolData data = GetThreadPoolData();
            if (data == null)
                return null;

            return new DesktopThreadPool(this, data);
        }

        private DomainContainer CreateAppDomainList()
        {
            IAppDomainStoreData ads = GetAppDomainStoreData();
            if (ads == null)
                return new DomainContainer();

            IList<ulong> domains = GetAppDomainList(ads.Count);
            if (domains == null)
                return new DomainContainer();

            return new DomainContainer()
            {
                Domains = domains.Select(ad=>(ClrAppDomain)InitDomain(ad)).Where(ad => ad != null).ToList(),
                Shared = InitDomain(ads.SharedDomain, "Shared Domain"),
                System = InitDomain(ads.SystemDomain, "System Domain")
            };
        }

        private DesktopAppDomain InitDomain(ulong domain, string name = null)
        {
            ulong[] bases = new ulong[1];
            IAppDomainData domainData = GetAppDomainData(domain);
            if (domainData == null)
                return null;

            DesktopAppDomain appDomain = new DesktopAppDomain(this, domainData, name ?? GetAppDomaminName(domain));

            if (domainData.AssemblyCount > 0)
            {
                foreach (ulong assembly in GetAssemblyList(domain, domainData.AssemblyCount))
                {
                    IAssemblyData assemblyData = GetAssemblyData(domain, assembly);
                    if (assemblyData == null)
                        continue;

                    if (assemblyData.ModuleCount > 0)
                    {
                        foreach (ulong module in GetModuleList(assembly, assemblyData.ModuleCount))
                        {
                            DesktopModule clrModule = GetModule(module);
                            if (clrModule != null)
                            {
                                clrModule.AddMapping(appDomain, module);
                                appDomain.AddModule(clrModule);
                            }
                        }
                    }
                }
            }

            return appDomain;
        }

        internal DesktopModule GetModule(ulong module)
        {
            if (module == 0)
                return null;

            if (_modules.TryGetValue(module, out DesktopModule res))
                return res;

            IModuleData moduleData = GetModuleData(module);
            if (moduleData == null)
                return null;
            
            string peFile = GetPEFileName(moduleData.PEFile);
            string assemblyName = GetAssemblyName(moduleData.Assembly);

            if (peFile == null)
            {
                res = new DesktopModule(this, module, moduleData, peFile, assemblyName);
            }
            else if (!_moduleFiles.TryGetValue(peFile, out res))
            {
                res = new DesktopModule(this, module, moduleData, peFile, assemblyName);
                _moduleFiles[peFile] = res;
            }

            // We've modified the 'real' module list, so clear the cached version.
            _moduleList = null;
            _modules[module] = res;
            return res;
        }

        /// <summary>
        /// 
        /// Returns the name of the type as specified by the TypeHandle.  Note this returns the name as specified by the
        /// metadata, NOT as you would expect to see it in a C# program.  For example, generics are denoted with a ` and
        /// the number of params.  Thus a Dictionary (with two type params) would look like:
        ///     System.Collections.Generics.Dictionary`2
        /// </summary>
        /// <param name="id">The TypeHandle to get the name of.</param>
        /// <returns>The name of the type, or null on error.</returns>
        internal string GetTypeName(TypeHandle id)
        {
            if (id.MethodTable == FreeMethodTable)
                return "Free";

            if (id.MethodTable == ArrayMethodTable && id.ComponentMethodTable != 0)
            {
                string name = GetNameForMT(id.ComponentMethodTable);
                if (name != null)
                    return name + "[]";
            }

            return GetNameForMT(id.MethodTable);
        }

        protected IXCLRDataProcess GetClrDataProcess()
        {
            return _dacInterface;
        }


        internal IEnumerable<ClrStackFrame> EnumerateStackFrames(DesktopThread thread)
        {
            IXCLRDataProcess proc = GetClrDataProcess();

            int res = proc.GetTaskByOSThreadID(thread.OSThreadId, out object tmp);
            if (res < 0)
                yield break;

            IXCLRDataTask task = null;
            IXCLRDataStackWalk stackwalk = null;

            try
            {
                task = (IXCLRDataTask)tmp;
                res = task.CreateStackWalk(0xf, out tmp);
                if (res < 0)
                    yield break;

                stackwalk = (IXCLRDataStackWalk)tmp;
                byte[] ulongBuffer = new byte[8];
                byte[] context = ContextHelper.Context;
                do
                {
                    res = stackwalk.GetContext(ContextHelper.ContextFlags, ContextHelper.Length, out uint size, context);
                    if (res < 0 || res == 1)
                        break;

                    ulong ip, sp;

                    if (PointerSize == 4)
                    {
                        ip = BitConverter.ToUInt32(context, ContextHelper.InstructionPointerOffset);
                        sp = BitConverter.ToUInt32(context, ContextHelper.StackPointerOffset);
                    }
                    else
                    {
                        ip = BitConverter.ToUInt64(context, ContextHelper.InstructionPointerOffset);
                        sp = BitConverter.ToUInt64(context, ContextHelper.StackPointerOffset);
                    }

                    res = stackwalk.Request(0xf0000000, 0, null, (uint)ulongBuffer.Length, ulongBuffer);

                    ulong frameVtbl = 0;
                    if (res >= 0)
                    {
                        frameVtbl = BitConverter.ToUInt64(ulongBuffer, 0);
                        if (frameVtbl != 0)
                        {
                            sp = frameVtbl;
                            ReadPointer(sp, out frameVtbl);
                        }
                    }

                    DesktopStackFrame frame = GetStackFrame(thread, res, ip, sp, frameVtbl);
                    yield return frame;
                } while (stackwalk.Next() == 0);
            }
            finally
            {
                if (task != null)
                    Marshal.FinalReleaseComObject(task);

                if (stackwalk != null)
                    Marshal.FinalReleaseComObject(stackwalk);
            }
        }

        internal ILToNativeMap[] GetILMap(ulong ip)
        {
            List<ILToNativeMap> list = null;
            ILToNativeMap[] tmp = null;

            int res = _dacInterface.StartEnumMethodInstancesByAddress(ip, null, out ulong handle);
            if (res < 0)
                return null;

            res = _dacInterface.EnumMethodInstanceByAddress(ref handle, out object objMethod);

            while (res == 0)
            {
                IXCLRDataMethodInstance method = (IXCLRDataMethodInstance)objMethod;
                res = method.GetILAddressMap(0, out uint needed, null);
                if (res == 0)
                {
                    tmp = new ILToNativeMap[needed];
                    res = method.GetILAddressMap(needed, out needed, tmp);

                    for (int i = 0; i < tmp.Length; i++)
                    {
                        // There seems to be a bug in IL to native mappings where a throw statement
                        // may end up with an end address lower than the start address.  This is a
                        // workaround for that issue.
                        if (tmp[i].StartAddress > tmp[i].EndAddress)
                        {
                            if (i + 1 == tmp.Length)
                                tmp[i].EndAddress = tmp[i].StartAddress + 0x20;
                            else
                                tmp[i].EndAddress = tmp[i + 1].StartAddress - 1;
                        }
                    }

                    if (res != 0)
                        tmp = null;
                }

                res = _dacInterface.EnumMethodInstanceByAddress(ref handle, out objMethod);
                if (res == 0 && tmp != null)
                {
                    if (list == null)
                        list = new List<ILToNativeMap>();

                    list.AddRange(tmp);
                }
            }

            if (list != null)
            {
                list.AddRange(tmp);
                return list.ToArray();
            }

            _dacInterface.EndEnumMethodInstancesByAddress(handle);
            return tmp;
        }

        #endregion

        #region Abstract Functions

        internal abstract Dictionary<ulong, List<ulong>> GetDependentHandleMap(CancellationToken cancelToken);
        internal abstract uint GetExceptionHROffset();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void LoaderHeapTraverse(ulong address, IntPtr size, int isCurrent);
        internal abstract IList<ulong> GetAppDomainList(int count);
        internal abstract ulong[] GetAssemblyList(ulong appDomain, int count);
        internal abstract ulong[] GetModuleList(ulong assembly, int count);
        internal abstract IAssemblyData GetAssemblyData(ulong domain, ulong assembly);
        internal abstract IAppDomainStoreData GetAppDomainStoreData();
        internal abstract bool GetCommonMethodTables(ref CommonMethodTables mCommonMTs);
        internal abstract string GetNameForMT(ulong mt);
        internal abstract string GetPEFileName(ulong addr);
        internal abstract IModuleData GetModuleData(ulong addr);
        internal abstract IAppDomainData GetAppDomainData(ulong addr);
        internal abstract string GetAppDomaminName(ulong addr);
        internal abstract bool TraverseHeap(ulong heap, LoaderHeapTraverse callback);
        internal abstract bool TraverseStubHeap(ulong appDomain, int type, LoaderHeapTraverse callback);
        internal abstract IEnumerable<ICodeHeap> EnumerateJitHeaps();
        internal abstract ulong GetModuleForMT(ulong mt);
        internal abstract IFieldInfo GetFieldInfo(ulong mt);
        internal abstract IFieldData GetFieldData(ulong fieldDesc);
        internal abstract ICorDebug.IMetadataImport GetMetadataImport(ulong module);
        internal abstract IObjectData GetObjectData(ulong objRef);
        internal abstract ulong GetMethodTableByEEClass(ulong eeclass);
        internal abstract IList<MethodTableTokenPair> GetMethodTableList(ulong module);
        internal abstract IDomainLocalModuleData GetDomainLocalModule(ulong appDomain, ulong id);
        internal abstract ICCWData GetCCWData(ulong ccw);
        internal abstract IRCWData GetRCWData(ulong rcw);
        internal abstract COMInterfacePointerData[] GetCCWInterfaces(ulong ccw, int count);
        internal abstract COMInterfacePointerData[] GetRCWInterfaces(ulong rcw, int count);
        internal abstract ulong GetThreadStaticPointer(ulong thread, ClrElementType type, uint offset, uint moduleId, bool shared);
        internal abstract IDomainLocalModuleData GetDomainLocalModule(ulong module);
        internal abstract IList<ulong> GetMethodDescList(ulong methodTable);
        internal abstract string GetNameForMD(ulong md);
        internal abstract IMethodDescData GetMethodDescData(ulong md);
        internal abstract uint GetMetadataToken(ulong mt);
        protected abstract DesktopStackFrame GetStackFrame(DesktopThread thread, int res, ulong ip, ulong sp, ulong frameVtbl);
        internal abstract IList<ClrStackFrame> GetExceptionStackTrace(ulong obj, ClrType type);
        internal abstract string GetAssemblyName(ulong assembly);
        internal abstract string GetAppBase(ulong appDomain);
        internal abstract string GetConfigFile(ulong appDomain);
        internal abstract IMethodDescData GetMDForIP(ulong ip);
        protected abstract ulong GetThreadFromThinlock(uint threadId);
        internal abstract int GetSyncblkCount();
        internal abstract ISyncBlkData GetSyncblkData(int index);
        internal abstract IThreadPoolData GetThreadPoolData();
        protected abstract uint GetRWLockDataOffset();
        internal abstract IEnumerable<NativeWorkItem> EnumerateWorkItems();
        internal abstract uint GetStringFirstCharOffset();
        internal abstract uint GetStringLengthOffset();
        internal abstract ulong GetILForModule(ClrModule module, uint rva);
        #endregion


    }


    internal struct MethodTableTokenPair
    {
        public ulong MethodTable { get; set; }
        public uint Token { get; set; }

        public MethodTableTokenPair(ulong methodTable, uint token)
        {
            MethodTable = methodTable;
            Token = token;
        }
    }

    internal class MemoryRegion : ClrMemoryRegion
    {
        #region Private Variables
        private DesktopRuntimeBase _runtime;
        private ulong _domainModuleHeap;
        private GCSegmentType _segmentType;
        #endregion

        private bool HasAppDomainData
        {
            get
            {
                return Type <= ClrMemoryRegionType.CacheEntryHeap || Type == ClrMemoryRegionType.HandleTableChunk;
            }
        }

        private bool HasModuleData
        {
            get
            {
                return Type == ClrMemoryRegionType.ModuleThunkHeap || Type == ClrMemoryRegionType.ModuleLookupTableHeap;
            }
        }

        private bool HasGCHeapData
        {
            get
            {
                return Type == ClrMemoryRegionType.GCSegment || Type == ClrMemoryRegionType.ReservedGCSegment;
            }
        }


        public override ClrAppDomain AppDomain
        {
            get
            {
                if (!HasAppDomainData)
                    return null;
                return _runtime.GetAppDomainByAddress(_domainModuleHeap);
            }
        }

        public override string Module
        {
            get
            {
                if (!HasModuleData)
                    return null;

                return _runtime.GetModule(_domainModuleHeap).FileName;
            }
        }

        public override int HeapNumber
        {
            get
            {
                if (!HasGCHeapData)
                    return -1;

                Debug.Assert(_domainModuleHeap < uint.MaxValue);
                return (int)_domainModuleHeap;
            }
            set
            {
                _domainModuleHeap = (ulong)value;
            }
        }

        public override GCSegmentType GCSegmentType
        {
            get
            {
                if (!HasGCHeapData)
                    throw new NotSupportedException();

                return _segmentType;
            }
            set
            {
                _segmentType = value;
            }
        }

        public override string ToString(bool detailed)
        {
            string value = null;

            switch (Type)
            {
                case ClrMemoryRegionType.LowFrequencyLoaderHeap:
                    value = "Low Frequency Loader Heap";
                    break;

                case ClrMemoryRegionType.HighFrequencyLoaderHeap:
                    value = "High Frequency Loader Heap";
                    break;

                case ClrMemoryRegionType.StubHeap:
                    value = "Stub Heap";
                    break;

                // Virtual Call Stub heaps
                case ClrMemoryRegionType.IndcellHeap:
                    value = "Indirection Cell Heap";
                    break;

                case ClrMemoryRegionType.LookupHeap:
                    value = "Loopup Heap";
                    break;

                case ClrMemoryRegionType.ResolveHeap:
                    value = "Resolver Heap";
                    break;

                case ClrMemoryRegionType.DispatchHeap:
                    value = "Dispatch Heap";
                    break;

                case ClrMemoryRegionType.CacheEntryHeap:
                    value = "Cache Entry Heap";
                    break;

                // Other regions
                case ClrMemoryRegionType.JitHostCodeHeap:
                    value = "JIT Host Code Heap";
                    break;

                case ClrMemoryRegionType.JitLoaderCodeHeap:
                    value = "JIT Loader Code Heap";
                    break;

                case ClrMemoryRegionType.ModuleThunkHeap:
                    value = "Thunk Heap";
                    break;

                case ClrMemoryRegionType.ModuleLookupTableHeap:
                    value = "Lookup Table Heap";
                    break;

                case ClrMemoryRegionType.HandleTableChunk:
                    value = "GC Handle Table Chunk";
                    break;

                case ClrMemoryRegionType.ReservedGCSegment:
                case ClrMemoryRegionType.GCSegment:
                    if (_segmentType == GCSegmentType.Ephemeral)
                        value = "Ephemeral Segment";
                    else if (_segmentType == GCSegmentType.LargeObject)
                        value = "Large Object Segment";
                    else
                        value = "GC Segment";

                    if (Type == ClrMemoryRegionType.ReservedGCSegment)
                        value += " (Reserved)";
                    break;

                default:
                    // should never happen.
                    value = "<unknown>";
                    break;
            }

            if (detailed)
            {
                if (HasAppDomainData)
                {
                    if (_domainModuleHeap == _runtime.SharedDomainAddress)
                    {
                        value = string.Format("{0} for Shared AppDomain", value);
                    }
                    else if (_domainModuleHeap == _runtime.SystemDomainAddress)
                    {
                        value = string.Format("{0} for System AppDomain", value);
                    }
                    else
                    {
                        ClrAppDomain domain = AppDomain;
                        value = string.Format("{0} for AppDomain {1}: {2}", value, domain.Id, domain.Name);
                    }
                }
                else if (HasModuleData)
                {
                    string fn = _runtime.GetModule(_domainModuleHeap).FileName;
                    value = string.Format("{0} for Module: {1}", value, Path.GetFileName(fn));
                }
                else if (HasGCHeapData)
                {
                    value = string.Format("{0} for Heap {1}", value, HeapNumber);
                }
            }

            return value;
        }

        /// <summary>
        /// Equivalent to GetDisplayString(false).
        /// </summary>
        public override string ToString()
        {
            return ToString(false);
        }

        #region Constructors
        internal MemoryRegion(DesktopRuntimeBase clr, ulong addr, ulong size, ClrMemoryRegionType type, ulong moduleOrAppDomain)
        {
            Address = addr;
            Size = size;
            _runtime = clr;
            Type = type;
            _domainModuleHeap = moduleOrAppDomain;
        }

        internal MemoryRegion(DesktopRuntimeBase clr, ulong addr, ulong size, ClrMemoryRegionType type, ClrAppDomain domain)
        {
            Address = addr;
            Size = size;
            _runtime = clr;
            Type = type;
            _domainModuleHeap = domain.Address;
        }

        internal MemoryRegion(DesktopRuntimeBase clr, ulong addr, ulong size, ClrMemoryRegionType type)
        {
            Address = addr;
            Size = size;
            _runtime = clr;
            Type = type;
        }

        internal MemoryRegion(DesktopRuntimeBase clr, ulong addr, ulong size, ClrMemoryRegionType type, uint heap, GCSegmentType seg)
        {
            Address = addr;
            Size = size;
            _runtime = clr;
            Type = type;
            _domainModuleHeap = heap;
            _segmentType = seg;
        }
        #endregion
    }

    internal class SubHeap
    {
        internal int HeapNum { get; private set; }
        private IHeapDetails ActualHeap { get; set; }

        /// <summary>
        /// The allocation context pointers/limits for this heap.  The keys of this
        /// dictionary are the allocation pointers, the values of this dictionary are
        /// the limits.  If an allocation pointer is ever reached while walking a
        /// segment, you must "skip" past the allocation limit.  That is:
        ///     if (curr_obj is in AllocPointers)
        ///         curr_obj = AllocPointers[curr_obj] + min_object_size;
        /// </summary>
        internal Dictionary<ulong, ulong> AllocPointers { get; set; }

        /// <summary>
        /// Returns the address of the ephemeral segment.  Users of this API should use
        /// HeapSegment.Ephemeral instead of this property.
        /// </summary>
        internal ulong EphemeralSegment { get { return ActualHeap.EphemeralSegment; } }

        /// <summary>
        /// Returns the actual end of the ephemeral segment.
        /// </summary>
        internal ulong EphemeralEnd { get { return ActualHeap.EphemeralEnd; } }

        internal ulong Gen0Start { get { return ActualHeap.Gen0Start; } }
        internal ulong Gen1Start { get { return ActualHeap.Gen1Start; } }
        internal ulong Gen2Start { get { return ActualHeap.Gen2Start; } }
        internal ulong FirstLargeSegment { get { return ActualHeap.FirstLargeHeapSegment; } }
        internal ulong FirstSegment { get { return ActualHeap.FirstHeapSegment; } }
        internal ulong FQStart { get { return ActualHeap.FQAllObjectsStart; } }
        internal ulong FQStop { get { return ActualHeap.FQAllObjectsStop; } }
        internal ulong FQLiveStart { get { return ActualHeap.FQRootsStart; } }
        internal ulong FQLiveStop { get { return ActualHeap.FQRootsEnd; } }

        internal SubHeap(IHeapDetails heap, int heapNum, Dictionary<ulong,ulong> allocPointers)
        {
            ActualHeap = heap;
            HeapNum = heapNum;
            AllocPointers = allocPointers;
        }
    }


    #region Data Interfaces
    internal enum CodeHeapType
    {
        Loader,
        Host,
        Unknown
    }

    internal interface ICodeHeap
    {
        CodeHeapType Type { get; }
        ulong Address { get; }
    }

    internal interface IThreadPoolData
    {
        int TotalThreads { get; }
        int RunningThreads { get; }
        int IdleThreads { get; }
        int MinThreads { get; }
        int MaxThreads { get; }
        ulong FirstWorkRequest { get; }
        ulong QueueUserWorkItemCallbackFPtr { get; }
        ulong AsyncCallbackCompletionFPtr { get; }
        ulong AsyncTimerCallbackCompletionFPtr { get; }
        int MinCP { get; }
        int MaxCP { get; }
        int CPU { get; }
        int NumFreeCP { get; }
        int MaxFreeCP { get; }
    }

    internal interface IAssemblyData
    {
        ulong Address { get; }
        ulong ParentDomain { get; }
        ulong AppDomain { get; }
        bool IsDynamic { get; }
        bool IsDomainNeutral { get; }
        int ModuleCount { get; }
    }

    internal interface IAppDomainData
    {
        int Id { get; }
        ulong Address { get; }
        ulong LowFrequencyHeap { get; }
        ulong HighFrequencyHeap { get; }
        ulong StubHeap { get; }
        int AssemblyCount { get; }
    }

    internal interface IThreadStoreData
    {
        ulong Finalizer { get; }
        ulong FirstThread { get; }
        int Count { get; }
    }

    internal interface IThreadData
    {
        ulong Next { get; }
        ulong AllocPtr { get; }
        ulong AllocLimit { get; }
        uint OSThreadID { get; }
        uint ManagedThreadID { get; }
        ulong Teb { get; }
        ulong AppDomain { get; }
        uint LockCount { get; }
        int State { get; }
        ulong ExceptionPtr { get; }
        bool Preemptive { get; }
    }

    internal interface ISegmentData
    {
        ulong Address { get; }
        ulong Next { get; }
        ulong Start { get; }
        ulong End { get; }
        ulong Committed { get; }
        ulong Reserved { get; }
    }

    internal interface IHeapDetails
    {
        ulong FirstHeapSegment { get; }
        ulong FirstLargeHeapSegment { get; }
        ulong EphemeralSegment { get; }
        ulong EphemeralEnd { get; }
        ulong EphemeralAllocContextPtr { get; }
        ulong EphemeralAllocContextLimit { get; }

        ulong Gen0Start { get; }
        ulong Gen0Stop { get; }
        ulong Gen1Start { get; }
        ulong Gen1Stop { get; }
        ulong Gen2Start { get; }
        ulong Gen2Stop { get; }

        ulong FQAllObjectsStart { get; }
        ulong FQAllObjectsStop { get; }
        ulong FQRootsStart { get; }
        ulong FQRootsEnd { get; }
    }

    internal interface IGCInfo
    {
        bool ServerMode { get; }
        int HeapCount { get; }
        int MaxGeneration { get; }
        bool GCStructuresValid { get; }
    }

    internal interface IMethodTableData
    {
        bool Shared { get; }
        bool Free { get; }
        bool ContainsPointers { get; }
        uint BaseSize { get; }
        uint ComponentSize { get; }
        ulong EEClass { get; }
        ulong Parent { get; }
        uint NumMethods { get; }
        ulong ElementTypeHandle { get; }
    }

    internal interface IFieldInfo
    {
        uint InstanceFields { get; }
        uint StaticFields { get; }
        uint ThreadStaticFields { get; }
        ulong FirstField { get; }
    }

    internal interface IFieldData
    {
        uint CorElementType { get; }
        uint SigType { get; }
        ulong TypeMethodTable { get; }

        ulong Module { get; }
        uint TypeToken { get; }

        uint FieldToken { get; }
        ulong EnclosingMethodTable { get; }
        uint Offset { get; }
        bool IsThreadLocal { get; }
        bool IsContextLocal { get; }
        bool IsStatic { get; }
        ulong NextField { get; }
    }

    internal interface IEEClassData
    {
        ulong MethodTable { get; }
        ulong Module { get; }
    }

    internal interface IDomainLocalModuleData
    {
        ulong AppDomainAddr { get; }
        ulong ModuleID { get; }

        ulong ClassData { get; }
        ulong DynamicClassTable { get; }
        ulong GCStaticDataStart { get; }
        ulong NonGCStaticDataStart { get; }
    }

    internal interface IModuleData
    {
        ulong ImageBase { get; }
        ulong PEFile { get; }
        ulong LookupTableHeap { get; }
        ulong ThunkHeap { get; }
        object LegacyMetaDataImport { get; }
        ulong ModuleId { get; }
        ulong ModuleIndex { get; }
        ulong Assembly { get; }
        bool IsReflection { get; }
        bool IsPEFile { get; }
        ulong MetdataStart { get; }
        ulong MetadataLength { get; }
    }

    internal interface IMethodDescData
    {
        ulong GCInfo { get; }
        ulong MethodDesc { get; }
        ulong Module { get; }
        uint MDToken { get; }
        ulong NativeCodeAddr { get; }
        ulong MethodTable { get; }
        MethodCompilationType JITType { get; }
        ulong ColdStart { get; }
        uint ColdSize { get; }
        uint HotSize { get; }
    }

    internal interface ICCWData
    {
        ulong IUnknown { get; }
        ulong Object { get; }
        ulong Handle { get; }
        ulong CCWAddress { get; }
        int RefCount { get; }
        int JupiterRefCount { get; }
        int InterfaceCount { get; }
    }

    internal interface IRCWData
    {
        ulong IdentityPointer { get; }
        ulong UnknownPointer { get; }
        ulong ManagedObject { get; }
        ulong JupiterObject { get; }
        ulong VTablePtr { get; }
        ulong CreatorThread { get; }

        int RefCount { get; }
        int InterfaceCount { get; }

        bool IsJupiterObject { get; }
        bool IsDisconnected { get; }
    }

    internal interface IAppDomainStoreData
    {
        ulong SharedDomain { get; }
        ulong SystemDomain { get; }
        int Count { get; }
    }

    internal interface IObjectData
    {
        ulong DataPointer { get; }
        ulong ElementTypeHandle { get; }
        ClrElementType ElementType { get; }
        ulong RCW { get; }
        ulong CCW { get; }
    }

    internal interface ISyncBlkData
    {
        bool Free { get; }
        ulong Address { get; }
        ulong Object { get; }
        ulong OwningThread { get; }
        bool MonitorHeld { get; }
        uint Recursion { get; }
        uint TotalCount { get; }
    }

    // This is consistent across all dac versions.  No need for interface.
    internal struct CommonMethodTables
    {
        public ulong ArrayMethodTable;
        public ulong StringMethodTable;
        public ulong ObjectMethodTable;
        public ulong ExceptionMethodTable;
        public ulong FreeMethodTable;

        public bool Validate()
        {
            return ArrayMethodTable != 0 &&
                StringMethodTable != 0 &&
                ObjectMethodTable != 0 &&
                ExceptionMethodTable != 0 &&
                FreeMethodTable != 0;
        }
    };


    internal interface IRWLockData
    {
        ulong Next { get; }
        int ULockID { get; }
        int LLockID { get; }
        int Level { get; }
    }

    internal struct RWLockData : IRWLockData
    {
        public IntPtr pNext;
        public IntPtr pPrev;
        public int _uLockID;
        public int _lLockID;
        public Int16 wReaderLevel;

        public ulong Next
        {
            get { return (ulong)pNext.ToInt64(); }
        }

        public int ULockID
        {
            get { return _uLockID; }
        }

        public int LLockID
        {
            get { return _lLockID; }
        }


        public int Level
        {
            get { return wReaderLevel; }
        }
    }
    #endregion
}
