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
            _moduleSizes.Value.TryGetValue(address, out var size);
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
            var data = GetGCInfoImpl();
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
            foreach (var thread in _dataReader.EnumerateAllThreads())
            {
                var teb = _dataReader.GetThreadTeb(thread);
                var threadType = ThreadBase.GetTlsSlotForThread(this, teb);
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
            var ccw = GetCCWData(addr);
            if (ccw == null)
                return null;

            return new DesktopCCWData(_heap.Value, addr, ccw);
        }

        internal ICorDebugThread GetCorDebugThread(uint osid)
        {
            if (_corDebugThreads == null)
            {
                _corDebugThreads = new Dictionary<uint, ICorDebugThread>();

                var process = CorDebugProcess;
                if (process == null)
                    return null;

                process.EnumerateThreads(out var threadEnum);

                var threads = new ICorDebugThread[1];
                while (threadEnum.Next(1, threads, out var fetched) == 0 && fetched == 1)
                {
                    try
                    {
                        threads[0].GetID(out var id);
                        _corDebugThreads[id] = threads[0];
                    }
                    catch
                    {
                    }
                }
            }

            _corDebugThreads.TryGetValue(osid, out var result);
            return result;
        }

        public override IList<ClrAppDomain> AppDomains => _appDomains.Value.Domains;
        public override IList<ClrThread> Threads => _threads.Value;

        private List<ClrThread> CreateThreadList()
        {
            var threadStore = GetThreadStoreData();
            var finalizer = ulong.MaxValue - 1;
            if (threadStore != null)
                finalizer = threadStore.Finalizer;

            var threads = new List<ClrThread>();

            var addr = GetFirstThread();
            var thread = GetThread(addr);

            // Ensure we don't hit an infinite loop
            var seen = new HashSet<ulong> {addr};
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

            var methodDesc = GetMethodDescData(methodHandle);
            if (methodDesc == null)
                return null;

            var type = Heap.GetTypeByMethodTable(methodDesc.MethodTable);
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
                var i = 0;
                var heapList = GetServerHeapList();
                if (heapList != null)
                {
                    foreach (var addr in heapList)
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

            var addresses = new HashSet<ulong>();
            for (var i = 0; i < heaps.Length; ++i)
            {
                // Small heap
                var segment = GetSegmentData(heaps[i].FirstHeapSegment);
                while (segment != null)
                {
                    Debug.Assert(segment.Start < segment.Committed);

                    var type = segment.Address == heaps[i].EphemeralSegment ? GCSegmentType.Ephemeral : GCSegmentType.Regular;
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
            var regions = new HashSet<ulong>();
            foreach (var handle in EnumerateHandles())
            {
                if (!_dataReader.VirtualQuery(handle.Address, out var vq))
                    continue;

                if (regions.Contains(vq.BaseAddress))
                    continue;

                regions.Add(vq.BaseAddress);
                yield return new MemoryRegion(this, vq.BaseAddress, vq.Size, ClrMemoryRegionType.HandleTableChunk, handle.AppDomain);
            }

            // Enumerate each AppDomain and Module specific heap.
            var adhw = new AppDomainHeapWalker(this);
            var ad = GetAppDomainData(SystemDomainAddress);
            foreach (var region in adhw.EnumerateHeaps(ad))
                yield return region;

            foreach (var module in EnumerateModules(ad))
                foreach (var region in adhw.EnumerateModuleHeaps(ad, module))
                    yield return region;

            ad = GetAppDomainData(SharedDomainAddress);
            foreach (var region in adhw.EnumerateHeaps(ad))
                yield return region;

            foreach (var module in EnumerateModules(ad))
                foreach (var region in adhw.EnumerateModuleHeaps(ad, module))
                    yield return region;

            var ads = GetAppDomainStoreData();
            if (ads != null)
            {
                var appDomains = GetAppDomainList(ads.Count);
                if (appDomains != null)
                {
                    foreach (var addr in appDomains)
                    {
                        ad = GetAppDomainData(addr);
                        foreach (var region in adhw.EnumerateHeaps(ad))
                            yield return region;

                        foreach (var module in EnumerateModules(ad))
                            foreach (var region in adhw.EnumerateModuleHeaps(ad, module))
                                yield return region;
                    }
                }
            }

            // Enumerate each JIT code heap.
            regions.Clear();
            foreach (var jitHeap in EnumerateJitHeaps())
            {
                if (jitHeap.Type == CodeHeapType.Host)
                {
                    if (_dataReader.VirtualQuery(jitHeap.Address, out var vq))
                        yield return new MemoryRegion(this, vq.BaseAddress, vq.Size, ClrMemoryRegionType.JitHostCodeHeap);
                    else
                        yield return new MemoryRegion(this, jitHeap.Address, 0, ClrMemoryRegionType.JitHostCodeHeap);
                }
                else if (jitHeap.Type == CodeHeapType.Loader)
                {
                    foreach (var region in adhw.EnumerateJitHeap(jitHeap.Address))
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
            var mdData = GetMDForIP(ip);
            if (mdData == null)
                return null;

            return DesktopMethod.Create(this, mdData);
        }

        internal IEnumerable<IRWLockData> EnumerateLockData(ulong thread)
        {
            // add offset of the m_pHead (tagLockEntry) field
            thread += GetRWLockDataOffset();
            if (ReadPointer(thread, out var firstEntry))
            {
                var lockEntry = firstEntry;
                var output = GetByteArrayForStruct<RWLockData>();
                do
                {
                    if (!ReadMemory(lockEntry, output, output.Length, out var read) || read != output.Length)
                        break;

                    var result = ConvertStruct<IRWLockData, RWLockData>(output);
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

            foreach (var thread in _threads.Value)
            {
                var min = thread.StackBase;
                var max = thread.StackLimit;

                if (min > max)
                {
                    var tmp = min;
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
            var thread = GetThreadFromThinlock(threadId);
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
            var moduleName = ClrInfo.Flavor == ClrFlavor.Core
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
                var ads = GetAppDomainStoreData();
                var sharedDomain = GetAppDomainData(ads.SharedDomain);
                foreach (var assembly in GetAssemblyList(ads.SharedDomain, sharedDomain.AssemblyCount))
                {
                    var name = GetAssemblyName(assembly);
                    if (name.ToLowerInvariant().Contains(moduleName))
                    {
                        var assemblyData = GetAssemblyData(ads.SharedDomain, assembly);
                        var module = GetModuleList(assembly, assemblyData.ModuleCount).Single();
                        mscorlib = GetModule(module);
                    }
                }
            }

            return mscorlib;
        }

        private static IEnumerable<ClrModule> UniqueModules(Dictionary<ulong, DesktopModule>.ValueCollection self)
        {
            var set = new HashSet<DesktopModule>();

            foreach (var value in self)
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
                var assemblies = GetAssemblyList(appDomain.Address, appDomain.AssemblyCount);
                if (assemblies != null)
                {
                    foreach (var assembly in assemblies)
                    {
                        var data = GetAssemblyData(appDomain.Address, assembly);
                        if (data == null)
                            continue;

                        var moduleList = GetModuleList(assembly, data.ModuleCount);
                        if (moduleList != null)
                            foreach (var module in moduleList)
                                yield return module;
                    }
                }
            }
        }

        private DesktopThreadPool CreateThreadPoolData()
        {
            var data = GetThreadPoolData();
            if (data == null)
                return null;

            return new DesktopThreadPool(this, data);
        }

        private DomainContainer CreateAppDomainList()
        {
            var ads = GetAppDomainStoreData();
            if (ads == null)
                return new DomainContainer();

            var domains = GetAppDomainList(ads.Count);
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
            var bases = new ulong[1];
            var domainData = GetAppDomainData(domain);
            if (domainData == null)
                return null;

            var appDomain = new DesktopAppDomain(this, domainData, name ?? GetAppDomaminName(domain));

            if (domainData.AssemblyCount > 0)
            {
                foreach (var assembly in GetAssemblyList(domain, domainData.AssemblyCount))
                {
                    var assemblyData = GetAssemblyData(domain, assembly);
                    if (assemblyData == null)
                        continue;

                    if (assemblyData.ModuleCount > 0)
                    {
                        foreach (var module in GetModuleList(assembly, assemblyData.ModuleCount))
                        {
                            var clrModule = GetModule(module);
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

            if (_modules.TryGetValue(module, out var res))
                return res;

            var moduleData = GetModuleData(module);
            if (moduleData == null)
                return null;

            var peFile = GetPEFileName(moduleData.PEFile);
            var assemblyName = GetAssemblyName(moduleData.Assembly);

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
                var name = GetNameForMT(id.ComponentMethodTable);
                if (name != null)
                    return name + "[]";
            }

            return GetNameForMT(id.MethodTable);
        }

        internal IEnumerable<ClrStackFrame> EnumerateStackFrames(DesktopThread thread)
        {
            using (var stackwalk = _dacInterface.CreateStackWalk(thread.OSThreadId, 0xf))
            {
                if (stackwalk == null)
                    yield break;

                var ulongBuffer = new byte[8];
                var context = ContextHelper.Context;
                do
                {
                    if (!stackwalk.GetContext(ContextHelper.ContextFlags, ContextHelper.Length, out var size, context))
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

                    var frameVtbl = stackwalk.GetFrameVtable();
                    if (frameVtbl != 0)
                    {
                        sp = frameVtbl;
                        ReadPointer(sp, out frameVtbl);
                    }

                    var frame = GetStackFrame(thread, ip, sp, frameVtbl);
                    yield return frame;
                } while (stackwalk.Next());
            }
        }

        internal ILToNativeMap[] GetILMap(ulong ip)
        {
            var list = new List<ILToNativeMap>();

            foreach (var method in _dacInterface.EnumerateMethodInstancesByAddress(ip))
            {
                var map = method.GetILToNativeMap();
                if (map != null)
                {
                    for (var i = 0; i < map.Length; i++)
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
        internal abstract string GetNameForMT(ulong mt);
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
        protected abstract DesktopStackFrame GetStackFrame(DesktopThread thread, ulong ip, ulong sp, ulong frameVtbl);
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