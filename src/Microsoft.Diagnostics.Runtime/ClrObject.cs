using System;
using System.Diagnostics;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents an object in the target process.
    /// </summary>
    public struct ClrObject
    {
        private ulong _address;
        private ClrType _type;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="address">The address of the object</param>
        /// <param name="type">The concrete type of the object.</param>
        public ClrObject(ulong address, ClrType type)
        {
            _address = address;
            _type = type;

            Debug.Assert(type != null);
            Debug.Assert(address == 0 || type.Heap.GetObjectType(address) == type);
        }

        /// <summary>
        /// The address of the object.
        /// </summary>
        public ulong Address { get { return _address; } }

        /// <summary>
        /// The type of the object.
        /// </summary>
        public ClrType Type { get { return _type; } }

        /// <summary>
        /// Returns if the object value is null.
        /// </summary>
        public bool IsNull { get { return _address == 0; } }

        /// <summary>
        /// Returns whether this ClrObject points to a valid object or not.  An object may be "invalid"
        /// during heap corruption, or if we simply encountered an error and could not determine its type.
        /// </summary>
        public bool IsValid { get { return _type != _type.Heap.ErrorType; } }

        #region Fields
        /// <summary>
        /// Gets an object reference field from ClrObject.  Any field which is a subclass of System.Object
        /// </summary>
        /// <param name="fieldName">The name of the field to retrieve.</param>
        /// <returns></returns>
        public ClrObject GetObjectField(string fieldName)
        {
            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            if (!field.IsObjectReference)
                throw new ArgumentException($"Field '{_type.Name}.{fieldName}' is not an object reference.");

            if (IsNull)
                throw new NullReferenceException();

            ClrHeap heap = _type.Heap;
            ClrType type = heap.ErrorType;

            ulong addr = field.GetAddress(_address);
            ulong obj;

            if (heap.ReadPointer(addr, out obj) && obj != 0)
                type = heap.GetObjectType(obj);

            Debug.Assert(type != null);
            return new ClrObject(obj, type);
        }

        /// <summary>
        /// Gets the given field in this object
        /// </summary>
        /// <param name="fieldName">The name of the field.</param>
        /// <returns>The value of the field.</returns>
        public ClrValue GetField(string fieldName)
        {
            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");
            
            if (IsNull)
                throw new NullReferenceException();

            ulong addr = Address;
            ulong fieldAddr = field.GetAddress(addr);
            if (fieldAddr == 0)
                throw new MemoryReadException(addr);

            return new ClrValueImpl(_type.Heap.Runtime, fieldAddr, field);
        }

        /// <summary>
        /// Gets the value of a boolean field in this type.
        /// </summary>
        /// <param name="fieldName">The name of the field.</param>
        /// <returns>A boolean for the value.</returns>
        public bool GetBoolean(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Boolean, "bool");
            bool result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadBoolean(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        public byte GetByte(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt8, "byte");
            byte result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadByte(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        public sbyte GetSByte(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Int8, "sbyte");
            sbyte result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadByte(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        public char GetChar(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Char, "char");
            char result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadChar(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        public short GetInt16(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Int16, "short");
            short result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadShort(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        public ushort GetUInt16(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt16, "ushort");
            ushort result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadShort(address, out result))
                throw new MemoryReadException(address);

            return result;
        }
        
        public int GetInt32(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Int32, "int");
            int result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadDword(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        public uint GetUInt32(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt32, "uint");
            uint result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadDword(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        public long GetInt64(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Int64, "long");
            long result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadQword(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        public ulong GetUInt64(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt64, "ulong");
            ulong result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadQword(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        public float GetFloat(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Float, "float");
            float result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadFloat(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        public double GetDouble(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Double, "double");
            double result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadFloat(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        public string GetString(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.String, "string");
            ulong str;
            RuntimeBase runtime = (RuntimeBase)_type.Heap.Runtime;

            if (!runtime.ReadPointer(address, out str))
                throw new MemoryReadException(address);

            string result;
            if (!runtime.ReadString(str, out result))
                throw new MemoryReadException(str);
            
            return result;
        }

        public IntPtr GetIntPtr(string fieldName)
        {
            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            if (field.ElementType != ClrElementType.NativeInt && field.ElementType != ClrElementType.Pointer && field.ElementType != ClrElementType.FunctionPointer)
                throw new InvalidOperationException($"Field '{_type.Name}.{fieldName}' is not a pointer.");

            if (IsNull)
                throw new NullReferenceException();

            ulong address = field.GetAddress(Address);
            ulong value;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadPointer(address, out value))
                throw new MemoryReadException(address);

            return new IntPtr((long)value);
        }

        public UIntPtr GetUIntPtr(string fieldName)
        {
            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            if (field.ElementType != ClrElementType.NativeUInt && field.ElementType != ClrElementType.Pointer && field.ElementType != ClrElementType.FunctionPointer)
                throw new InvalidOperationException($"Field '{_type.Name}.{fieldName}' is not a pointer.");

            if (IsNull)
                throw new NullReferenceException();

            ulong address = field.GetAddress(Address);
            ulong value;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadPointer(address, out value))
                throw new MemoryReadException(address);

            return new UIntPtr((ulong)value);
        }

        private ulong GetFieldAddress(string fieldName, ClrElementType element, string typeName)
        {
            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            if (field.ElementType != element)
                throw new InvalidOperationException($"Field '{_type.Name}.{fieldName}' is not of type '{typeName}'.");

            if (IsNull)
                throw new NullReferenceException();

            ulong address = field.GetAddress(Address);
            return address;
        }
        #endregion

        // TODO:  This implementation not finished.
    }
}
