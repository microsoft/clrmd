// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopArrayType : BaseDesktopHeapType
    {
        private readonly ClrElementType _arrayElement;
        private ClrType _arrayElementType;
        private readonly int _ranks;
        private string _name;

        public DesktopArrayType(DesktopGCHeap heap, DesktopBaseModule module, ClrElementType eltype, int ranks, uint token, string nameHint)
            : base(0, heap, module, token)
        {
            ElementType = ClrElementType.Array;
            _arrayElement = eltype;
            _ranks = ranks;
            if (nameHint != null)
                BuildName(nameHint);
        }

        internal override ClrMethod GetMethod(uint token)
        {
            return null;
        }

        // Unfortunately this is a "fake" type (we constructed it because we could not
        // get the real type from the dac APIs).  So we have nothing we can return here.
        public override ulong MethodTable => 0;

        public override IEnumerable<ulong> EnumerateMethodTables()
        {
            return new[] {MethodTable};
        }

        public override ClrModule Module => DesktopModule;

        internal override ulong GetModuleAddress(ClrAppDomain domain)
        {
            return 0;
        }

        public override string Name
        {
            get
            {
                if (_name == null)
                    BuildName(null);

                return _name;
            }
        }

        private void BuildName(string hint)
        {
            StringBuilder builder = new StringBuilder();
            ClrType inner = ComponentType;

            builder.Append(inner != null ? inner.Name : GetElementTypeName(hint));
            builder.Append("[");

            for (int i = 0; i < _ranks - 1; ++i)
                builder.Append(",");

            builder.Append("]");
            _name = builder.ToString();
        }

        private string GetElementTypeName(string hint)
        {
            switch (_arrayElement)
            {
                case ClrElementType.Boolean:
                    return "System.Boolean";

                case ClrElementType.Char:
                    return "System.Char";

                case ClrElementType.Int8:
                    return "System.SByte";

                case ClrElementType.UInt8:
                    return "System.Byte";

                case ClrElementType.Int16:
                    return "System.Int16";

                case ClrElementType.UInt16:
                    return "ClrElementType.UInt16";

                case ClrElementType.Int32:
                    return "System.Int32";

                case ClrElementType.UInt32:
                    return "System.UInt32";

                case ClrElementType.Int64:
                    return "System.Int64";

                case ClrElementType.UInt64:
                    return "System.UInt64";

                case ClrElementType.Float:
                    return "System.Single";

                case ClrElementType.Double:
                    return "System.Double";

                case ClrElementType.NativeInt:
                    return "System.IntPtr";

                case ClrElementType.NativeUInt:
                    return "System.UIntPtr";

                case ClrElementType.Struct:
                    return "Sytem.ValueType";
            }

            if (hint != null)
                return hint;

            return "ARRAY";
        }

        public override bool IsFinalizeSuppressed(ulong obj)
        {
            return false;
        }

        public override ClrType ComponentType
        {
            get
            {
                if (_arrayElementType == null)
                    _arrayElementType = DesktopHeap.GetBasicType(_arrayElement);

                return _arrayElementType;
            }
            internal set
            {
                if (value != null)
                    _arrayElementType = value;
            }
        }

        public override bool IsArray => true;

        public override IList<ClrInstanceField> Fields => new ClrInstanceField[0];

        public override IList<ClrStaticField> StaticFields => new ClrStaticField[0];

        public override IList<ClrThreadStaticField> ThreadStaticFields => new ClrThreadStaticField[0];

        public override IList<ClrMethod> Methods => new ClrMethod[0];

        public override ulong GetSize(ulong objRef)
        {
            ClrType realType = DesktopHeap.GetObjectType(objRef);
            return realType.GetSize(objRef);
        }

        public override void EnumerateRefsOfObject(ulong objRef, Action<ulong, int> action)
        {
            ClrType realType = DesktopHeap.GetObjectType(objRef);
            realType.EnumerateRefsOfObject(objRef, action);
        }

        public override ClrHeap Heap => DesktopHeap;

        public override IList<ClrInterface> Interfaces => new ClrInterface[0];

        public override bool IsFinalizable => false;

        public override bool IsPublic => true;

        public override bool IsPrivate => false;

        public override bool IsInternal => false;

        public override bool IsProtected => false;

        public override bool IsAbstract => false;

        public override bool IsSealed => false;

        public override bool IsInterface => false;

        public override bool GetFieldForOffset(int fieldOffset, bool inner, out ClrInstanceField childField, out int childFieldOffset)
        {
            childField = null;
            childFieldOffset = 0;
            return false;
        }

        public override ClrInstanceField GetFieldByName(string name)
        {
            return null;
        }

        public override ClrStaticField GetStaticFieldByName(string name)
        {
            return null;
        }

        public override ClrType BaseType => DesktopHeap.ArrayType;

        public override int GetArrayLength(ulong objRef)
        {
            //todo
            throw new NotImplementedException();
        }

        public override ulong GetArrayElementAddress(ulong objRef, int index)
        {
            throw new NotImplementedException();
        }

        public override object GetArrayElementValue(ulong objRef, int index)
        {
            throw new NotImplementedException();
        }

        public override int ElementSize => DesktopInstanceField.GetSize(null, _arrayElement);

        public override int BaseSize => IntPtr.Size * 8;

        public override void EnumerateRefsOfObjectCarefully(ulong objRef, Action<ulong, int> action)
        {
            ClrType realType = DesktopHeap.GetObjectType(objRef);
            realType.EnumerateRefsOfObjectCarefully(objRef, action);
        }
    }
}