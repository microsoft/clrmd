// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class DesktopPointerType : BaseDesktopHeapType
    {
        private readonly ClrElementType _pointerElement;
        private ClrType _pointerElementType;
        private string _name;

        public DesktopPointerType(DesktopGCHeap heap, DesktopBaseModule module, ClrElementType eltype, uint token, string nameHint)
            : base(0, heap, module, token)
        {
            ElementType = ClrElementType.Pointer;
            _pointerElement = eltype;
            if (nameHint != null)
                BuildName(nameHint);
        }

        public override ClrModule Module => DesktopModule;

        // We have no good way of finding this value, unfortunately
        public override ulong MethodTable => 0;

        public override IEnumerable<ulong> EnumerateMethodTables()
        {
            return new[] {MethodTable};
        }

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
            builder.Append("*");
            _name = builder.ToString();
        }

        private string GetElementTypeName(string hint)
        {
            switch (_pointerElement)
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

            return "POINTER";
        }

        public override bool IsFinalizeSuppressed(ulong obj)
        {
            return false;
        }

        public override ClrType ComponentType
        {
            get
            {
                if (_pointerElementType == null)
                    _pointerElementType = DesktopHeap.GetBasicType(_pointerElement);

                return _pointerElementType;
            }
            internal set
            {
                if (value != null)
                    _pointerElementType = value;
            }
        }

        public override bool IsPointer => true;
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

        public override ClrType BaseType => null;

        public override int GetArrayLength(ulong objRef)
        {
            return 0;
        }

        public override ulong GetArrayElementAddress(ulong objRef, int index)
        {
            return 0;
        }

        public override object GetArrayElementValue(ulong objRef, int index)
        {
            return null;
        }

        public override int ElementSize => DesktopInstanceField.GetSize(null, _pointerElement);

        public override int BaseSize => IntPtr.Size * 8;

        public override void EnumerateRefsOfObjectCarefully(ulong objRef, Action<ulong, int> action) // TODO GET HELP
        {
            ClrType realType = DesktopHeap.GetObjectType(objRef);
            realType.EnumerateRefsOfObjectCarefully(objRef, action);
        }
    }
}