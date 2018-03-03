// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal abstract class DesktopGCHeap : HeapBase
    {
        public DesktopGCHeap(DesktopRuntimeBase runtime)
            : base(runtime)
        {
            DesktopRuntime = runtime;
            _types = new List<ClrType>(1000);
            Revision = runtime.Revision;

            // Prepopulate a few important method tables.
            _arrayType = new Lazy<ClrType>(CreateArrayType);
            _exceptionType = new Lazy<ClrType>(() => GetTypeByMethodTable(DesktopRuntime.ExceptionMethodTable, 0, 0));
            ErrorType = new ErrorType(this);

            StringType = DesktopRuntime.StringMethodTable != 0 ? GetTypeByMethodTable(DesktopRuntime.StringMethodTable, 0, 0) : ErrorType;
            ObjectType = DesktopRuntime.ObjectMethodTable != 0 ? GetTypeByMethodTable(DesktopRuntime.ObjectMethodTable, 0, 0) : ErrorType;
            if (DesktopRuntime.FreeMethodTable != 0)
            {
                var free = GetTypeByMethodTable(DesktopRuntime.FreeMethodTable, 0, 0);

                ((DesktopHeapType)free).Shared = true;
                ((BaseDesktopHeapType)free).DesktopModule = ObjectType.Module as DesktopModule;
                _free = free;
            }
            else
            {
                _free = ErrorType;
            }

            InitSegments(runtime);
        }

        private ClrType CreateFree()
        {
            var free = GetTypeByMethodTable(DesktopRuntime.FreeMethodTable, 0, 0);

            ((DesktopHeapType)free).Shared = true;
            ((BaseDesktopHeapType)free).DesktopModule = (DesktopModule)ObjectType.Module;
            return free;
        }

        private ClrType CreateArrayType()
        {
            ClrType type = GetTypeByMethodTable(DesktopRuntime.ArrayMethodTable, DesktopRuntime.ObjectMethodTable, 0);
            type.ComponentType = ObjectType;
            return type;
        }

        protected override int GetRuntimeRevision()
        {
            return DesktopRuntime.Revision;
        }

        public override ClrRuntime Runtime
        {
            get
            {
                return DesktopRuntime;
            }
        }

        public override ClrException GetExceptionObject(ulong objRef)
        {
            ClrType type = GetObjectType(objRef);
            if (type == null)
                return null;

            // It's possible for the exception handle to go stale on a dead thread.  In this
            // case we will simply return null if we have a valid object at the address, but
            // that object isn't actually an exception.
            if (!type.IsException)
                return null;

            return new DesktopException(objRef, (BaseDesktopHeapType)type);
        }


        public override ulong GetEEClassByMethodTable(ulong methodTable)
        {
            if (methodTable == 0)
                return 0;

            IMethodTableData mtData = DesktopRuntime.GetMethodTableData(methodTable);
            if (mtData == null)
                return 0;

            return mtData.EEClass;
        }

        public override ulong GetMethodTableByEEClass(ulong eeclass)
        {
            if (eeclass == 0)
                return 0;

            return DesktopRuntime.GetMethodTableByEEClass(eeclass);
        }

        public override bool TryGetMethodTable(ulong obj, out ulong methodTable, out ulong componentMethodTable)
        {
            componentMethodTable = 0;
            if (!ReadPointer(obj, out methodTable))
                return false;

            if (methodTable == DesktopRuntime.ArrayMethodTable)
                if (!ReadPointer(obj + (ulong)(IntPtr.Size * 2), out componentMethodTable))
                    return false;

            return true;
        }

        protected override MemoryReader GetMemoryReaderForAddress(ulong obj)
        {
            if (MemoryReader.Contains(obj))
                return MemoryReader;

            return DesktopRuntime.MemoryReader;
        }


        internal ClrType GetGCHeapTypeFromModuleAndToken(ulong moduleAddr, uint token)
        {
            DesktopModule module = DesktopRuntime.GetModule(moduleAddr);
            ModuleEntry modEnt = new ModuleEntry(module, token);

            if (_typeEntry.TryGetValue(modEnt, out int index))
            {
                BaseDesktopHeapType match = (BaseDesktopHeapType)_types[index];
                if (match.MetadataToken == token)
                    return match;
            }
            else
            {
                // TODO: TypeRefTokens do not work with this code
                foreach (ClrType type in module.EnumerateTypes())
                {
                    if (type.MetadataToken == token)
                        return type;
                }
            }

            return null;
        }

        internal abstract ClrType GetTypeByMethodTable(ulong mt, ulong cmt, ulong obj);


        protected ClrType TryGetComponentType(ulong obj, ulong cmt)
        {
            ClrType result = null;
            IObjectData data = GetObjectData(obj);
            if (data != null)
            {
                if (data.ElementTypeHandle != 0)
                    result = GetTypeByMethodTable(data.ElementTypeHandle, 0, 0);

                if (result == null && data.ElementType != ClrElementType.Unknown)
                    result = GetBasicType(data.ElementType);
            }
            else if (cmt != 0)
            {
                result = GetTypeByMethodTable(cmt, 0);
            }

            return result;
        }

        protected static StringBuilder GetTypeNameFromToken(DesktopModule module, uint token)
        {
            if (module == null)
                return null;

            ICorDebug.IMetadataImport meta = module.GetMetadataImport();
            if (meta == null)
                return null;

            // Get type name.
            StringBuilder typeBuilder = new StringBuilder(256);
            int res = meta.GetTypeDefProps((int)token, typeBuilder, typeBuilder.Capacity, out int typeDefLen, out System.Reflection.TypeAttributes typeAttrs, out int ptkExtends);
            if (res < 0)
                return null;

            res = meta.GetNestedClassProps((int)token, out int enclosing);
            if (res == 0 && token != enclosing)
            {
                StringBuilder inner = GetTypeNameFromToken(module, (uint)enclosing);
                if (inner == null)
                {
                    inner = new StringBuilder(typeBuilder.Capacity + 16);
                    inner.Append("<UNKNOWN>");
                }

                inner.Append('+');
                inner.Append(typeBuilder);
                return inner;
            }

            return typeBuilder;
        }



        public override IEnumerable<ulong> EnumerateFinalizableObjectAddresses()
        {
            if (DesktopRuntime.GetHeaps(out SubHeap[] heaps))
            {
                foreach (SubHeap heap in heaps)
                {
                    foreach (ulong obj in DesktopRuntime.GetPointersInRange(heap.FQLiveStart, heap.FQLiveStop))
                    {
                        if (obj == 0)
                            continue;

                        var type = GetObjectType(obj);
                        if (type != null && !type.IsFinalizeSuppressed(obj))
                            yield return obj;
                    }
                }
            }
        }

        private BlockingObject[] _managedLocks;

        public override IEnumerable<BlockingObject> EnumerateBlockingObjects()
        {
            InitLockInspection();
            return _managedLocks;
        }

        internal void InitLockInspection()
        {
            if (_managedLocks != null)
                return;

            LockInspection li = new LockInspection(this, DesktopRuntime);
            _managedLocks = li.InitLockInspection();
        }

        public override ClrRootStackwalkPolicy StackwalkPolicy
        {
            get => _stackwalkPolicy;
            set
            {
                if (value != _currentStackCache)
                    _stackCache = null;

                _stackwalkPolicy = value;
            }
        }

        private ClrRootStackwalkPolicy _stackwalkPolicy;
        private ClrRootStackwalkPolicy _currentStackCache = ClrRootStackwalkPolicy.SkipStack;
        private Dictionary<ClrThread, ClrRoot[]> _stackCache;
        private ClrHandle[] _strongHandles;
        private Dictionary<ulong, List<ulong>> _dependentHandles;
        
        public override void CacheRoots(CancellationToken cancelToken)
        {
            if (StackwalkPolicy != ClrRootStackwalkPolicy.SkipStack && (_stackCache == null || _currentStackCache != StackwalkPolicy))
            {
                var cache = new Dictionary<ClrThread, ClrRoot[]>();

                bool exactStackwalk = ClrThread.GetExactPolicy(Runtime, StackwalkPolicy);
                foreach (ClrThread thread in Runtime.Threads)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    if (thread.IsAlive)
                        cache.Add(thread, thread.EnumerateStackObjects(!exactStackwalk).ToArray());
                }

                _stackCache = cache;
                _currentStackCache = StackwalkPolicy;
            }

            if (_strongHandles == null)
                CacheStrongHandles(cancelToken);
        }

        private void CacheStrongHandles(CancellationToken cancelToken)
        {
            _strongHandles = EnumerateStrongHandlesWorker(cancelToken).OrderBy(k => GetHandleOrder(k.HandleType)).ToArray();
            Debug.Assert(_dependentHandles != null);
        }

        public override void ClearRootCache()
        {
            _currentStackCache = ClrRootStackwalkPolicy.SkipStack;
            _stackCache = null;
            _strongHandles = null;
            _dependentHandles = null;
        }

        public override bool AreRootsCached => (_stackwalkPolicy == ClrRootStackwalkPolicy.SkipStack || (_stackCache != null && _currentStackCache == StackwalkPolicy)) && _strongHandles != null;

        private static int GetHandleOrder(HandleType handleType)
        {
            switch (handleType)
            {
                case HandleType.AsyncPinned:
                    return 0;

                case HandleType.Pinned:
                    return 1;

                case HandleType.Strong:
                    return 2;

                case HandleType.RefCount:
                    return 3;

                default:
                    return 4;
            }
        }

        internal override IEnumerable<ClrHandle> EnumerateStrongHandles()
        {
            if (_strongHandles != null)
            {
                Debug.Assert(_dependentHandles != null);
                return _strongHandles;
            }

            return EnumerateStrongHandlesWorker(CancellationToken.None);
        }

        internal override void BuildDependentHandleMap(CancellationToken cancelToken)
        {
            if (_dependentHandles != null)
                return;

            _dependentHandles = DesktopRuntime.GetDependentHandleMap(cancelToken);
        }

        private IEnumerable<ClrHandle> EnumerateStrongHandlesWorker(CancellationToken cancelToken)
        {
            Dictionary<ulong, List<ulong>> dependentHandles = null;
            if (_dependentHandles == null)
                dependentHandles = new Dictionary<ulong, List<ulong>>();

            foreach (ClrHandle handle in Runtime.EnumerateHandles())
            {
                cancelToken.ThrowIfCancellationRequested();

                if (handle.Object != 0)
                {
                    switch (handle.HandleType)
                    {
                        case HandleType.RefCount:
                            if (handle.RefCount > 0)
                                yield return handle;

                            break;

                        case HandleType.AsyncPinned:
                        case HandleType.Pinned:
                        case HandleType.SizedRef:
                        case HandleType.Strong:
                            yield return handle;
                            break;

                        case HandleType.Dependent:
                            if (dependentHandles != null)
                            {
                                if (!dependentHandles.TryGetValue(handle.Object, out List<ulong> list))
                                    dependentHandles[handle.Object] = list = new List<ulong>();

                                list.Add(handle.DependentTarget);
                            }

                            break;

                        default:
                            break;
                    }
                }
            }

            if (dependentHandles != null)
                _dependentHandles = dependentHandles;
        }

        internal override IEnumerable<ClrRoot> EnumerateStackRoots()
        {
            if (StackwalkPolicy != ClrRootStackwalkPolicy.SkipStack)
            {
                if (_stackCache != null && _currentStackCache == StackwalkPolicy)
                {
                    return _stackCache.SelectMany(t => t.Value);
                }
                else
                {
                    return EnumerateStackRootsWorker();
                }
            }
            
            return new ClrRoot[0];
        }

        private IEnumerable<ClrRoot> EnumerateStackRootsWorker()
        {
            bool exactStackwalk = ClrThread.GetExactPolicy(Runtime, StackwalkPolicy);
            foreach (ClrThread thread in DesktopRuntime.Threads)
            {
                if (thread.IsAlive)
                {
                    if (exactStackwalk)
                    {
                        foreach (var root in thread.EnumerateStackObjects(false))
                            yield return root;
                    }
                    else
                    {
                        HashSet<ulong> seen = new HashSet<ulong>();
                        foreach (var root in thread.EnumerateStackObjects(true))
                        {
                            if (!seen.Contains(root.Object))
                            {
                                seen.Add(root.Object);
                                yield return root;
                            }
                        }
                    }
                }
            }
        }

        public override IEnumerable<ClrRoot> EnumerateRoots()
        {
            return EnumerateRoots(true);
        }

        public override IEnumerable<ClrRoot> EnumerateRoots(bool enumerateStatics)
        {
            if (enumerateStatics)
            {
                // Statics
                foreach (var type in EnumerateTypes())
                {
                    // Statics
                    foreach (var staticField in type.StaticFields)
                    {
                        if (!ClrRuntime.IsPrimitive(staticField.ElementType))
                        {
                            foreach (var ad in DesktopRuntime.AppDomains)
                            {
                                ulong addr = 0;
                                // We must manually get the value, as strings will not be returned as an object address.
                                try // If this fails for whatever reasion, don't fail completely.  
                                {
                                    addr = staticField.GetAddress(ad);
                                }
                                catch (Exception e)
                                {
                                    Trace.WriteLine(string.Format("Error getting stack field {0}.{1}: {2}", type.Name, staticField.Name, e.Message));
                                    goto NextStatic;
                                }

                                if (DesktopRuntime.ReadPointer(addr, out ulong value) && value != 0)
                                {
                                    ClrType objType = GetObjectType(value);
                                    if (objType != null)
                                        yield return new StaticVarRoot(addr, value, objType, type.Name, staticField.Name, ad);
                                }
                            }
                        }
                    NextStatic:;
                    }

                    // Thread statics
                    foreach (var tsf in type.ThreadStaticFields)
                    {
                        if (ClrRuntime.IsObjectReference(tsf.ElementType))
                        {
                            foreach (var ad in DesktopRuntime.AppDomains)
                            {
                                foreach (var thread in DesktopRuntime.Threads)
                                {
                                    // We must manually get the value, as strings will not be returned as an object address.
                                    ulong addr = tsf.GetAddress(ad, thread);

                                    if (DesktopRuntime.ReadPointer(addr, out ulong value) && value != 0)
                                    {
                                        ClrType objType = GetObjectType(value);
                                        if (objType != null)
                                            yield return new ThreadStaticVarRoot(addr, value, objType, type.Name, tsf.Name, ad);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Handles
            foreach (ClrHandle handle in EnumerateStrongHandles())
            {
                ulong objAddr = handle.Object;
                GCRootKind kind = GCRootKind.Strong;
                if (objAddr != 0)
                {
                    ClrType type = GetObjectType(objAddr);
                    if (type != null)
                    {
                        switch (handle.HandleType)
                        {
                            case HandleType.WeakShort:
                            case HandleType.WeakLong:
                                break;
                            case HandleType.RefCount:
                                if (handle.RefCount <= 0)
                                    break;
                                goto case HandleType.Strong;
                            case HandleType.Pinned:
                                kind = GCRootKind.Pinning;
                                goto case HandleType.Strong;
                            case HandleType.AsyncPinned:
                                kind = GCRootKind.AsyncPinning;
                                goto case HandleType.Strong;
                            case HandleType.Strong:
                            case HandleType.SizedRef:
                                yield return new HandleRoot(handle.Address, objAddr, type, handle.HandleType, kind, handle.AppDomain);

                                // Async pinned handles keep 1 or more "sub objects" alive.  I will report them here as their own pinned handle.
                                if (handle.HandleType == HandleType.AsyncPinned)
                                {
                                    ClrInstanceField userObjectField = type.GetFieldByName("m_userObject");
                                    if (userObjectField != null)
                                    {
                                        ulong _userObjAddr = userObjectField.GetAddress(objAddr);
                                        ulong _userObj = (ulong)userObjectField.GetValue(objAddr);
                                        var _userObjType = GetObjectType(_userObj);
                                        if (_userObjType != null)
                                        {
                                            if (_userObjType.IsArray)
                                            {
                                                if (_userObjType.ComponentType != null)
                                                {
                                                    if (_userObjType.ComponentType.ElementType == ClrElementType.Object)
                                                    {
                                                        // report elements
                                                        int len = _userObjType.GetArrayLength(_userObj);
                                                        for (int i = 0; i < len; ++i)
                                                        {
                                                            ulong indexAddr = _userObjType.GetArrayElementAddress(_userObj, i);
                                                            ulong indexObj = (ulong)_userObjType.GetArrayElementValue(_userObj, i);
                                                            ClrType indexObjType = GetObjectType(indexObj);

                                                            if (indexObj != 0 && indexObjType != null)
                                                                yield return new HandleRoot(indexAddr, indexObj, indexObjType, HandleType.AsyncPinned, GCRootKind.AsyncPinning, handle.AppDomain);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        yield return new HandleRoot(_userObjAddr, _userObj, _userObjType, HandleType.AsyncPinned, GCRootKind.AsyncPinning, handle.AppDomain);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                yield return new HandleRoot(_userObjAddr, _userObj, _userObjType, HandleType.AsyncPinned, GCRootKind.AsyncPinning, handle.AppDomain);
                                            }
                                        }
                                    }
                                }

                                break;
                            default:
                                Debug.WriteLine("Warning, unknown handle type {0} ignored", Enum.GetName(typeof(HandleType), handle.HandleType));
                                break;
                        }
                    }
                }
            }
            
            // Finalization Queue
            foreach (ulong objAddr in DesktopRuntime.EnumerateFinalizerQueueObjectAddresses())
                if (objAddr != 0)
                {
                    ClrType type = GetObjectType(objAddr);
                    if (type != null)
                        yield return new FinalizerRoot(objAddr, type);
                }

            // Threads
            foreach (ClrRoot root in EnumerateStackRoots())
                yield return root;
        }

        internal string GetStringContents(ulong strAddr)
        {
            if (strAddr == 0)
                return null;

            if (!_initializedStringFields)
            {
                _firstChar = StringType.GetFieldByName("m_firstChar");
                _stringLength = StringType.GetFieldByName("m_stringLength");

                // .Type being null can happen in minidumps.  In that case we will fall back to
                // hardcoded values and hope they don't get out of date.
                if (_firstChar?.Type == ErrorType)
                    _firstChar = null;

                if (_stringLength?.Type == ErrorType)
                    _stringLength = null;

                _initializedStringFields = true;
            }

            int length = 0;
            if (_stringLength != null)
                length = (int)_stringLength.GetValue(strAddr);
            else if (!DesktopRuntime.ReadDword(strAddr + DesktopRuntime.GetStringLengthOffset(), out length))
                return null;


            if (length == 0)
                return "";

            ulong data = 0;
            if (_firstChar != null)
                data = _firstChar.GetAddress(strAddr);
            else
                data = strAddr + DesktopRuntime.GetStringFirstCharOffset();

            byte[] buffer = new byte[length * 2];
            if (!DesktopRuntime.ReadMemory(data, buffer, buffer.Length, out int read))
                return null;

            return UnicodeEncoding.Unicode.GetString(buffer);
        }

        public override int ReadMemory(ulong address, byte[] buffer, int offset, int count)
        {
            if (offset != 0)
                throw new NotImplementedException("Non-zero offsets not supported (yet)");

            if (!DesktopRuntime.ReadMemory(address, buffer, count, out int bytesRead))
                return 0;

            return bytesRead;
        }

        public override IEnumerable<ClrType> EnumerateTypes()
        {
            LoadAllTypes();

            for (int i = 0; i < _types.Count; ++i)
                yield return _types[i];
        }


        internal bool TypesLoaded { get { return _loadedTypes; } }

        internal void LoadAllTypes()
        {
            if (_loadedTypes)
                return;

            _loadedTypes = true;

            // Walking a module is sloooow.  Ensure we only walk each module once.
            HashSet<ulong> modules = new HashSet<ulong>();

            foreach (ulong module in DesktopRuntime.EnumerateModules(DesktopRuntime.GetAppDomainData(DesktopRuntime.SystemDomainAddress)))
                modules.Add(module);

            foreach (ulong module in DesktopRuntime.EnumerateModules(DesktopRuntime.GetAppDomainData(DesktopRuntime.SharedDomainAddress)))
                modules.Add(module);

            IAppDomainStoreData ads = DesktopRuntime.GetAppDomainStoreData();
            if (ads == null)
                return;

            IList<ulong> appDomains = DesktopRuntime.GetAppDomainList(ads.Count);
            if (appDomains == null)
                return;

            foreach (ulong ad in appDomains)
            {
                var adData = DesktopRuntime.GetAppDomainData(ad);
                if (adData != null)
                {
                    foreach (ulong module in DesktopRuntime.EnumerateModules(adData))
                        modules.Add(module);
                }
            }

            ulong arrayMt = DesktopRuntime.ArrayMethodTable;
            foreach (var module in modules)
            {
                var mtList = DesktopRuntime.GetMethodTableList(module);
                if (mtList != null)
                {
                    foreach (var pair in mtList)
                    {
                        if (pair.MethodTable != arrayMt)
                        {
                            // prefetch element type, as this also can load types
                            var type = GetTypeByMethodTable(pair.MethodTable, 0, 0);
                            if (type != null)
                            {
                                ClrElementType cet = type.ElementType;
                            }
                        }
                    }
                }
            }
        }

        internal bool GetObjectHeader(ulong obj, out uint value)
        {
            return MemoryReader.TryReadDword(obj - 4, out value);
        }

        internal IObjectData GetObjectData(ulong address)
        {
            return DesktopRuntime.GetObjectData(address);
        }

        internal object GetValueAtAddress(ClrElementType cet, ulong addr)
        {
            switch (cet)
            {
                case ClrElementType.String:
                    return GetStringContents(addr);

                case ClrElementType.Class:
                case ClrElementType.Array:
                case ClrElementType.SZArray:
                case ClrElementType.Object:
                    {
                        if (!MemoryReader.TryReadPtr(addr, out ulong val))
                            return null;

                        return val;
                    }

                case ClrElementType.Boolean:
                    {
                        if (!DesktopRuntime.ReadByte(addr, out byte val))
                            return null;
                        return val != 0;
                    }

                case ClrElementType.Int32:
                    {
                        if (!DesktopRuntime.ReadDword(addr, out int val))
                            return null;

                        return val;
                    }

                case ClrElementType.UInt32:
                    {
                        if (!DesktopRuntime.ReadDword(addr, out uint val))
                            return null;

                        return val;
                    }

                case ClrElementType.Int64:
                    {
                        if (!DesktopRuntime.ReadQword(addr, out long val))
                            return long.MaxValue;

                        return val;
                    }

                case ClrElementType.UInt64:
                    {
                        if (!DesktopRuntime.ReadQword(addr, out ulong val))
                            return long.MaxValue;

                        return val;
                    }

                case ClrElementType.NativeUInt:  // native unsigned int
                case ClrElementType.Pointer:
                case ClrElementType.FunctionPointer:
                    {
                        if (!MemoryReader.TryReadPtr(addr, out ulong val))
                            return null;

                        return val;
                    }

                case ClrElementType.NativeInt:  // native int
                    {
                        if (!MemoryReader.TryReadPtr(addr, out ulong val))
                            return null;

                        return (long)val;
                    }

                case ClrElementType.Int8:
                    {
                        if (!DesktopRuntime.ReadByte(addr, out sbyte val))
                            return null;
                        return val;
                    }

                case ClrElementType.UInt8:
                    {
                        if (!DesktopRuntime.ReadByte(addr, out byte val))
                            return null;
                        return val;
                    }

                case ClrElementType.Float:
                    {
                        if (!DesktopRuntime.ReadFloat(addr, out float val))
                            return null;
                        return val;
                    }

                case ClrElementType.Double: // double
                    {
                        if (!DesktopRuntime.ReadFloat(addr, out double val))
                            return null;
                        return val;
                    }

                case ClrElementType.Int16:
                    {
                        if (!DesktopRuntime.ReadShort(addr, out short val))
                            return null;
                        return val;
                    }

                case ClrElementType.Char:  // u2
                    {
                        if (!DesktopRuntime.ReadShort(addr, out ushort val))
                            return null;
                        return (char)val;
                    }

                case ClrElementType.UInt16:
                    {
                        if (!DesktopRuntime.ReadShort(addr, out ushort val))
                            return null;
                        return val;
                    }
            }

            throw new Exception("Unexpected element type.");
        }

        internal ClrElementType GetElementType(BaseDesktopHeapType type, int depth)
        {
            // Max recursion.
            if (depth >= 32)
                return ClrElementType.Object;

            if (type == ObjectType)
                return ClrElementType.Object;
            else if (type == StringType)
                return ClrElementType.String;
            else if (type.ElementSize > 0)
                return ClrElementType.SZArray;

            BaseDesktopHeapType baseType = (BaseDesktopHeapType)type.BaseType;
            if (baseType == null || baseType == ObjectType)
                return ClrElementType.Object;

            bool vc = false;
            if (ValueType == null)
            {
                if (baseType.Name == "System.ValueType")
                {
                    ValueType = baseType;
                    vc = true;
                }
            }
            else if (baseType == ValueType)
            {
                vc = true;
            }

            if (!vc)
            {
                ClrElementType et = baseType.ElementType;
                if (et == ClrElementType.Unknown)
                {
                    et = GetElementType(baseType, depth + 1);
                    baseType.ElementType = et;
                }

                return et;
            }

            switch (type.Name)
            {
                case "System.Int32":
                    return ClrElementType.Int32;
                case "System.Int16":
                    return ClrElementType.Int16;
                case "System.Int64":
                    return ClrElementType.Int64;
                case "System.IntPtr":
                    return ClrElementType.NativeInt;
                case "System.UInt16":
                    return ClrElementType.UInt16;
                case "System.UInt32":
                    return ClrElementType.UInt32;
                case "System.UInt64":
                    return ClrElementType.UInt64;
                case "System.UIntPtr":
                    return ClrElementType.NativeUInt;
                case "System.Boolean":
                    return ClrElementType.Boolean;
                case "System.Single":
                    return ClrElementType.Float;
                case "System.Double":
                    return ClrElementType.Double;
                case "System.Byte":
                    return ClrElementType.UInt8;
                case "System.Char":
                    return ClrElementType.Char;
                case "System.SByte":
                    return ClrElementType.Int8;
                case "System.Enum":
                    return ClrElementType.Int32;
            }

            return ClrElementType.Struct;
        }

        
        protected List<ClrType> _types;
        protected Dictionary<ModuleEntry, int> _typeEntry = new Dictionary<ModuleEntry, int>(new ModuleEntryCompare());
        private Dictionary<ArrayRankHandle, BaseDesktopHeapType> _arrayTypes;

        private ClrInstanceField _firstChar, _stringLength;
        private bool _initializedStringFields = false;
        private ClrType[] _basicTypes;
        private bool _loadedTypes = false;

        internal readonly ClrInterface[] EmptyInterfaceList = new ClrInterface[0];
        internal Dictionary<string, ClrInterface> Interfaces = new Dictionary<string, ClrInterface>();
        private Lazy<ClrType> _arrayType,  _exceptionType;
        private ClrType _free;

        private DictionaryList _objectMap;
        private ExtendedArray<ObjectInfo> _objects;
        private ExtendedArray<ulong> _gcRefs;

        internal DesktopRuntimeBase DesktopRuntime { get; private set; }
        internal BaseDesktopHeapType ErrorType { get; private set; }
        internal ClrType ObjectType { get; private set; }
        internal ClrType StringType { get; private set; }
        internal ClrType ValueType { get; private set; }
        internal ClrType ArrayType => _arrayType.Value;
        public override ClrType Free => _free;

        internal ClrType ExceptionType => _exceptionType.Value;
        internal ClrType EnumType { get; set; }

        private class ModuleEntryCompare : IEqualityComparer<ModuleEntry>
        {
            public bool Equals(ModuleEntry mx, ModuleEntry my)
            {
                return mx.Token == my.Token && mx.Module == my.Module;
            }

            public int GetHashCode(ModuleEntry obj)
            {
                return (int)obj.Token;
            }
        }

        internal ClrType GetBasicType(ClrElementType elType)
        {
            // Early out without having to construct the array.
            if (_basicTypes == null)
            {
                switch (elType)
                {
                    case ClrElementType.String:
                        return StringType;

                    case ClrElementType.Array:
                    case ClrElementType.SZArray:
                        return ArrayType;

                    case ClrElementType.Object:
                    case ClrElementType.Class:
                        return ObjectType;

                    case ClrElementType.Struct:
                        if (ValueType != null)
                            return ValueType;
                        break;
                }
            }

            if (_basicTypes == null)
                InitBasicTypes();

            if (_basicTypes[(int)elType] == null && this.DesktopRuntime.DataReader.IsMinidump)
            {
                switch (elType)
                {
                    case ClrElementType.Boolean:
                    case ClrElementType.Char:
                    case ClrElementType.Double:
                    case ClrElementType.Float:
                    case ClrElementType.Pointer:
                    case ClrElementType.NativeInt:
                    case ClrElementType.FunctionPointer:
                    case ClrElementType.NativeUInt:
                    case ClrElementType.Int16:
                    case ClrElementType.Int32:
                    case ClrElementType.Int64:
                    case ClrElementType.Int8:
                    case ClrElementType.UInt16:
                    case ClrElementType.UInt32:
                    case ClrElementType.UInt64:
                    case ClrElementType.UInt8:
                        _basicTypes[(int)elType] = new PrimitiveType(this, elType);
                        break;
                }
            }
            return _basicTypes[(int)elType]; ;
        }

        private void InitBasicTypes()
        {
            const int max = (int)ClrElementType.SZArray + 1;

            _basicTypes = new ClrType[max];
            _basicTypes[(int)ClrElementType.Unknown] = null;  // ???
            _basicTypes[(int)ClrElementType.String] = StringType;
            _basicTypes[(int)ClrElementType.Array] = ArrayType;
            _basicTypes[(int)ClrElementType.SZArray] = ArrayType;
            _basicTypes[(int)ClrElementType.Object] = ObjectType;
            _basicTypes[(int)ClrElementType.Class] = ObjectType;

            ClrModule mscorlib = DesktopRuntime.Mscorlib;
            if (mscorlib == null)
                return;

            int count = 0;
            foreach (ClrType type in mscorlib.EnumerateTypes())
            {
                if (count == 14)
                    break;

                switch (type.Name)
                {
                    case "System.ValueType":
                        Debug.Assert(_basicTypes[(int)ClrElementType.Struct] == null);
                        _basicTypes[(int)ClrElementType.Struct] = type;
                        count++;
                        break;

                    case "System.Boolean":
                        Debug.Assert(_basicTypes[(int)ClrElementType.Boolean] == null);
                        _basicTypes[(int)ClrElementType.Boolean] = type;
                        count++;
                        break;

                    case "System.Char":
                        Debug.Assert(_basicTypes[(int)ClrElementType.Char] == null);
                        _basicTypes[(int)ClrElementType.Char] = type;
                        count++;
                        break;

                    case "System.SByte":
                        Debug.Assert(_basicTypes[(int)ClrElementType.Int8] == null);
                        _basicTypes[(int)ClrElementType.Int8] = type;
                        count++;
                        break;

                    case "System.Byte":
                        Debug.Assert(_basicTypes[(int)ClrElementType.UInt8] == null);
                        _basicTypes[(int)ClrElementType.UInt8] = type;
                        count++;
                        break;

                    case "System.Int16":
                        Debug.Assert(_basicTypes[(int)ClrElementType.Int16] == null);
                        _basicTypes[(int)ClrElementType.Int16] = type;
                        count++;
                        break;

                    case "System.UInt16":
                        Debug.Assert(_basicTypes[(int)ClrElementType.UInt16] == null);
                        _basicTypes[(int)ClrElementType.UInt16] = type;
                        count++;
                        break;

                    case "System.Int32":
                        Debug.Assert(_basicTypes[(int)ClrElementType.Int32] == null);
                        _basicTypes[(int)ClrElementType.Int32] = type;
                        count++;
                        break;

                    case "System.UInt32":
                        Debug.Assert(_basicTypes[(int)ClrElementType.UInt32] == null);
                        _basicTypes[(int)ClrElementType.UInt32] = type;
                        count++;
                        break;

                    case "System.Int64":
                        Debug.Assert(_basicTypes[(int)ClrElementType.Int64] == null);
                        _basicTypes[(int)ClrElementType.Int64] = type;
                        count++;
                        break;

                    case "System.UInt64":
                        Debug.Assert(_basicTypes[(int)ClrElementType.UInt64] == null);
                        _basicTypes[(int)ClrElementType.UInt64] = type;
                        count++;
                        break;

                    case "System.Single":
                        Debug.Assert(_basicTypes[(int)ClrElementType.Float] == null);
                        _basicTypes[(int)ClrElementType.Float] = type;
                        count++;
                        break;

                    case "System.Double":
                        Debug.Assert(_basicTypes[(int)ClrElementType.Double] == null);
                        _basicTypes[(int)ClrElementType.Double] = type;
                        count++;
                        break;

                    case "System.IntPtr":
                        Debug.Assert(_basicTypes[(int)ClrElementType.NativeInt] == null);
                        _basicTypes[(int)ClrElementType.NativeInt] = type;
                        count++;
                        break;

                    case "System.UIntPtr":
                        Debug.Assert(_basicTypes[(int)ClrElementType.NativeUInt] == null);
                        _basicTypes[(int)ClrElementType.NativeUInt] = type;
                        count++;
                        break;
                }
            }

            Debug.Assert(DesktopRuntime.DataReader.IsMinidump || count == 14);           

        }

        internal BaseDesktopHeapType CreatePointerType(BaseDesktopHeapType innerType, ClrElementType clrElementType, string nameHint)
        {
            return new DesktopPointerType(this, (DesktopBaseModule)DesktopRuntime.Mscorlib, clrElementType, 0, nameHint) { ComponentType = innerType };
        }

        internal BaseDesktopHeapType GetArrayType(ClrElementType clrElementType, int ranks, string nameHint)
        {
            if (_arrayTypes == null)
                _arrayTypes = new Dictionary<ArrayRankHandle, BaseDesktopHeapType>();

            var handle = new ArrayRankHandle(clrElementType, ranks);
            if (!_arrayTypes.TryGetValue(handle, out BaseDesktopHeapType result))
                _arrayTypes[handle] = result = new DesktopArrayType(this, (DesktopBaseModule)DesktopRuntime.Mscorlib, clrElementType, ranks, ArrayType.MetadataToken, nameHint);

            return result;
        }

        internal override long TotalObjects => _objects?.Count ?? -1;

        public override bool IsHeapCached => _objectMap != null;

        public override void ClearHeapCache()
        {
            _objectMap = null;
            _objects = null;
            _gcRefs = null;
        }

        public override void CacheHeap(CancellationToken cancelToken)
        {
            // TODO
            Action<long, long> progressReport = null;

            DictionaryList objmap = new DictionaryList();
            ExtendedArray<ulong> gcrefs = new ExtendedArray<ulong>();
            ExtendedArray<ObjectInfo> objInfo = new ExtendedArray<ObjectInfo>();

            long totalBytes = Segments.Sum(s => (long)s.Length);
            long completed = 0;

            uint pointerSize = (uint)PointerSize;

            foreach (ClrSegment seg in Segments)
            {
                progressReport?.Invoke(completed, totalBytes);
                
                for (ulong obj = seg.FirstObject; obj < seg.End && obj != 0; obj = seg.NextObject(obj))
                {
                    cancelToken.ThrowIfCancellationRequested();
                    
                    // We may
                    ClrType type = GetObjectType(obj);
                    if (type == null || GCRoot.IsTooLarge(obj, type, seg))
                    {
                        AddObject(objmap, gcrefs, objInfo, obj, Free);
                        do
                        {
                            cancelToken.ThrowIfCancellationRequested();

                            obj += pointerSize;
                            if (obj >= seg.End)
                                break;

                            type = GetObjectType(obj);
                        } while (type == null);

                        if (obj >= seg.End)
                            break;
                    }
                    
                    AddObject(objmap, gcrefs, objInfo, obj, type);
                }

                completed += (long)seg.Length;
            }

            progressReport?.Invoke(totalBytes, totalBytes);

            _objectMap = objmap;
            _gcRefs = gcrefs;
            _objects = objInfo;
        }

        public override IEnumerable<ClrObject> EnumerateObjects()
        {
            if (Revision != GetRuntimeRevision())
                ClrDiagnosticsException.ThrowRevisionError(Revision, GetRuntimeRevision());

            if (IsHeapCached)
                return _objectMap.Enumerate().Select(item => ClrObject.Create(item.Key, _objects[item.Value].Type));
            
            return base.EnumerateObjects();
        }

        public override IEnumerable<ulong> EnumerateObjectAddresses()
        {
            if (Revision != GetRuntimeRevision())
                ClrDiagnosticsException.ThrowRevisionError(Revision, GetRuntimeRevision());

            if (IsHeapCached)
                return _objectMap.Enumerate().Select(item => item.Key);

            return base.EnumerateObjectAddresses();
        }

        public override ClrType GetObjectType(ulong objRef)
        {
            Debug.Assert(IsHeapCached);

            if (!_objectMap.TryGetValue(objRef, out int index))
                return null;

            return _objects[index].Type;
        }

        internal override void EnumerateObjectReferences(ulong obj, ClrType type, bool carefully, Action<ulong, int> callback)
        {
            if (IsHeapCached)
            {
                if (type.ContainsPointers && _objectMap.TryGetValue(obj, out int index))
                {
                    uint count = _objects[index].RefCount;
                    uint offset = _objects[index].RefOffset;


                    for (uint i = offset; i < offset + count; i++)
                        callback(_gcRefs[i], 0);
                }
            }
            else
            {
                base.EnumerateObjectReferences(obj, type, carefully, callback);
            }

            if (_dependentHandles == null)
                BuildDependentHandleMap(CancellationToken.None);

            Debug.Assert(_dependentHandles != null);
            if (_dependentHandles.TryGetValue(obj, out List<ulong> value))
                foreach (ulong item in value)
                    callback(item, -1);
        }

        internal override IEnumerable<ClrObject> EnumerateObjectReferences(ulong obj, ClrType type, bool carefully)
        {
            IEnumerable<ClrObject> result = null;
            if (IsHeapCached)
            {
                if (type.ContainsPointers && _objectMap.TryGetValue(obj, out int index))
                {
                    uint count = _objects[index].RefCount;
                    uint offset = _objects[index].RefOffset;

                    result = EnumerateRefs(offset, count);
                }
                else
                {
                    result = s_emptyObjectSet;
                }
            }
            else
            {
                result = base.EnumerateObjectReferences(obj, type, carefully);
            }

            if (_dependentHandles == null)
                BuildDependentHandleMap(CancellationToken.None);

            Debug.Assert(_dependentHandles != null);
            if (_dependentHandles.TryGetValue(obj, out List<ulong> values))
                result = result.Union(values.Select(v => ClrObject.Create(v, GetObjectType(v))));

            return result;
        }

        private IEnumerable<ClrObject> EnumerateRefs(uint offset, uint count)
        {
            for (uint i = offset; i < offset + count; i++)
            {
                ulong obj = _gcRefs[i];
                yield return ClrObject.Create(obj, GetObjectType(obj));
            }
        }

        private void AddObject(DictionaryList objmap, ExtendedArray<ulong> gcrefs, ExtendedArray<ObjectInfo> objInfo, ulong obj, ClrType type)
        {
            uint offset = (uint)gcrefs.Count;
            
            if (type.ContainsPointers)
            {
                EnumerateObjectReferences(obj, type, true, (addr, offs) =>
                {
                    gcrefs.Add(addr);
                });
            }
            
            uint refCount = (uint)gcrefs.Count - offset;
            objmap.Add(obj, checked((int)objInfo.Count));
            objInfo.Add(new ObjectInfo()
            {
                Type = type,
                RefOffset = refCount != 0 ? offset : uint.MaxValue,
                RefCount = refCount
            });
        }



        protected string GetTypeName(ulong mt, DesktopModule module, uint token)
        {
            string typeName = DesktopRuntime.GetNameForMT(mt);
            return GetBetterTypeName(typeName, module, token);
        }
        
        protected string GetTypeName(TypeHandle hnd, DesktopModule module, uint token)
        {
            string typeName = DesktopRuntime.GetTypeName(hnd);
            return GetBetterTypeName(typeName, module, token);
        }

        private static string GetBetterTypeName(string typeName, DesktopModule module, uint token)
        {
            if (typeName == null || typeName == "<Unloaded Type>")
            {
                var builder = GetTypeNameFromToken(module, token);
                string newName = builder?.ToString();
                if (newName != null && newName != "<UNKNOWN>")
                    typeName = newName;
            }
            else
            {
                typeName = DesktopHeapType.FixGenerics(typeName);
            }

            return typeName;
        }

        struct ObjectInfo
        {
            public ClrType Type;
            public uint RefOffset;
            public uint RefCount;

            public override string ToString()
            {
                return $"{Type.Name} refs: {RefCount:n0}";
            }
        }

        #region Large array/dictionary helpers
        internal class ExtendedArray<T>
        {
            private const int Initial = 0x100000; // 1 million-ish
            private const int Secondary = 0x1000000;
            private const int Complete = 0x4000000;

            private List<T[]> _lists = new List<T[]>();
            private int _curr = 0;

            public long Count
            {
                get
                {
                    if (_lists.Count <= 0)
                        return 0;

                    long total = (_lists.Count - 1) * (long)Complete;
                    total += _curr;
                    return total;
                }
            }

            public T this[int index]
            {
                get
                {
                    int arrayIndex = index / Complete;
                    index %= Complete;

                    return _lists[arrayIndex][index];
                }
                set
                {
                    int arrayIndex = index / Complete;
                    index %= Complete;

                    _lists[arrayIndex][index] = value;
                }
            }


            public T this[long index]
            {
                get
                {
                    long arrayIndex = index / Complete;
                    index %= Complete;

                    return _lists[(int)arrayIndex][index];
                }
                set
                {
                    long arrayIndex = index / Complete;
                    index %= Complete;

                    _lists[(int)arrayIndex][index] = value;
                }
            }

            public void Add(T t)
            {
                T[] arr = _lists.LastOrDefault();
                if (arr == null || _curr == Complete)
                {
                    arr = new T[Initial];
                    _lists.Add(arr);
                    _curr = 0;
                }

                if (_curr >= arr.Length)
                {
                    if (arr.Length == Complete)
                    {
                        arr = new T[Initial];
                        _lists.Add(arr);
                        _curr = 0;
                    }
                    else
                    {
                        int newSize = arr.Length == Initial ? Secondary : Complete;

                        _lists.RemoveAt(_lists.Count - 1);
                        Array.Resize(ref arr, newSize);
                        _lists.Add(arr);
                    }
                }

                arr[_curr++] = t;
            }

            public void Condense()
            {
                T[] arr = _lists.LastOrDefault();
                if (arr != null && _curr < arr.Length)
                {
                    _lists.RemoveAt(_lists.Count - 1);
                    Array.Resize(ref arr, _curr);
                    _lists.Add(arr);
                }
            }
        }

        internal class DictionaryList
        {
            private const int MaxEntries = 40000000;
            private List<Entry> _entries = new List<Entry>();

            public IEnumerable<KeyValuePair<ulong,int>> Enumerate()
            {
                return _entries.SelectMany(e => e.Dictionary);
            }

            public void Add(ulong obj, int index)
            {
                Entry curr = GetOrCreateEntry(obj);
                curr.End = obj;
                curr.Dictionary.Add(obj, index);
            }

            public bool TryGetValue(ulong obj, out int index)
            {
                foreach (Entry entry in _entries)
                    if (entry.Start <= obj && obj <= entry.End)
                        return entry.Dictionary.TryGetValue(obj, out index);

                index = 0;
                return false;
            }

            private Entry GetOrCreateEntry(ulong obj)
            {
                if (_entries.Count == 0)
                {
                    return NewEntry(obj);
                }
                else
                {
                    Entry last = _entries.Last();
                    if (last.Dictionary.Count > MaxEntries)
                        return NewEntry(obj);

                    return last;
                }
            }

            private Entry NewEntry(ulong obj)
            {
                Entry result = new Entry() { Start = obj, End = obj, Dictionary = new SortedDictionary<ulong, int>() };
                _entries.Add(result);
                return result;
            }

            class Entry
            {
                public ulong Start;
                public ulong End;
                public SortedDictionary<ulong, int> Dictionary;
            }
        }
        #endregion
    }


    internal class FinalizerRoot : ClrRoot
    {
        private ClrType _type;
        public FinalizerRoot(ulong obj, ClrType type)
        {
            Object = obj;
            _type = type;
        }

        public override GCRootKind Kind
        {
            get { return GCRootKind.Finalizer; }
        }

        public override string Name
        {
            get
            {
                return "finalization handle";
            }
        }

        public override ClrType Type
        {
            get { return _type; }
        }
    }

    internal class HandleRoot : ClrRoot
    {
        private GCRootKind _kind;
        private string _name;
        private ClrType _type;
        private ClrAppDomain _domain;

        public HandleRoot(ulong addr, ulong obj, ClrType type, HandleType hndType, GCRootKind kind, ClrAppDomain domain)
        {
            _name = Enum.GetName(typeof(HandleType), hndType) + " handle";
            Address = addr;
            Object = obj;
            _kind = kind;
            _type = type;
            _domain = domain;
        }

        public override ClrAppDomain AppDomain
        {
            get
            {
                return _domain;
            }
        }

        public override bool IsPinned
        {
            get
            {
                return Kind == GCRootKind.Pinning || Kind == GCRootKind.AsyncPinning;
            }
        }

        public override GCRootKind Kind
        {
            get { return _kind; }
        }

        public override string Name
        {
            get
            {
                return _name;
            }
        }

        public override ClrType Type
        {
            get { return _type; }
        }
    }

    internal class StaticVarRoot : ClrRoot
    {
        private string _name;
        private ClrAppDomain _domain;
        private ClrType _type;

        public StaticVarRoot(ulong addr, ulong obj, ClrType type, string typeName, string variableName, ClrAppDomain appDomain)
        {
            Address = addr;
            Object = obj;
            _name = string.Format("static var {0}.{1}", typeName, variableName);
            _domain = appDomain;
            _type = type;
        }

        public override ClrAppDomain AppDomain
        {
            get
            {
                return _domain;
            }
        }

        public override GCRootKind Kind
        {
            get { return GCRootKind.StaticVar; }
        }

        public override string Name
        {
            get
            {
                return _name;
            }
        }

        public override ClrType Type
        {
            get { return _type; }
        }
    }

    internal class ThreadStaticVarRoot : ClrRoot
    {
        private string _name;
        private ClrAppDomain _domain;
        private ClrType _type;

        public ThreadStaticVarRoot(ulong addr, ulong obj, ClrType type, string typeName, string variableName, ClrAppDomain appDomain)
        {
            Address = addr;
            Object = obj;
            _name = string.Format("thread static var {0}.{1}", typeName, variableName);
            _domain = appDomain;
            _type = type;
        }

        public override ClrAppDomain AppDomain
        {
            get
            {
                return _domain;
            }
        }

        public override GCRootKind Kind
        {
            get { return GCRootKind.ThreadStaticVar; }
        }

        public override string Name
        {
            get
            {
                return _name;
            }
        }

        public override ClrType Type
        {
            get { return _type; }
        }
    }

    internal class DesktopBlockingObject : BlockingObject
    {
        private ulong _obj;
        private bool _locked;
        private int _recursion;
        private IList<ClrThread> _waiters;
        private BlockingReason _reason;
        private ClrThread[] _owners;

        private static readonly ClrThread[] s_emptyWaiters = new ClrThread[0];

        internal void SetOwners(ClrThread[] owners)
        {
            _owners = owners;
        }

        internal void SetOwner(ClrThread owner)
        {
            _owners = new ClrThread[0];
            _owners[0] = owner;
        }

        public DesktopBlockingObject(ulong obj, bool locked, int recursion, ClrThread owner, BlockingReason reason)
        {
            _obj = obj;
            _locked = locked;
            _recursion = recursion;
            _reason = reason;
            _owners = new ClrThread[1];
            _owners[0] = owner;
        }

        public DesktopBlockingObject(ulong obj, bool locked, int recursion, BlockingReason reason, ClrThread[] owners)
        {
            _obj = obj;
            _locked = locked;
            _recursion = recursion;
            _reason = reason;
            _owners = owners;
        }

        public DesktopBlockingObject(ulong obj, bool locked, int recursion, BlockingReason reason)
        {
            _obj = obj;
            _locked = locked;
            _recursion = recursion;
            _reason = reason;
        }

        public override ulong Object
        {
            get { return _obj; }
        }

        public override bool Taken
        {
            get { return _locked; }
        }

        public void SetTaken(bool status)
        {
            _locked = status;
        }

        public override int RecursionCount
        {
            get { return _recursion; }
        }


        public override IList<ClrThread> Waiters
        {
            get
            {
                if (_waiters == null)
                    return s_emptyWaiters;

                return _waiters;
            }
        }

        internal void AddWaiter(ClrThread thread)
        {
            if (thread == null)
                return;

            if (_waiters == null)
                _waiters = new List<ClrThread>();

            _waiters.Add(thread);
            _locked = true;
        }

        public override BlockingReason Reason
        {
            get { return _reason; }
            internal set { _reason = value; }
        }

        public override ClrThread Owner
        {
            get
            {
                if (!HasSingleOwner)
                    throw new InvalidOperationException("BlockingObject has more than one owner.");

                return _owners[0];
            }
        }

        public override bool HasSingleOwner
        {
            get { return _owners.Length == 1; }
        }

        public override IList<ClrThread> Owners
        {
            get
            {
                return _owners ?? new ClrThread[0];
            }
        }
    }

    internal class DesktopException : ClrException
    {
        public DesktopException(ulong objRef, BaseDesktopHeapType type)
        {
            _object = objRef;
            _type = type;
        }

        public override ClrType Type
        {
            get { return _type; }
        }

        public override string Message
        {
            get
            {
                var field = _type.GetFieldByName("_message");
                if (field != null)
                    return (string)field.GetValue(_object);

                var runtime = _type.DesktopHeap.DesktopRuntime;
                uint offset = runtime.GetExceptionMessageOffset();
                Debug.Assert(offset > 0);

                ulong message = _object + offset;
                if (!runtime.ReadPointer(message, out message))
                    return null;

                return _type.DesktopHeap.GetStringContents(message);
            }
        }

        public override ulong Address
        {
            get { return _object; }
        }

        public override ClrException Inner
        {
            get
            {
                // TODO:  This needs to get the field offset by runtime instead.
                var field = _type.GetFieldByName("_innerException");
                if (field == null)
                    return null;
                object inner = field.GetValue(_object);
                if (inner == null || !(inner is ulong) || ((ulong)inner == 0))
                    return null;

                ulong ex = (ulong)inner;
                BaseDesktopHeapType type = (BaseDesktopHeapType)_type.DesktopHeap.GetObjectType(ex);

                return new DesktopException(ex, type);
            }
        }

        public override IList<ClrStackFrame> StackTrace
        {
            get
            {
                if (_stackTrace == null)
                    _stackTrace = _type.DesktopHeap.DesktopRuntime.GetExceptionStackTrace(_object, _type);

                return _stackTrace;
            }
        }

        public override int HResult
        {
            get
            {
                var field = _type.GetFieldByName("_HResult");
                if (field != null)
                    return (int)field.GetValue(_object);

                var runtime = _type.DesktopHeap.DesktopRuntime;
                uint offset = runtime.GetExceptionHROffset();
                runtime.ReadDword(_object + offset, out int hr);

                return hr;
            }
        }

        #region Private
        private ulong _object;
        private BaseDesktopHeapType _type;
        private IList<ClrStackFrame> _stackTrace;
        #endregion
    }

    internal struct ArrayRankHandle : IEquatable<ArrayRankHandle>
    {
        private ClrElementType _type;
        private int _ranks;

        public ArrayRankHandle(ClrElementType eltype, int ranks)
        {
            _type = eltype;
            _ranks = ranks;
        }

        public bool Equals(ArrayRankHandle other)
        {
            return _type == other._type && _ranks == other._ranks;
        }
    }

    internal struct TypeHandle : IEquatable<TypeHandle>
    {
        public ulong MethodTable;
        public ulong ComponentMethodTable;

        #region Constructors
        public TypeHandle(ulong mt)
        {
            MethodTable = mt;
            ComponentMethodTable = 0;
        }

        public TypeHandle(ulong mt, ulong cmt)
        {
            MethodTable = mt;
            ComponentMethodTable = cmt;
        }
        #endregion

        public override int GetHashCode()
        {
            return ((int)MethodTable + (int)ComponentMethodTable) >> 3;
        }

        bool IEquatable<TypeHandle>.Equals(TypeHandle other)
        {
            return (MethodTable == other.MethodTable) && (ComponentMethodTable == other.ComponentMethodTable);
        }

        #region Compare Helpers
        // TODO should not be needed.   IEquatable should cover it.  
        public static IEqualityComparer<TypeHandle> EqualityComparer = new HeapTypeEqualityComparer();
        private class HeapTypeEqualityComparer : IEqualityComparer<TypeHandle>
        {
            public bool Equals(TypeHandle x, TypeHandle y)
            {
                return (x.MethodTable == y.MethodTable) && (x.ComponentMethodTable == y.ComponentMethodTable);
            }
            public int GetHashCode(TypeHandle obj)
            {
                return ((int)obj.MethodTable + (int)obj.ComponentMethodTable) >> 3;
            }
        }
        #endregion
    }

    struct ModuleEntry
    {
        public ClrModule Module;
        public uint Token;
        public ModuleEntry(ClrModule module, uint token)
        {
            Module = module;
            Token = token;
        }
    }

    internal class LegacyGCHeap : DesktopGCHeap
    {
        private ClrObject _lastObject = new ClrObject();
        private Dictionary<TypeHandle, int> _indices = new Dictionary<TypeHandle, int>(TypeHandle.EqualityComparer);

        public LegacyGCHeap(DesktopRuntimeBase runtime)
            : base(runtime)
        {
        }

        public override bool HasComponentMethodTables
        {
            get
            {
                return true;
            }
        }

        public override ClrType GetTypeByMethodTable(ulong mt, ulong cmt)
        {
            return GetTypeByMethodTable(mt, cmt, 0);
        }

        internal override ClrType GetTypeByMethodTable(ulong mt, ulong cmt, ulong obj)
        {
            if (mt == 0)
                return null;

            ClrType componentType = null;
            if (mt == DesktopRuntime.ArrayMethodTable)
            {
                if (cmt != 0)
                {
                    componentType = GetTypeByMethodTable(cmt, 0);
                    if (componentType != null)
                    {
                        cmt = componentType.MethodTable;
                    }
                    else if (obj != 0)
                    {
                        componentType = TryGetComponentType(obj, cmt);
                        if (componentType != null)
                            cmt = componentType.MethodTable;
                    }
                }
                else
                {
                    componentType = ObjectType;
                    cmt = ObjectType.MethodTable;
                }
            }
            else
            {
                cmt = 0;
            }

            TypeHandle hnd = new TypeHandle(mt, cmt);
            ClrType ret = null;

            // See if we already have the type.
            if (_indices.TryGetValue(hnd, out int index))
            {
                ret = _types[index];
            }
            else if (mt == DesktopRuntime.ArrayMethodTable && cmt == 0)
            {
                // Handle the case where the methodtable is an array, but the component method table
                // was not specified.  (This happens with fields.)  In this case, return System.Object[],
                // with an ArrayComponentType set to System.Object.
                uint token = DesktopRuntime.GetMetadataToken(mt);
                if (token == 0xffffffff)
                    return null;

                ModuleEntry modEnt = new ModuleEntry(ArrayType.Module, token);

                ret = ArrayType;
                index = _types.Count;

                _indices[hnd] = index;
                _typeEntry[modEnt] = index;
                _types.Add(ret);

                Debug.Assert(_types[index] == ret);
            }
            else
            {
                // No, so we'll have to construct it.
                var moduleAddr = DesktopRuntime.GetModuleForMT(hnd.MethodTable);
                DesktopModule module = DesktopRuntime.GetModule(moduleAddr);
                uint token = DesktopRuntime.GetMetadataToken(mt);

                bool isFree = mt == DesktopRuntime.FreeMethodTable;
                if (token == 0xffffffff && !isFree)
                    return null;

                // Dynamic functions/modules
                uint tokenEnt = token;
                if (!isFree && (module == null || module.IsDynamic))
                    tokenEnt = (uint)mt;

                ModuleEntry modEnt = new ModuleEntry(module, tokenEnt);

                if (ret == null)
                {
                    IMethodTableData mtData = DesktopRuntime.GetMethodTableData(mt);
                    if (mtData == null)
                        return null;

                    ret = new DesktopHeapType(() => GetTypeName(hnd, module, token), module, token, mt, mtData, this) { ComponentType = componentType };
                    index = _types.Count;
                    ((DesktopHeapType)ret).SetIndex(index);
                    _indices[hnd] = index;
                    _typeEntry[modEnt] = index;
                    _types.Add(ret);

                    Debug.Assert(_types[index] == ret);
                }
            }

            if (obj != 0 && ret.ComponentType == null && ret.IsArray)
                ret.ComponentType = TryGetComponentType(obj, cmt);

            return ret;
        }

        public override ClrType GetObjectType(ulong objRef)
        {
            ulong mt, cmt = 0;

            if (_lastObject.Address == objRef)
                return _lastObject.Type;

            if (IsHeapCached)
                return base.GetObjectType(objRef);

            var cache = MemoryReader;
            if (cache.Contains(objRef))
            {
                if (!cache.ReadPtr(objRef, out mt))
                    return null;
            }
            else if (DesktopRuntime.MemoryReader.Contains(objRef))
            {
                cache = DesktopRuntime.MemoryReader;
                if (!cache.ReadPtr(objRef, out mt))
                    return null;
            }
            else
            {
                cache = null;
                mt = DesktopRuntime.DataReader.ReadPointerUnsafe(objRef);
            }

            if ((((int)mt) & 3) != 0)
                mt &= ~3UL;

            if (mt == DesktopRuntime.ArrayMethodTable)
            {
                uint elemenTypeOffset = (uint)PointerSize * 2;
                if (cache == null)
                    cmt = DesktopRuntime.DataReader.ReadPointerUnsafe(objRef + elemenTypeOffset);
                else if (!cache.ReadPtr(objRef + elemenTypeOffset, out cmt))
                    return null;
            }
            else
            {
                cmt = 0;
            }

            ClrType type = GetTypeByMethodTable(mt, cmt, objRef);
            _lastObject = ClrObject.Create(objRef, type);

            return type;
        }
    }

    internal class V46GCHeap : DesktopGCHeap
    {
        private ClrObject _lastObject;
        private Dictionary<ulong, int> _indices = new Dictionary<ulong, int>();
        
        public V46GCHeap(DesktopRuntimeBase runtime)
            : base(runtime)
        {

        }

        public override ClrType GetObjectType(ulong objRef)
        {
            ulong mt;

            if (_lastObject.Address == objRef)
                return _lastObject.Type;

            if (IsHeapCached)
                return base.GetObjectType(objRef);

            var cache = MemoryReader;
            if (cache.Contains(objRef))
            {
                if (!cache.ReadPtr(objRef, out mt))
                    return null;
            }
            else if (DesktopRuntime.MemoryReader.Contains(objRef))
            {
                cache = DesktopRuntime.MemoryReader;
                if (!cache.ReadPtr(objRef, out mt))
                    return null;
            }
            else
            {
                cache = null;
                mt = DesktopRuntime.DataReader.ReadPointerUnsafe(objRef);
            }

            if ((((int)mt) & 3) != 0)
                mt &= ~3UL;
            
            ClrType type = GetTypeByMethodTable(mt, 0, objRef);
            _lastObject = ClrObject.Create(objRef, type);

            return type;
        }

        public override ClrType GetTypeByMethodTable(ulong mt, ulong cmt)
        {
            return GetTypeByMethodTable(mt, 0, 0);
        }

        internal override ClrType GetTypeByMethodTable(ulong mt, ulong _, ulong obj)
        {
            if (mt == 0)
                return null;

            ClrType ret = null;

            // See if we already have the type.
            if (_indices.TryGetValue(mt, out int index))
            {
                ret = _types[index];
            }
            else
            {
                // No, so we'll have to construct it.
                var moduleAddr = DesktopRuntime.GetModuleForMT(mt);
                DesktopModule module = DesktopRuntime.GetModule(moduleAddr);
                uint token = DesktopRuntime.GetMetadataToken(mt);

                bool isFree = mt == DesktopRuntime.FreeMethodTable;
                if (token == 0xffffffff && !isFree)
                    return null;

                // Dynamic functions/modules
                uint tokenEnt = token;
                if (!isFree && (module == null || module.IsDynamic))
                    tokenEnt = (uint)mt;

                ModuleEntry modEnt = new ModuleEntry(module, tokenEnt);

                if (ret == null)
                {
                    IMethodTableData mtData = DesktopRuntime.GetMethodTableData(mt);
                    if (mtData == null)
                        return null;

                    ret = new DesktopHeapType(() => GetTypeName(mt, module, token), module, token, mt, mtData, this);

                    index = _types.Count;
                    ((DesktopHeapType)ret).SetIndex(index);
                    _indices[mt] = index;

                    // Arrays share a common token, so it's not helpful to look them up here.
                    if (!ret.IsArray)
                        _typeEntry[modEnt] = index;

                    _types.Add(ret);
                    Debug.Assert(_types[index] == ret);
                }
            }

            if (obj != 0 && ret.ComponentType == null && ret.IsArray)
                ret.ComponentType = TryGetComponentType(obj, 0);

            return ret;
        }
    }
}
