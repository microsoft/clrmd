// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopArrayType : BaseDesktopHeapType
    {
        private ClrElementType _arrayElement;
        private ClrType _arrayElementType;
        private int _ranks;
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

        public override ulong MethodTable
        {
            get
            {
                // Unfortunately this is a "fake" type (we constructed it because we could not
                // get the real type from the dac APIs).  So we have nothing we can return here.
                return 0;
            }
        }

        public override IEnumerable<ulong> EnumerateMethodTables()
        {
            return new ulong[] { MethodTable };
        }

        public override ClrModule Module { get { return DesktopModule; } }


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

        override public bool IsArray { get { return true; } }

        override public IList<ClrInstanceField> Fields { get { return new ClrInstanceField[0]; } }

        override public IList<ClrStaticField> StaticFields { get { return new ClrStaticField[0]; } }

        override public IList<ClrThreadStaticField> ThreadStaticFields { get { return new ClrThreadStaticField[0]; } }

        override public IList<ClrMethod> Methods { get { return new ClrMethod[0]; } }

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

        public override ClrHeap Heap
        {
            get { return DesktopHeap; }
        }

        public override IList<ClrInterface> Interfaces
        { // todo
            get { return new ClrInterface[0]; }
        }

        public override bool IsFinalizable
        {
            get { return false; }
        }

        public override bool IsPublic
        {
            get { return true; }
        }

        public override bool IsPrivate
        {
            get { return false; }
        }

        public override bool IsInternal
        {
            get { return false; }
        }

        public override bool IsProtected
        {
            get { return false; }
        }

        public override bool IsAbstract
        {
            get { return false; }
        }

        public override bool IsSealed
        {
            get { return false; }
        }

        public override bool IsInterface
        {
            get { return false; }
        }

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

        public override ClrType BaseType
        {
            get { return DesktopHeap.ArrayType; }
        }

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

        public override int ElementSize
        {
            get { return DesktopInstanceField.GetSize(null, _arrayElement); }
        }

        public override int BaseSize
        {
            get { return IntPtr.Size * 8; }
        }

        public override void EnumerateRefsOfObjectCarefully(ulong objRef, Action<ulong, int> action)
        {
            ClrType realType = DesktopHeap.GetObjectType(objRef);
            realType.EnumerateRefsOfObjectCarefully(objRef, action);
        }
    }
}
