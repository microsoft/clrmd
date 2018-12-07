// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopThreadStaticField : ClrThreadStaticField
    {
        public DesktopThreadStaticField(DesktopGCHeap heap, IFieldData field, string name)
        {
            _field = field;
            Name = name;
            Token = field.FieldToken;
            _type = (BaseDesktopHeapType)heap.GetTypeByMethodTable(field.TypeMethodTable, 0);
        }

        public override object GetValue(ClrAppDomain appDomain, ClrThread thread, bool convertStrings = true)
        {
            if (!HasSimpleValue)
                return null;

            ulong addr = GetAddress(appDomain, thread);
            if (addr == 0)
                return null;

            if (ElementType == ClrElementType.String)
            {
                object val = _type.DesktopHeap.GetValueAtAddress(ClrElementType.Object, addr);

                Debug.Assert(val == null || val is ulong);
                if (val == null || !(val is ulong))
                    return convertStrings ? null : (object)(ulong)0;

                addr = (ulong)val;
                if (!convertStrings)
                    return addr;
            }

            return _type.DesktopHeap.GetValueAtAddress(ElementType, addr);
        }

        public override uint Token { get; }

        public override ulong GetAddress(ClrAppDomain appDomain, ClrThread thread)
        {
            if (_type == null)
                return 0;

            DesktopRuntimeBase runtime = _type.DesktopHeap.DesktopRuntime;
            IModuleData moduleData = runtime.GetModuleData(_field.Module);

            return runtime.GetThreadStaticPointer(thread.Address, (ClrElementType)_field.CorElementType, (uint)Offset, (uint)moduleData.ModuleId, _type.Shared);
        }

        public override ClrElementType ElementType => (ClrElementType)_field.CorElementType;

        public override string Name { get; }

        public override ClrType Type => _type;

        // these are optional.  
        /// <summary>
        /// If the field has a well defined offset from the base of the object, return it (otherwise -1).
        /// </summary>
        public override int Offset => (int)_field.Offset;

        /// <summary>
        /// Given an object reference, fetch the address of the field.
        /// </summary>

        public override bool HasSimpleValue => _type != null && !ElementType.IsValueClass();
        public override int Size => DesktopInstanceField.GetSize(_type, ElementType);

        public override bool IsPublic => throw new NotImplementedException();
        public override bool IsPrivate => throw new NotImplementedException();
        public override bool IsInternal => throw new NotImplementedException();
        public override bool IsProtected => throw new NotImplementedException();

        private readonly IFieldData _field;
        private readonly BaseDesktopHeapType _type;
    }
}