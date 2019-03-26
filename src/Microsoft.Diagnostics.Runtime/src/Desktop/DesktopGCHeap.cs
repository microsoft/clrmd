// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Diagnostics.Runtime.DacInterface;

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
            ErrorType = new ErrorType(this);
            _arrayType = new Lazy<ClrType>(CreateArrayType);
            _exceptionType = new Lazy<ClrType>(() => GetTypeByMethodTable(DesktopRuntime.ExceptionMethodTable, 0, 0) ?? ErrorType);

            StringType = GetTypeByMethodTable(DesktopRuntime.StringMethodTable, 0, 0) ?? ErrorType;
            ObjectType = GetTypeByMethodTable(DesktopRuntime.ObjectMethodTable, 0, 0) ?? ErrorType;
            Free = CreateFree();

            InitSegments(runtime);
        }

        private ClrType CreateFree()
        {
            ClrType free = GetTypeByMethodTable(DesktopRuntime.FreeMethodTable, 0, 0);
            if (free == null)
                return ErrorType;

            ((DesktopHeapType)free).Shared = true;
            ((BaseDesktopHeapType)free).DesktopModule = (DesktopModule)ObjectType.Module;
            return free;
        }

        private ClrType CreateArrayType()
        {
            ClrType type = GetTypeByMethodTable(DesktopRuntime.ArrayMethodTable, DesktopRuntime.ObjectMethodTable, 0);
            if (type == null)
                return ErrorType;

            type.ComponentType = ObjectType;
            return type;
        }

        protected override int GetRuntimeRevision()
        {
            return DesktopRuntime.Revision;
        }

        public override ClrRuntime Runtime => DesktopRuntime;

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

        protected static string GetTypeNameFromToken(DesktopModule module, uint token)
        {
            if (module == null)
                return null;

            MetaDataImport meta = module.GetMetadataImport();
            if (meta == null)
                return null;

            // Get type name.
            if (!meta.GetTypeDefProperties((int)token, out string name, out TypeAttributes attrs, out int parent))
                return null;

            if (meta.GetNestedClassProperties((int)token, out int enclosing) && token != enclosing)
            {
                string inner = GetTypeNameFromToken(module, (uint)enclosing);
                if (inner == null)
                    inner = "<UNKNOWN>";

                return $"{inner}+{name}";
            }

            return name;
        }

        public override IEnumerable<ulong> EnumerateFinalizableObjectAddresses()
        {
            if (DesktopRuntime.GetHeaps(out SubHeap[] heaps))
            {
                foreach (SubHeap heap in heaps)
                {
                    foreach (ulong obj in DesktopRuntime.GetPointersInRange(heap.FQAllObjectsStart, heap.FQAllObjectsStop))
                    {
                        if (obj == 0)
                            continue;

                        ClrType type = GetObjectType(obj);
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
                Dictionary<ClrThread, ClrRoot[]> cache = new Dictionary<ClrThread, ClrRoot[]>();

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

        public override bool AreRootsCached => (_stackwalkPolicy == ClrRootStackwalkPolicy.SkipStack || (_stackCache != null && _currentStackCache == StackwalkPolicy))
            && _strongHandles != null;

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

        protected internal override IEnumerable<ClrHandle> EnumerateStrongHandles()
        {
            if (_strongHandles != null)
            {
                Debug.Assert(_dependentHandles != null);
                return _strongHandles;
            }

            return EnumerateStrongHandlesWorker(CancellationToken.None);
        }

        protected internal override void BuildDependentHandleMap(CancellationToken cancelToken)
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
                    }
                }
            }

            if (dependentHandles != null)
                _dependentHandles = dependentHandles;
        }

        protected internal override IEnumerable<ClrRoot> EnumerateStackRoots()
        {
            if (StackwalkPolicy != ClrRootStackwalkPolicy.SkipStack)
            {
                if (_stackCache != null && _currentStackCache == StackwalkPolicy)
                {
                    return _stackCache.SelectMany(t => t.Value);
                }

                return EnumerateStackRootsWorker();
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
                        foreach (ClrRoot root in thread.EnumerateStackObjects(false))
                            yield return root;
                    }
                    else
                    {
                        HashSet<ulong> seen = new HashSet<ulong>();
                        foreach (ClrRoot root in thread.EnumerateStackObjects(true))
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
                foreach (ClrType type in EnumerateTypes())
                {
                    // Statics
                    foreach (ClrStaticField staticField in type.StaticFields)
                    {
                        if (!staticField.ElementType.IsPrimitive())
                        {
                            foreach (ClrAppDomain ad in DesktopRuntime.AppDomains)
                            {
                                ulong addr = 0;
                                // We must manually get the value, as strings will not be returned as an object address.
                                try // If this fails for whatever reasion, don't fail completely.  
                                {
                                    addr = staticField.GetAddress(ad);
                                }
                                catch (Exception e)
                                {
                                    Trace.WriteLine($"Error getting stack field {type.Name}.{staticField.Name}: {e.Message}");
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

                        NextStatic: ;
                    }

                    // Thread statics
                    foreach (ClrThreadStaticField tsf in type.ThreadStaticFields)
                    {
                        if (tsf.ElementType.IsObjectReference())
                        {
                            foreach (ClrAppDomain ad in DesktopRuntime.AppDomains)
                            {
                                foreach (ClrThread thread in DesktopRuntime.Threads)
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
                                        ClrType _userObjType = GetObjectType(_userObj);
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
                                                                yield return new HandleRoot(
                                                                    indexAddr,
                                                                    indexObj,
                                                                    indexObjType,
                                                                    HandleType.AsyncPinned,
                                                                    GCRootKind.AsyncPinning,
                                                                    handle.AppDomain);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        yield return new HandleRoot(
                                                            _userObjAddr,
                                                            _userObj,
                                                            _userObjType,
                                                            HandleType.AsyncPinned,
                                                            GCRootKind.AsyncPinning,
                                                            handle.AppDomain);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                yield return new HandleRoot(
                                                    _userObjAddr,
                                                    _userObj,
                                                    _userObjType,
                                                    HandleType.AsyncPinned,
                                                    GCRootKind.AsyncPinning,
                                                    handle.AppDomain);
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

            return Encoding.Unicode.GetString(buffer);
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

        internal bool TypesLoaded { get; private set; }

        internal void LoadAllTypes()
        {
            if (TypesLoaded)
                return;

            TypesLoaded = true;

            // Walking a module is sloooow.  Ensure we only walk each module once.
            HashSet<ulong> modules = new HashSet<ulong>();

            foreach (ulong module in DesktopRuntime.EnumerateModules(DesktopRuntime.GetAppDomainData(DesktopRuntime.SystemDomainAddress)))
                modules.Add(module);

            foreach (ulong module in DesktopRuntime.EnumerateModules(DesktopRuntime.GetAppDomainData(DesktopRuntime.SharedDomainAddress)))
                modules.Add(module);

            IAppDomainStoreData ads = DesktopRuntime.GetAppDomainStoreData();
            if (ads == null)
                return;

            ulong[] appDomains = DesktopRuntime.GetAppDomainList(ads.Count);
            if (appDomains == null)
                return;

            foreach (ulong ad in appDomains)
            {
                IAppDomainData adData = DesktopRuntime.GetAppDomainData(ad);
                if (adData != null)
                {
                    foreach (ulong module in DesktopRuntime.EnumerateModules(adData))
                        modules.Add(module);
                }
            }

            ulong arrayMt = DesktopRuntime.ArrayMethodTable;
            foreach (ulong module in modules)
            {
                IList<MethodTableTokenPair> mtList = DesktopRuntime.GetMethodTableList(module);
                if (mtList != null)
                {
                    foreach (MethodTableTokenPair pair in mtList)
                    {
                        if (pair.MethodTable != arrayMt)
                        {
                            // prefetch element type, as this also can load types
                            ClrType type = GetTypeByMethodTable(pair.MethodTable, 0, 0);
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

                case ClrElementType.NativeUInt: // native unsigned int
                case ClrElementType.Pointer:
                case ClrElementType.FunctionPointer:
                {
                    if (!MemoryReader.TryReadPtr(addr, out ulong val))
                        return null;

                    return val;
                }

                case ClrElementType.NativeInt: // native int
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

                case ClrElementType.Char: // u2
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

            if (type == StringType)
                return ClrElementType.String;
            if (type.ElementSize > 0)
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
        private bool _initializedStringFields;
        private ClrType[] _basicTypes;

        internal readonly ClrInterface[] EmptyInterfaceList = new ClrInterface[0];
        internal Dictionary<string, ClrInterface> Interfaces = new Dictionary<string, ClrInterface>();
        private readonly Lazy<ClrType> _arrayType;
        private readonly Lazy<ClrType> _exceptionType;

        private DictionaryList _objectMap;
        private ExtendedArray<ObjectInfo> _objects;
        private ExtendedArray<ulong> _gcRefs;

        internal DesktopRuntimeBase DesktopRuntime { get; }
        internal BaseDesktopHeapType ErrorType { get; }
        internal ClrType ObjectType { get; }
        internal ClrType StringType { get; }
        internal ClrType ValueType { get; private set; }
        internal ClrType ArrayType => _arrayType.Value;
        public override ClrType Free { get; }

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

            if (_basicTypes[(int)elType] == null && DesktopRuntime.DataReader.IsMinidump)
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

            return _basicTypes[(int)elType];

            ;
        }

        private void InitBasicTypes()
        {
            const int max = (int)ClrElementType.SZArray + 1;

            _basicTypes = new ClrType[max];
            _basicTypes[(int)ClrElementType.Unknown] = null; // ???
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
            return new DesktopPointerType(this, (DesktopBaseModule)DesktopRuntime.Mscorlib, clrElementType, 0, nameHint) {ComponentType = innerType};
        }

        internal BaseDesktopHeapType GetArrayType(ClrElementType clrElementType, int ranks, string nameHint)
        {
            if (_arrayTypes == null)
                _arrayTypes = new Dictionary<ArrayRankHandle, BaseDesktopHeapType>();

            ArrayRankHandle handle = new ArrayRankHandle(clrElementType, ranks);
            if (!_arrayTypes.TryGetValue(handle, out BaseDesktopHeapType result))
                _arrayTypes[handle] = result = new DesktopArrayType(this, (DesktopBaseModule)DesktopRuntime.Mscorlib, clrElementType, ranks, ArrayType.MetadataToken, nameHint);

            return result;
        }

        protected internal override long TotalObjects => _objects?.Count ?? -1;

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

        protected internal override void EnumerateObjectReferences(ulong obj, ClrType type, bool carefully, Action<ulong, int> callback)
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

        protected internal override IEnumerable<ClrObject> EnumerateObjectReferences(ulong obj, ClrType type, bool carefully)
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
                result = result.Union(values.Select(v => GetObject(v)));

            return result;
        }

        private IEnumerable<ClrObject> EnumerateRefs(uint offset, uint count)
        {
            for (uint i = offset; i < offset + count; i++)
            {
                ulong obj = _gcRefs[i];
                yield return GetObject(obj);
            }
        }

        private void AddObject(DictionaryList objmap, ExtendedArray<ulong> gcrefs, ExtendedArray<ObjectInfo> objInfo, ulong obj, ClrType type)
        {
            uint offset = (uint)gcrefs.Count;

            if (type.ContainsPointers)
            {
                EnumerateObjectReferences(obj, type, true, (addr, offs) => { gcrefs.Add(addr); });
            }

            uint refCount = (uint)gcrefs.Count - offset;
            objmap.Add(obj, checked((int)objInfo.Count));
            objInfo.Add(
                new ObjectInfo
                {
                    Type = type,
                    RefOffset = refCount != 0 ? offset : uint.MaxValue,
                    RefCount = refCount
                });
        }

        protected string GetTypeName(ulong mt, DesktopModule module, uint token)
        {
            string typeName = DesktopRuntime.GetMethodTableName(mt);
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
                string builder = GetTypeNameFromToken(module, token);
                string newName = builder;
                if (newName != null && newName != "<UNKNOWN>")
                    typeName = newName;
            }
            else
            {
                typeName = DesktopHeapType.FixGenerics(typeName);
            }

            return typeName;
        }

        private struct ObjectInfo
        {
            public ClrType Type;
            public uint RefOffset;
            public uint RefCount;

            public override string ToString()
            {
                return $"{Type.Name} refs: {RefCount:n0}";
            }
        }

        internal class ExtendedArray<T>
        {
            private const int Initial = 0x100000; // 1 million-ish
            private const int Secondary = 0x1000000;
            private const int Complete = 0x4000000;

            private readonly List<T[]> _lists = new List<T[]>();
            private int _curr;

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
            private readonly List<Entry> _entries = new List<Entry>();

            public IEnumerable<KeyValuePair<ulong, int>> Enumerate()
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

                Entry last = _entries.Last();
                if (last.Dictionary.Count > MaxEntries)
                    return NewEntry(obj);

                return last;
            }

            private Entry NewEntry(ulong obj)
            {
                Entry result = new Entry {Start = obj, End = obj, Dictionary = new SortedDictionary<ulong, int>()};
                _entries.Add(result);
                return result;
            }

            private class Entry
            {
                public ulong Start;
                public ulong End;
                public SortedDictionary<ulong, int> Dictionary;
            }
        }
    }
}