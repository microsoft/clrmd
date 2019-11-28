// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class ClrmdType : ClrType
    {
        private string _name;

        private readonly ITypeHelpers _helpers;
        private TypeAttributes _attributes;
        private ulong? _loaderAllocatorHandle;
        private byte _finalizable;

        private ClrMethod[] _methods;
        private ClrInstanceField[] _fields;
        private ClrStaticField[] _statics;

        private int _baseArrayOffset;
        private bool? _runtimeType;
        private EnumData _enumData;
        private ClrElementType _elementType;
        private GCDesc? _gcDesc;


        public override string Name => _name ?? (_name = _helpers.GetTypeName(MethodTable));

        public override int BaseSize { get; }
        public override int ComponentSize { get; }
        public override ClrModule Module { get; }
        public override GCDesc GCDesc => GetOrCreateGCDesc();

        public override ClrElementType ElementType => GetElementType();
        public bool Shared { get; }


        public override ulong MethodTable { get; }
        public override ClrHeap Heap { get; }

        public override ClrType BaseType { get; }

        private IDataReader DataReader => _helpers.DataReader;

        public override bool ContainsPointers { get; }

        internal ClrmdType(ClrHeap heap, ClrModule module, ITypeData data)
        {
            _helpers = data.Helpers;
            MethodTable = data.MethodTable;
            Heap = heap;
            Module = module;
            MetadataToken = data.Token;
            Shared = data.IsShared;
            BaseSize = data.BaseSize;
            ComponentSize = data.ComponentSize;
            ContainsPointers = data.ContainsPointers;
            BaseType = _helpers?.Factory?.GetOrCreateType(heap, data.ParentMethodTable, 0);

            if (data.MethodCount == 0)
                _methods = Array.Empty<ClrMethod>();

            DebugOnlyLoadLazyValues();
        }

        [Conditional("DEBUG")]
        private void DebugOnlyLoadLazyValues()
        {
            _ = Name;
            _ = Fields;
            _ = Methods;
        }


        private GCDesc GetOrCreateGCDesc()
        {
            if (_gcDesc.HasValue)
                return _gcDesc.Value;

            IDataReader reader = _helpers.DataReader;
            if (reader == null)
                return default;

            Debug.Assert(MethodTable != 0, "Attempted to fill GC desc with a constructed (not real) type.");
            if (!reader.Read(MethodTable - (ulong)IntPtr.Size, out int entries))
            {
                _gcDesc = default;
                return default;
            }

            // Get entries in map
            if (entries < 0)
                entries = -entries;

            int slots = 1 + entries * 2;
            byte[] buffer = new byte[slots * IntPtr.Size];
            if (!reader.ReadMemory(MethodTable - (ulong)(slots * IntPtr.Size), buffer, out int read) || read != buffer.Length)
            {
                _gcDesc = default;
                return default;
            }

            // Construct the gc desc
            _gcDesc = new GCDesc(buffer);
            return _gcDesc.Value;
        }

        public override uint MetadataToken { get; }

        public override IEnumerable<ClrInterface> EnumerateInterfaces()
        {
            MetaDataImport import = Module.MetadataImport;
            if (import != null)
            {
                foreach (int token in import.EnumerateInterfaceImpls(MetadataToken))
                {
                    if (import.GetInterfaceImplProps(token, out _, out int mdIFace))
                    {
                        ClrInterface result = GetInterface(import, mdIFace);
                        if (result != null)
                            yield return result;
                    }
                }
            }
        }

        private ClrInterface GetInterface(MetaDataImport import, int mdIFace)
        {
            ClrInterface result = null;
            if (!import.GetTypeDefProperties(mdIFace, out string name, out _, out int extends))
            {
                name = import.GetTypeRefName(mdIFace);
            }

            // TODO:  Handle typespec case.
            if (name != null)
            {
                ClrInterface type = null;
                if (extends != 0 && extends != 0x01000000)
                    type = GetInterface(import, extends);

                result = new DesktopHeapInterface(name, type);
            }

            return result;
        }

        private ClrElementType GetElementType()
        {
            if (_elementType != ClrElementType.Unknown)
                return _elementType;


            if (this == Heap.ObjectType)
                return _elementType = ClrElementType.Object;

            if (this == Heap.StringType)
                return _elementType = ClrElementType.String;
            if (ElementSize > 0)
                return _elementType = ClrElementType.SZArray;

            ClrType baseType = BaseType;
            if (baseType == null || baseType == Heap.ObjectType)
                return _elementType = ClrElementType.Object;


            if (Name != "System.ValueType")
            {
                ClrElementType et = baseType.ElementType;
                return _elementType = et;
            }

            switch (Name)
            {
                case "System.Int32":
                    return _elementType = ClrElementType.Int32;
                case "System.Int16":
                    return _elementType = ClrElementType.Int16;
                case "System.Int64":
                    return _elementType = ClrElementType.Int64;
                case "System.IntPtr":
                    return _elementType = ClrElementType.NativeInt;
                case "System.UInt16":
                    return _elementType = ClrElementType.UInt16;
                case "System.UInt32":
                    return _elementType = ClrElementType.UInt32;
                case "System.UInt64":
                    return _elementType = ClrElementType.UInt64;
                case "System.UIntPtr":
                    return _elementType = ClrElementType.NativeUInt;
                case "System.Boolean":
                    return _elementType = ClrElementType.Boolean;
                case "System.Single":
                    return _elementType = ClrElementType.Float;
                case "System.Double":
                    return _elementType = ClrElementType.Double;
                case "System.Byte":
                    return _elementType = ClrElementType.UInt8;
                case "System.Char":
                    return _elementType = ClrElementType.Char;
                case "System.SByte":
                    return _elementType = ClrElementType.Int8;
                case "System.Enum":
                    return _elementType = ClrElementType.Int32;
                default:
                    break;
            }

            return _elementType = ClrElementType.Struct;
        }

        public override ulong GetSize(ulong objRef) => Heap.GetObjectSize(objRef, this);

        public override string ToString()
        {
            return Name;
        }

        public override bool IsException
        {
            get
            {
                ClrType type = this;
                while (type != null)
                    if (type == Heap.ExceptionType)
                        return true;
                    else
                        type = type.BaseType;

                return false;
            }
        }

        // TODO:  Add ClrObject GetCcw/GetRcw
        // TODO:  Move out of ClrType.
        public override CcwData GetCCWData(ulong obj) => _helpers.Factory.CreateCCWForObject(obj);
        public override RcwData GetRCWData(ulong obj) => _helpers.Factory.CreateRCWForObject(obj);

        private class EnumData
        {
            internal ClrElementType ElementType;
            internal readonly Dictionary<string, object> NameToValue = new Dictionary<string, object>();
            internal readonly Dictionary<object, string> ValueToName = new Dictionary<object, string>();
        }


        public override bool TryGetEnumValue(string name, out int value)
        {
            if (TryGetEnumValue(name, out object val))
            {
                value = (int)val;
                return true;
            }

            value = int.MinValue;
            return false;
        }

        public override bool TryGetEnumValue(string name, out object value)
        {
            if (_enumData == null)
                InitEnumData();

            return _enumData.NameToValue.TryGetValue(name, out value);
        }

        public override bool IsEnum
        {
            get
            {
                for (ClrType type = this; type != null; type = type.BaseType)
                    if (type.Name == "System.Enum")
                        return true;

                return false;
            }
        }

        public override string GetEnumName(object value)
        {
            if (_enumData == null)
                InitEnumData();

            _enumData.ValueToName.TryGetValue(value, out string result);
            return result;
        }

        public override string GetEnumName(int value)
        {
            return GetEnumName((object)value);
        }

        public override IEnumerable<string> GetEnumNames()
        {
            if (_enumData == null)
                InitEnumData();

            return _enumData.NameToValue.Keys;
        }

        private void InitEnumData()
        {
            if (!IsEnum)
                throw new InvalidOperationException("Type is not an Enum.");

            _enumData = new EnumData();
            MetaDataImport import = Module.MetadataImport;
            if (import == null)
                return;

            List<string> names = new List<string>();
            foreach (int token in import.EnumerateFields((int)MetadataToken))
            {
                if (import.GetFieldProps(token, out string name, out FieldAttributes attr, out IntPtr ppvSigBlob, out int pcbSigBlob, out int pdwCPlusTypeFlag, out IntPtr ppValue))
                {
                    if ((int)attr == 0x606 && name == "value__")
                    {
                        SigParser parser = new SigParser(ppvSigBlob, pcbSigBlob);
                        if (parser.GetCallingConvInfo(out _) && parser.GetElemType(out int elemType))
                            _enumData.ElementType = (ClrElementType)elemType;
                    }

                    // public, static, literal, has default
                    if ((int)attr == 0x8056)
                    {
                        names.Add(name);

                        SigParser parser = new SigParser(ppvSigBlob, pcbSigBlob);
                        parser.GetCallingConvInfo(out _);
                        parser.GetElemType(out _);

                        Type type = ((ClrElementType)pdwCPlusTypeFlag).GetTypeForElementType();
                        if (type != null)
                        {
                            object o = Marshal.PtrToStructure(ppValue, type);
                            _enumData.NameToValue[name] = o;
                            _enumData.ValueToName[o] = name;
                        }
                    }
                }
            }
        }

        public override bool IsFree => this == Heap.FreeType;

        private const uint FinalizationSuppressedFlag = 0x40000000;

        public override bool IsFinalizeSuppressed(ulong obj)
        {
            // TODO move to ClrObject?
            uint value = _helpers.DataReader.ReadUnsafe<uint>(obj - 4);

            return (value & FinalizationSuppressedFlag) == FinalizationSuppressedFlag;
        }

        public override bool IsFinalizable
        {
            get
            {
                if (_finalizable == 0)
                {
                    foreach (ClrMethod method in Methods)
                    {
                        if (method.IsVirtual && method.Name == "Finalize")
                        {
                            _finalizable = 1;
                            break;
                        }
                    }

                    if (_finalizable == 0)
                        _finalizable = 2;
                }

                return _finalizable == 1;
            }
        }

        public override bool IsArray => ComponentSize != 0 && this != Heap.StringType && this != Heap.FreeType;
        public override bool IsCollectible => LoaderAllocatorHandle != 0;

        public override ulong LoaderAllocatorHandle
        {
            get
            {
                if (_loaderAllocatorHandle.HasValue)
                    return _loaderAllocatorHandle.Value;

                ulong handle = _helpers.GetLoaderAllocatorHandle(MethodTable);
                _loaderAllocatorHandle = handle;
                return handle;
            }
        }

        public override bool IsString => this == Heap.StringType;

        public override bool GetFieldForOffset(int fieldOffset, bool inner, out ClrInstanceField childField, out int childFieldOffset)
        {
            if (!IsArray)
            {
                int offset = fieldOffset;

                if (!inner)
                    offset -= IntPtr.Size;

                foreach (ClrInstanceField field in Fields)
                {
                    if (field.ElementType == ClrElementType.Unknown)
                        break;

                    if (field.Offset <= offset && offset < field.Offset + field.Size)
                    {
                        childField = field;
                        childFieldOffset = offset - field.Offset;
                        return true;
                    }
                }
            }

            if (BaseType != null)
                return BaseType.GetFieldForOffset(fieldOffset, inner, out childField, out childFieldOffset);

            childField = null;
            childFieldOffset = 0;
            return false;
        }

        public override int ElementSize => (int)ComponentSize;
        public override IReadOnlyList<ClrInstanceField> Fields
        {
            get
            {
                if (_fields == null)
                    InitFields();

                return _fields;
            }
        }

        public override IReadOnlyList<ClrStaticField> StaticFields
        {
            get
            {
                if (_fields == null)
                    InitFields();

                if (_statics == null)
                    return Array.Empty<ClrStaticField>();

                return _statics;
            }
        }

        private void InitFields()
        {
            if (_fields != null)
                return;

            _helpers.Factory.CreateFieldsForMethodTable(this, out _fields, out _statics);

            if (_fields == null)
                _fields = Array.Empty<ClrInstanceField>();

            if (_statics == null)
                _statics = Array.Empty<ClrStaticField>();
        }

        internal override ClrMethod GetMethod(uint token)
        {
            return Methods.FirstOrDefault(m => m.MetadataToken == token);
        }

        public override IReadOnlyList<ClrMethod> Methods
        {
            get
            {
                if (_methods != null)
                    return _methods;

                return _methods = _helpers?.EnumerateMethods(MethodTable).Select(data => new ClrmdMethod(this, data)).ToArray();
            }
        }

        public override ClrStaticField GetStaticFieldByName(string name)
        {
            foreach (ClrStaticField field in StaticFields)
                if (field.Name == name)
                    return field;

            return null;
        }

        //TODO: remove
        public override ClrInstanceField GetFieldByName(string name) => Fields.FirstOrDefault(f => f.Name == name);


        public override ulong GetArrayElementAddress(ulong objRef, int index)
        {
            //todo: remove
            if (_baseArrayOffset == 0)
            {
                ClrType componentType = ComponentType;

                IObjectData data = _helpers.GetObjectData(objRef);
                if (data != null)
                {
                    _baseArrayOffset = (int)(data.DataPointer - objRef);
                    Debug.Assert(_baseArrayOffset >= 0);
                }
                else if (componentType != null)
                {
                    if (!componentType.IsObjectReference)
                        _baseArrayOffset = IntPtr.Size * 2;
                }
                else
                {
                    return 0;
                }
            }

            return objRef + (ulong)(_baseArrayOffset + index * ComponentSize);
        }

        //todo: move to clrobject
        public override object GetArrayElementValue(ulong objRef, int index)
        {
            ulong addr = GetArrayElementAddress(objRef, index);
            if (addr == 0)
                return null;

            ClrType componentType = ComponentType;
            ClrElementType cet;
            if (componentType != null)
            {
                cet = componentType.ElementType;
            }
            else
            {
                // Slow path, we need to get the element type of the array.
                IObjectData data = _helpers.GetObjectData(objRef);
                if (data == null)
                    return null;

                cet = data.ElementType;
            }

            if (cet == ClrElementType.Unknown)
                return null;

            if (cet == ClrElementType.String)
                addr = DataReader.ReadPointerUnsafe(addr);

            return ValueReader.GetValueAtAddress(Heap, DataReader, cet, addr);
        }


        /// <summary>
        /// A messy version with better performance that doesn't use regular expression.
        /// </summary>
        internal static int FixGenericsWorker(string name, int start, int end, StringBuilder sb)
        {
            int parenCount = 0;
            while (start < end)
            {
                char c = name[start];
                if (c == '`')
                    break;

                if (c == '[')
                    parenCount++;

                if (c == ']')
                    parenCount--;

                if (parenCount < 0)
                    return start + 1;

                if (c == ',' && parenCount == 0)
                    return start;

                sb.Append(c);
                start++;
            }

            if (start >= end)
                return start;

            start++;
            int paramCount = 0;

            bool hasSubtypeArity;
            do
            {
                int currParamCount = 0;
                hasSubtypeArity = false;
                // Skip arity.
                while (start < end)
                {
                    char c = name[start];
                    if (c < '0' || c > '9')
                        break;

                    currParamCount = currParamCount * 10 + c - '0';
                    start++;
                }

                paramCount += currParamCount;
                if (start >= end)
                    return start;

                if (name[start] == '+')
                {
                    while (start < end && name[start] != '[')
                    {
                        if (name[start] == '`')
                        {
                            start++;
                            hasSubtypeArity = true;
                            break;
                        }

                        sb.Append(name[start]);
                        start++;
                    }

                    if (start >= end)
                        return start;
                }
            } while (hasSubtypeArity);

            if (name[start] == '[')
            {
                sb.Append('<');
                start++;
                while (paramCount-- > 0)
                {
                    if (start >= end)
                        return start;

                    bool withModule = false;
                    if (name[start] == '[')
                    {
                        withModule = true;
                        start++;
                    }

                    start = FixGenericsWorker(name, start, end, sb);

                    if (start < end && name[start] == '[')
                    {
                        start++;
                        if (start >= end)
                            return start;

                        sb.Append('[');

                        while (start < end && name[start] == ',')
                        {
                            sb.Append(',');
                            start++;
                        }

                        if (start >= end)
                            return start;

                        if (name[start] == ']')
                        {
                            sb.Append(']');
                            start++;
                        }
                    }

                    if (withModule)
                    {
                        while (start < end && name[start] != ']')
                            start++;
                        start++;
                    }

                    if (paramCount > 0)
                    {
                        if (start >= end)
                            return start;

                        //Debug.Assert(name[start] == ',');
                        sb.Append(',');
                        start++;

                        if (start >= end)
                            return start;

                        if (name[start] == ' ')
                            start++;
                    }
                }

                sb.Append('>');
                start++;
            }

            if (start + 1 >= end)
                return start;

            if (name[start] == '[' && name[start + 1] == ']')
                sb.Append("[]");

            return start;
        }

        internal static string FixGenerics(string name)
        {
            StringBuilder builder = new StringBuilder();
            FixGenericsWorker(name, 0, name.Length, builder);
            return builder.ToString();
        }

        private void InitFlags()
        {
            if (_attributes != 0 || Module == null)
                return;

            MetaDataImport import = Module?.MetadataImport;
            if (import == null)
            {
                _attributes = (TypeAttributes)0x70000000;
                return;
            }

            if (!import.GetTypeDefAttributes((int)MetadataToken, out _attributes) || _attributes == 0)
                _attributes = (TypeAttributes)0x70000000;
        }

        public override bool IsInternal
        {
            get
            {
                if (_attributes == 0)
                    InitFlags();

                TypeAttributes visibility = _attributes & TypeAttributes.VisibilityMask;
                return visibility == TypeAttributes.NestedAssembly || visibility == TypeAttributes.NotPublic;
            }
        }

        public override bool IsPublic
        {
            get
            {
                if (_attributes == 0)
                    InitFlags();

                TypeAttributes visibility = _attributes & TypeAttributes.VisibilityMask;
                return visibility == TypeAttributes.Public || visibility == TypeAttributes.NestedPublic;
            }
        }

        public override bool IsPrivate
        {
            get
            {
                if (_attributes == 0)
                    InitFlags();

                TypeAttributes visibility = _attributes & TypeAttributes.VisibilityMask;
                return visibility == TypeAttributes.NestedPrivate;
            }
        }

        public override bool IsProtected
        {
            get
            {
                if (_attributes == 0)
                    InitFlags();

                TypeAttributes visibility = _attributes & TypeAttributes.VisibilityMask;
                return visibility == TypeAttributes.NestedFamily;
            }
        }

        public override bool IsAbstract
        {
            get
            {
                if (_attributes == 0)
                    InitFlags();

                return (_attributes & TypeAttributes.Abstract) == TypeAttributes.Abstract;
            }
        }

        public override bool IsSealed
        {
            get
            {
                if (_attributes == 0)
                    InitFlags();

                return (_attributes & TypeAttributes.Sealed) == TypeAttributes.Sealed;
            }
        }

        public override bool IsInterface
        {
            get
            {
                if (_attributes == 0)
                    InitFlags();
                return (_attributes & TypeAttributes.Interface) == TypeAttributes.Interface;
            }
        }

        public override bool IsRuntimeType
        {
            get
            {
                if (_runtimeType == null)
                    _runtimeType = Name == "System.RuntimeType";

                return (bool)_runtimeType;
            }
        }

        public override ClrType GetRuntimeType(ulong obj)
        {
            if (!IsRuntimeType)
                return null;

            ClrInstanceField field = GetFieldByName("m_handle");
            if (field == null)
                return null;

            ulong methodTable = 0;
            if (field.ElementType == ClrElementType.NativeInt)
            {
                methodTable = (ulong)(long)field.GetValue(obj);
            }
            else if (field.ElementType == ClrElementType.Struct)
            {
                ClrInstanceField ptrField = field.Type.GetFieldByName("m_ptr");
                methodTable = (ulong)(long)ptrField.GetValue(field.GetAddress(obj, false), true);
            }

            return null;// Heap.GetTypeByMethodTable(methodTable, 0, obj);
        }
    }
}