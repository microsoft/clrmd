using System;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents an object in the target process.
    /// </summary>
    public struct ClrObject
    {
        private ulong _address;
        private ClrType _type;

        internal static ClrObject Create(ulong address, ClrType type)
        {
            ClrObject obj = new ClrObject();
            obj._address = address;
            obj._type = type;
            return obj;
        }

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

        /// <summary>
        /// Gets the size of the object.
        /// </summary>
        public ulong Size { get { return _type.GetSize(Address); } }

        #region GetField
        /// <summary>
        /// Gets the given object reference field from this ClrObject.  Throws ArgumentException if the given field does
        /// not exist in the object.  Throws NullReferenceException if IsNull is true.
        /// </summary>
        /// <param name="fieldName">The name of the field to retrieve.</param>
        /// <returns>A ClrObject of the given field.</returns>
        public ClrObject GetObject(string fieldName)
        {
            if (IsNull)
                throw new NullReferenceException();

            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            if (!field.IsObjectReference)
                throw new ArgumentException($"Field '{_type.Name}.{fieldName}' is not an object reference.");

            ClrHeap heap = _type.Heap;
            
            ulong addr = field.GetAddress(_address);
            ulong obj;
            if (!heap.ReadPointer(addr, out obj))
                throw new MemoryReadException(addr);

            ClrType type = heap.GetObjectType(obj);
            return new ClrObject(obj, type);
        }

        /// <summary>
        /// Gets the given field in this object.
        /// </summary>
        /// <param name="fieldName">The name of the field.</param>
        /// <returns>The value of the field.</returns>
        public ClrValue GetField(string fieldName)
        {
            if (IsNull)
                throw new NullReferenceException();

            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            ulong addr = Address;
            ulong fieldAddr = field.GetAddress(addr);
            if (fieldAddr == 0)
                throw new MemoryReadException(addr);

            return new ClrValueImpl(_type.Heap.Runtime, fieldAddr, field);
        }

        /// <summary>
        /// Gets a boolean field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        public bool GetBoolean(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Boolean, "bool");
            bool result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadBoolean(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        /// <summary>
        /// Gets a byte field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        public byte GetByte(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt8, "byte");
            byte result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadByte(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        /// <summary>
        /// Gets a signed byte field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        public sbyte GetSByte(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Int8, "sbyte");
            sbyte result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadByte(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        /// <summary>
        /// Gets a character field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        public char GetChar(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Char, "char");
            char result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadChar(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets a short field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        public short GetInt16(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Int16, "short");
            short result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadShort(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets an unsigned short field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        public ushort GetUInt16(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt16, "ushort");
            ushort result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadShort(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets a int field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        public int GetInt32(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Int32, "int");
            int result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadDword(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        /// <summary>
        /// Gets a uint field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        public uint GetUInt32(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt32, "uint");
            uint result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadDword(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets a long field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        public long GetInt64(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Int64, "long");
            long result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadQword(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets a ulong field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        public ulong GetUInt64(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt64, "ulong");
            ulong result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadQword(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets a float field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        public float GetFloat(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Float, "float");
            float result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadFloat(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets a double field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
        public double GetDouble(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Double, "double");
            double result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadFloat(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets a string field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
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


        /// <summary>
        /// Gets a pointer field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
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


        /// <summary>
        /// Gets an unsigned pointer field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw a NullReferenceException if the target object is null (that is,
        /// if (IsNull returns true).  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field.</returns>
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
            if (IsNull)
                throw new NullReferenceException();

            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            if (field.ElementType != element)
                throw new InvalidOperationException($"Field '{_type.Name}.{fieldName}' is not of type '{typeName}'.");

            ulong address = field.GetAddress(Address);
            return address;
        }
        #endregion


        #region FieldOrNull
        /// <summary>
        /// Gets an object reference field from ClrObject.  Any field which is a subclass of System.Object
        /// </summary>
        /// <param name="fieldName">The name of the field to retrieve.</param>
        /// <returns></returns>
        public ClrObject GetObjectOrNull(string fieldName)
        {
            if (IsNull)
                return new ClrObject(0, Type.Heap.NullType);

            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            if (!field.IsObjectReference)
                throw new ArgumentException($"Field '{_type.Name}.{fieldName}' is not an object reference.");

            ClrHeap heap = _type.Heap;

            ulong addr = field.GetAddress(_address);
            ulong obj;

            if (!heap.ReadPointer(addr, out obj))
                throw new MemoryReadException(addr);

            ClrType type = heap.GetObjectType(obj);
            return new ClrObject(obj, type);
        }

        /// <summary>
        /// Gets the given field in this object
        /// </summary>
        /// <param name="fieldName">The name of the field.</param>
        /// <returns>The value of the field.</returns>
        public ClrValue GetFieldOrNull(string fieldName)
        {
            if (IsNull)
                return new ClrValueImpl(Type.Heap);

            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            ulong addr = Address;
            ulong fieldAddr = field.GetAddress(addr);
            if (fieldAddr == 0)
                throw new MemoryReadException(addr);

            return new ClrValueImpl(_type.Heap.Runtime, fieldAddr, field);
        }

        /// <summary>
        /// Gets a boolean field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field or null if this object points to a null value.</returns>
        public bool? GetBooleanOrNull(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.Boolean, "bool");
            bool result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadBoolean(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        /// <summary>
        /// Gets a byte field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field or null if this object points to a null value.</returns>
        public byte? GetByteOrNull(string fieldName)
        {
            if (IsNull)
                return null;

            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt8, "byte");
            byte result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadByte(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        /// <summary>
        /// Gets a signed byte field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field or null if this object points to a null value.</returns>
        public sbyte? GetSByteOrNull(string fieldName)
        {
            if (IsNull)
                return null;

            ulong address = GetFieldAddress(fieldName, ClrElementType.Int8, "sbyte");
            sbyte result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadByte(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        /// <summary>
        /// Gets a character field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field or null if this object points to a null value.</returns>
        public char? GetCharOrNull(string fieldName)
        {
            if (IsNull)
                return null;

            ulong address = GetFieldAddress(fieldName, ClrElementType.Char, "char");
            char result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadChar(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets a short field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field or null if this object points to a null value.</returns>
        public short? GetInt16OrNull(string fieldName)
        {
            if (IsNull)
                return null;

            ulong address = GetFieldAddress(fieldName, ClrElementType.Int16, "short");
            short result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadShort(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets an unsigned short field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.   It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field or null if this object points to a null value.</returns>
        public ushort? GetUInt16OrNull(string fieldName)
        {
            if (IsNull)
                return null;

            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt16, "ushort");
            ushort result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadShort(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets a int field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field or null if this object points to a null value.</returns>
        public int? GetInt32OrNull(string fieldName)
        {
            if (IsNull)
                return null;

            ulong address = GetFieldAddress(fieldName, ClrElementType.Int32, "int");
            int result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadDword(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        /// <summary>
        /// Gets a uint field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field or null if this object points to a null value.</returns>
        public uint? GetUInt32OrNull(string fieldName)
        {
            if (IsNull)
                return null;

            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt32, "uint");
            uint result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadDword(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets a long field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.    It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field or null if this object points to a null value.</returns>
        public long? GetInt64OrNull(string fieldName)
        {
            if (IsNull)
                return null;

            ulong address = GetFieldAddress(fieldName, ClrElementType.Int64, "long");
            long result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadQword(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets a ulong field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field or null if this object points to a null value.</returns>
        public ulong? GetUInt64OrNull(string fieldName)
        {
            if (IsNull)
                return null;

            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt64, "ulong");
            ulong result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadQword(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets a float field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field or null if this object points to a null value.</returns>
        public float? GetFloatOrNull(string fieldName)
        {
            if (IsNull)
                return null;

            ulong address = GetFieldAddress(fieldName, ClrElementType.Float, "float");
            float result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadFloat(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets a double field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field or null if this object points to a null value.</returns>
        public double? GetDoubleOrNull(string fieldName)
        {
            if (IsNull)
                return null;

            ulong address = GetFieldAddress(fieldName, ClrElementType.Double, "double");
            double result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadFloat(address, out result))
                throw new MemoryReadException(address);

            return result;
        }


        /// <summary>
        /// Gets a string field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field or null if this object points to a null value.</returns>
        public string GetStringOrNull(string fieldName)
        {
            if (IsNull)
                return null;

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


        /// <summary>
        /// Gets a pointer field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field or null if this object points to a null value.</returns>
        public IntPtr GetIntPtrOrZero(string fieldName)
        {
            if (IsNull)
                return IntPtr.Zero;

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


        /// <summary>
        /// Gets an unsigned pointer field from the object.  Note that the type must match exactly, as this method
        /// will not do type coercion.  This method will throw an ArgumentException if no field matches
        /// the given name.  It will throw an InvalidOperationException if the field is not
        /// of the correct type.  Lastly, it will throw a MemoryReadException if there was an error reading
        /// the value of this field out of the data target.
        /// </summary>
        /// <param name="fieldName">The name of the field to get the value for.</param>
        /// <returns>The value of the given field or null if this object points to a null value.</returns>
        public UIntPtr GetUIntPtrOrZero(string fieldName)
        {
            if (IsNull)
                return UIntPtr.Zero;

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
        #endregion
    }
}
