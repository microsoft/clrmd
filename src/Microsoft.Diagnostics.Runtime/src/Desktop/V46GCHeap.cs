// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class V46GCHeap : DesktopGCHeap
    {
        private ClrObject _lastObject;
        private readonly Dictionary<ulong, int> _indices = new Dictionary<ulong, int>();

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

            unchecked
            {
                if (((byte)mt & 3) != 0)
                    mt &= ~3UL;
            }

            var type = GetTypeByMethodTable(mt, 0, objRef);
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
            if (_indices.TryGetValue(mt, out var index))
            {
                ret = _types[index];
            }
            else
            {
                // No, so we'll have to construct it.
                var moduleAddr = DesktopRuntime.GetModuleForMT(mt);
                var module = DesktopRuntime.GetModule(moduleAddr);
                var token = DesktopRuntime.GetMetadataToken(mt);

                var isFree = mt == DesktopRuntime.FreeMethodTable;
                if (token == 0xffffffff && !isFree)
                    return null;

                // Dynamic functions/modules
                var tokenEnt = token;
                if (!isFree && (module == null || module.IsDynamic))
                    tokenEnt = unchecked((uint)mt);

                var modEnt = new ModuleEntry(module, tokenEnt);

                if (ret == null)
                {
                    var mtData = DesktopRuntime.GetMethodTableData(mt);
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