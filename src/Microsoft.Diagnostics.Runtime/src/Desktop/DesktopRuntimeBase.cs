// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.ICorDebug;

#pragma warning disable 649

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal enum DesktopVersion
    {
        v2,
        v4,
        v45
    }

    internal abstract class DesktopRuntimeBase : RuntimeBase
    {
        protected CommonMethodTables _commonMTs;
        private Dictionary<uint, ICorDebugThread> _corDebugThreads;
        private ClrModule[] _moduleList;
        private Lazy<List<ClrThread>> _threads;
        private Lazy<DesktopGCHeap> _heap;
        private Lazy<DesktopThreadPool> _threadpool;
        private ErrorModule _errorModule;
        private Lazy<DomainContainer> _appDomains;
        private readonly Lazy<Dictionary<ulong, uint>> _moduleSizes;
        private Dictionary<ulong, DesktopModule> _modules = new Dictionary<ulong, DesktopModule>();
        private Dictionary<string, DesktopModule> _moduleFiles = new Dictionary<string, DesktopModule>();
        private Lazy<ClrModule> _mscorlib;

        internal DesktopRuntimeBase(ClrInfo info, DataTarget dt, DacLibrary lib)
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
                throw new ClrDiagnosticsException("This runtime is not initialized and contains no data.", ClrDiagnosticsExceptionKind.RuntimeUninitialized);
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
                int threadType = ThreadBase.GetTlsSlotForThread(this, teb);
                if ((threadType & (int)ThreadBase.TlsThreadType.ThreadType_GC) == (int)ThreadBase.TlsThreadType.ThreadType_GC)
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
        public override int PointerSize => IntPtr.Size;

        /// <summary>
        /// Returns the MethodTable for an array of objects.
        /// </summary>
        public ulong ArrayMethodTable => _commonMTs.ArrayMethodTable;

        public override CcwData GetCcwDataByAddress(ulong addr)
        {
            ICCWData ccw = GetCCWData(addr);
            if (ccw == null)
                return null;

            return new DesktopCCWData(_heap.Value, addr, ccw);
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
            HashSet<ulong> seen = new HashSet<ulong> {addr};
            while (thread != null)
            {
                threads.Add(new DesktopThread(this, thread, addr, addr == finalizer));
                addr = thread.Next;
                if (seen.Contains(addr) || addr == 0)
                    break;

                seen.Add(addr);
                thread = GetThread(addr);
            }

            return threads;
        }

        public ulong ExceptionMethodTable => _commonMTs.ExceptionMethodTable;
        public ulong ObjectMethodTable => _commonMTs.ObjectMethodTable;

        /// <summary>
        /// Returns the MethodTable for string objects.
        /// </summary>
        public ulong StringMethodTable => _commonMTs.StringMethodTable;

        /// <summary>
        /// Returns the MethodTable for free space markers.
        /// </summary>
        public ulong FreeMethodTable => _commonMTs.FreeMethodTable;

        public override ClrHeap Heap => _heap.Value;

        private DesktopGCHeap CreateHeap()
        {
            if (HasArrayComponentMethodTables)
                return new LegacyGCHeap(this);

            return new V46GCHeap(this);
        }

        public override ClrThreadPool ThreadPool => _threadpool.Value;
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

                    GCSegmentType type = segment.Address == heaps[i].EphemeralSegment ? GCSegmentType.Ephemeral : GCSegmentType.Regular;
                    yield return new MemoryRegion(this, segment.Start, segment.Committed - segment.Start, ClrMemoryRegionType.GCSegment, (uint)i, type);

                    if (segment.Committed <= segment.Reserved)
                        yield return new MemoryRegion(this, segment.Committed, segment.Reserved - segment.Committed, ClrMemoryRegionType.ReservedGCSegment, (uint)i, type);

                    if (segment.Address == segment.Next || segment.Address == 0)
                    {
                        break;
                    }

                    if (!addresses.Add(segment.Next))
                        break;

                    segment = GetSegmentData(segment.Next);
                }

                segment = GetSegmentData(heaps[i].FirstLargeHeapSegment);
                while (segment != null)
                {
                    Debug.Assert(segment.Start < segment.Committed);

                    yield return new MemoryRegion(this, segment.Start, segment.Committed - segment.Start, ClrMemoryRegionType.GCSegment, (uint)i, GCSegmentType.LargeObject);

                    if (segment.Committed <= segment.Reserved)
                        yield return new MemoryRegion(
                            this,
                            segment.Committed,
                            segment.Reserved - segment.Committed,
                            ClrMemoryRegionType.ReservedGCSegment,
                            (uint)i,
                            GCSegmentType.LargeObject);

                    if (segment.Address == segment.Next || segment.Address == 0)
                    {
                        break;
                    }

                    if (!addresses.Add(segment.Next))
                        break;

                    segment = GetSegmentData(segment.Next);
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
                ulong[] appDomains = GetAppDomainList(ads.Count);
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
            foreach (ClrAppDomain ad in AppDomains)
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

            foreach (ClrThread clrThread in _threads.Value)
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
                        DomainContainer value = _appDomains.Value;
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

        private static IEnumerable<ClrModule> UniqueModules(Dictionary<ulong, DesktopModule>.ValueCollection self)
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

                        ulong[] moduleList = GetModuleList(assembly, data.ModuleCount);
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

            ulong[] domains = GetAppDomainList(ads.Count);
            if (domains == null)
                return new DomainContainer();

            return new DomainContainer
            {
                Domains = domains.Select(ad => (ClrAppDomain)InitDomain(ad)).Where(ad => ad != null).ToList(),
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
        /// Returns the name of the type as specified by the TypeHandle.  Note this returns the name as specified by the
        /// metadata, NOT as you would expect to see it in a C# program.  For example, generics are denoted with a ` and
        /// the number of params.  Thus a Dictionary (with two type params) would look like:
        /// System.Collections.Generics.Dictionary`2
        /// </summary>
        /// <param name="id">The TypeHandle to get the name of.</param>
        /// <returns>The name of the type, or null on error.</returns>
        internal string GetTypeName(TypeHandle id)
        {
            if (id.MethodTable == FreeMethodTable)
                return "Free";

            if (id.MethodTable == ArrayMethodTable && id.ComponentMethodTable != 0)
            {
                string name = GetMethodTableName(id.ComponentMethodTable);
                if (name != null)
                    return name + "[]";
            }

            return GetMethodTableName(id.MethodTable);
        }

        internal IEnumerable<ClrStackFrame> EnumerateStackFrames(DesktopThread thread)
        {
            using (ClrStackWalk stackwalk = _dacInterface.CreateStackWalk(thread.OSThreadId, 0xf))
            {
                if (stackwalk == null)
                    yield break;

                byte[] ulongBuffer = new byte[8];
                byte[] context = ContextHelper.Context;
                do
                {
                    if (!stackwalk.GetContext(ContextHelper.ContextFlags, ContextHelper.Length, out uint size, context))
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

                    ulong frameVtbl = stackwalk.GetFrameVtable();
                    if (frameVtbl != 0)
                    {
                        sp = frameVtbl;
                        ReadPointer(sp, out frameVtbl);
                    }

                    byte[] contextCopy = new byte[context.Length];
                    Buffer.BlockCopy(context, 0, contextCopy, 0, context.Length);

                    DesktopStackFrame frame = GetStackFrame(thread, contextCopy, ip, sp, frameVtbl);
                    yield return frame;
                } while (stackwalk.Next());
            }
        }

        internal ILToNativeMap[] GetILMap(ulong ip)
        {
            List<ILToNativeMap> list = new List<ILToNativeMap>();

            foreach (ClrDataMethod method in _dacInterface.EnumerateMethodInstancesByAddress(ip))
            {
                ILToNativeMap[] map = method.GetILToNativeMap();
                if (map != null)
                {
                    for (int i = 0; i < map.Length; i++)
                    {
                        // There seems to be a bug in IL to native mappings where a throw statement
                        // may end up with an end address lower than the start address.  This is a
                        // workaround for that issue.
                        if (map[i].StartAddress > map[i].EndAddress)
                        {
                            if (i + 1 == map.Length)
                                map[i].EndAddress = map[i].StartAddress + 0x20;
                            else
                                map[i].EndAddress = map[i + 1].StartAddress - 1;
                        }
                    }

                    list.AddRange(map);
                }

                method.Dispose();
            }

            return list.ToArray();
        }

        internal abstract Dictionary<ulong, List<ulong>> GetDependentHandleMap(CancellationToken cancelToken);
        internal abstract uint GetExceptionHROffset();
        internal abstract ulong[] GetAppDomainList(int count);
        internal abstract ulong[] GetAssemblyList(ulong appDomain, int count);
        internal abstract ulong[] GetModuleList(ulong assembly, int count);
        internal abstract IAssemblyData GetAssemblyData(ulong domain, ulong assembly);
        internal abstract IAppDomainStoreData GetAppDomainStoreData();
        internal abstract bool GetCommonMethodTables(ref CommonMethodTables mCommonMTs);
        internal abstract string GetPEFileName(ulong addr);
        internal abstract IModuleData GetModuleData(ulong addr);
        internal abstract IAppDomainData GetAppDomainData(ulong addr);
        internal abstract string GetAppDomaminName(ulong addr);
        internal abstract bool TraverseHeap(ulong heap, SOSDac.LoaderHeapTraverse callback);
        internal abstract bool TraverseStubHeap(ulong appDomain, int type, SOSDac.LoaderHeapTraverse callback);
        internal abstract IEnumerable<ICodeHeap> EnumerateJitHeaps();
        internal abstract ulong GetModuleForMT(ulong mt);
        internal abstract IFieldInfo GetFieldInfo(ulong mt);
        internal abstract IFieldData GetFieldData(ulong fieldDesc);
        internal abstract MetaDataImport GetMetadataImport(ulong module);
        internal abstract IObjectData GetObjectData(ulong objRef);
        internal abstract ulong GetMethodTableByEEClass(ulong eeclass);
        internal abstract IList<MethodTableTokenPair> GetMethodTableList(ulong module);
        internal abstract IDomainLocalModuleData GetDomainLocalModuleById(ulong appDomain, ulong id);
        internal abstract ICCWData GetCCWData(ulong ccw);
        internal abstract IRCWData GetRCWData(ulong rcw);
        internal abstract COMInterfacePointerData[] GetCCWInterfaces(ulong ccw, int count);
        internal abstract COMInterfacePointerData[] GetRCWInterfaces(ulong rcw, int count);
        internal abstract ulong GetThreadStaticPointer(ulong thread, ClrElementType type, uint offset, uint moduleId, bool shared);
        internal abstract IDomainLocalModuleData GetDomainLocalModule(ulong appDomain, ulong module);
        internal abstract IList<ulong> GetMethodDescList(ulong methodTable);
        internal abstract string GetNameForMD(ulong md);
        internal abstract IMethodDescData GetMethodDescData(ulong md);
        internal abstract uint GetMetadataToken(ulong mt);
        protected abstract DesktopStackFrame GetStackFrame(DesktopThread thread, byte[] context, ulong ip, ulong sp, ulong frameVtbl);
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

        private struct DomainContainer
        {
            public List<ClrAppDomain> Domains;
            public DesktopAppDomain System;
            public DesktopAppDomain Shared;
        }
    }
}