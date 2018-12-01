// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Native
{
    public class NativeHeap
    {
        public NativeRuntime Runtime { get; }

        internal NativeHeap(NativeRuntime runtime)
        {
            Runtime = runtime;
        }

        /*
        private void InitSegments()
        {
            if (runtime.GetHeaps(out SubHeap[] heaps))
            {
                List<HeapSegment> segments = new List<HeapSegment>();
                foreach (SubHeap heap in heaps)
                {
                    if (heap != null)
                    {
                        ISegmentData seg = runtime.GetSegmentData(heap.FirstLargeSegment);
                        while (seg != null)
                        {
                            HeapSegment segment = new HeapSegment(runtime, seg, heap, true, this);
                            segments.Add(segment);

                            UpdateSegmentData(segment);
                            seg = runtime.GetSegmentData(seg.Next);
                        }

                        seg = runtime.GetSegmentData(heap.FirstSegment);
                        while (seg != null)
                        {
                            HeapSegment segment = new HeapSegment(runtime, seg, heap, false, this);
                            segments.Add(segment);

                            UpdateSegmentData(segment);
                            seg = runtime.GetSegmentData(seg.Next);
                        }
                    }
                }

                UpdateSegments(segments.ToArray());
            }
            else
            {
                _segments = new ClrSegment[0];
            }
        }

        private NativeModule FindMrtModule()
        {
            foreach (NativeModule module in _modules)
                if (string.Compare(module.Name, "mrt100", StringComparison.CurrentCultureIgnoreCase) == 0 ||
                    string.Compare(module.Name, "mrt100_app", StringComparison.CurrentCultureIgnoreCase) == 0)
                    return module;

            return null;
        }

        public override ClrType GetObjectType(ulong objRef)
        {
            if (_lastObj == objRef)
                return _lastType;

            var cache = MemoryReader;
            if (!cache.Contains(objRef))
                cache = NativeRuntime.MemoryReader;

            if (!cache.ReadPtr(objRef, out ulong eeType))
                return null;

            ClrType last = this.GetTypeByMethodTable(eeType);
            _lastObj = objRef;
            _lastType = last;
            return last;
        }

        private ClrType ConstructObjectType(ulong eeType)
        {
            IMethodTableData mtData = NativeRuntime.GetMethodTableData(eeType);
            if (mtData == null)
                return null;

            ulong componentType = mtData.ElementTypeHandle;
            bool isArray = componentType != 0;

            // EEClass is the canonical method table.  I stuffed the pointer there instead of creating a new property.
            ulong canonType = isArray ? componentType : mtData.EEClass;
            if (!isArray && canonType != 0)
            {
                if (!isArray && _indices.TryGetValue(canonType, out int index))
                {
                    _indices[eeType] = index; // Link the original eeType to its canonical ClrType.
                    return _types[index];
                }

                ulong tmp = eeType;
                eeType = canonType;
                canonType = tmp;
            }

            NativeModule module = FindContainingModule(eeType);
            if (module == null && canonType != 0)
                module = FindContainingModule(canonType);

            string name = null;
            if (module != null)
            {
                Debug.Assert(module.ImageBase < eeType);

                PdbInfo pdb = module.Pdb;

                if (pdb != null)
                {
                    if (!_resolvers.TryGetValue(module, out ISymbolResolver resolver))
                        _resolvers[module] = resolver = _symProvider.GetSymbolResolver(pdb.FileName, pdb.Guid, pdb.Revision);

                    name = resolver?.GetSymbolNameByRVA((uint)(eeType - module.ImageBase));
                }
            }

            if (name == null)
            {
                string moduleName = module != null ? Path.GetFileNameWithoutExtension(module.FileName) : "UNKNWON";
                name = string.Format("{0}_{1:x}", moduleName, eeType);
            }

            int len = name.Length;
            if (name.EndsWith("::`vftable'"))
                len -= 11;

            int i = name.IndexOf('!') + 1;
            name = name.Substring(i, len - i);

            if (isArray)
                name += "[]";

            if (module == null)
                module = _mrtModule;

            NativeType type = new NativeType(this, _types.Count, module, name, eeType, mtData);
            _indices[eeType] = _types.Count;
            if (!isArray)
                _indices[canonType] = _types.Count;
            _types.Add(type);

            return type;
        }

        internal NativeModule GetModuleFromAddress(ulong addr)
        {
            // we expect addr to either be a pointer to a EE class desc or a stack IP pointing to a MD
            return FindContainingModule(addr);
        }

        private NativeModule FindContainingModule(ulong eeType)
        {
            int min = 0, max = _modules.Length;

            while (min <= max)
            {
                int mid = (min + max) / 2;

                int compare = _modules[mid].ComparePointer(eeType);
                if (compare < 0)
                    max = mid - 1;
                else if (compare > 0)
                    min = mid + 1;
                else
                    return _modules[mid];
            }

            return null;
        }

        public override IEnumerable<ClrRoot> EnumerateRoots()
        {
            return EnumerateRoots(true);
        }

        public override IEnumerable<ClrRoot> EnumerateRoots(bool enumerateStatics)
        {
            // Stack objects.
            foreach (NativeThread thread in NativeRuntime.Threads)
                foreach (var stackRef in NativeRuntime.EnumerateStackRoots(thread))
                    yield return stackRef;

            // Static Variables.
            foreach (var root in NativeRuntime.EnumerateStaticRoots(enumerateStatics))
                yield return root;

            // Handle Table.
            foreach (ClrRoot root in NativeRuntime.EnumerateHandleRoots())
                yield return root;

            // Finalizer Queue.
            ClrAppDomain domain = NativeRuntime.AppDomains[0];
            foreach (ulong obj in NativeRuntime.EnumerateFinalizerQueueObjectAddresses())
            {
                ClrType type = GetObjectType(obj);
                if (type == null)
                    continue;

                yield return new NativeFinalizerRoot(obj, type, domain, "finalizer root");
            }
        }

        public override int ReadMemory(ulong address, byte[] buffer, int offset, int count)
        {
            if (offset != 0)
                throw new NotImplementedException("Non-zero offsets not supported (yet)");

            if (!NativeRuntime.ReadMemory(address, buffer, count, out int bytesRead))
                return 0;

            return bytesRead;
        }
        */
    }
}