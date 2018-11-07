// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopThreadStaticField : ClrThreadStaticField
    {
        public DesktopThreadStaticField(DesktopGCHeap heap, IFieldData field, string name)
        {
            _field = field;
            _name = name;
            _token = field.FieldToken;
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

        public override uint Token { get { return _token; } }

        public override ulong GetAddress(ClrAppDomain appDomain, ClrThread thread)
        {
            if (_type == null)
                return 0;

            DesktopRuntimeBase runtime = _type.DesktopHeap.DesktopRuntime;
            IModuleData moduleData = runtime.GetModuleData(_field.Module);

            return runtime.GetThreadStaticPointer(thread.Address, (ClrElementType)_field.CorElementType, (uint)Offset, (uint)moduleData.ModuleId, _type.Shared);
        }


        public override ClrElementType ElementType
        {
            get { return (ClrElementType)_field.CorElementType; }
        }

        public override string Name { get { return _name; } }

        public override ClrType Type { get { return _type; } }

        // these are optional.  
        /// <summary>
        /// If the field has a well defined offset from the base of the object, return it (otherwise -1). 
        /// </summary>
        public override int Offset { get { return (int)_field.Offset; } }

        /// <summary>
        /// Given an object reference, fetch the address of the field. 
        /// </summary>

        public override bool HasSimpleValue
        {
            get { return _type != null && !DesktopRuntimeBase.IsValueClass(ElementType); }
        }
        public override int Size
        {
            get
            {
                return DesktopInstanceField.GetSize(_type, ElementType);
            }
        }

        public override bool IsPublic
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsPrivate
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsInternal
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsProtected
        {
            get { throw new NotImplementedException(); }
        }

        private IFieldData _field;
        private string _name;
        private BaseDesktopHeapType _type;
        private uint _token;
    }
}
