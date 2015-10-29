using System;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A ClrValue represents the value of a field or variable.  This value may be of any type (or null).
    /// </summary>
    public abstract class ClrValue
    {
        private readonly RuntimeBase _runtime;

        internal ClrValue(ClrRuntime runtime)
        {
            _runtime = (RuntimeBase)runtime;
        }

        internal ClrValue(RuntimeBase runtime)
        {
            _runtime = runtime;
        }

        /// <summary>
        /// Returns the size of the value.
        /// </summary>
        public abstract int Size { get; }

        /// <summary>
        /// Returns the address of the value.
        /// </summary>
        public abstract ulong Address { get; }

        /// <summary>
        /// Returns the result of ReadPointer(Address) if this value is an object.
        /// </summary>
        protected abstract ulong Object { get; }

        /// <summary>
        /// Returns whether Address is an interior value.
        /// </summary>
        public abstract bool Interior { get; }

        /// <summary>
        /// Returns whether this value is null (or otherwise cannot be accessed).
        /// </summary>
        public virtual bool IsNull { get { return Address == 0 || (ClrRuntime.IsObjectReference(ElementType) && Object == 0); } }

        /// <summary>
        /// Returns the element type of this value.
        /// </summary>
        public abstract ClrElementType ElementType { get; }
        
        /// <summary>
        /// The runtime associated with this value.
        /// </summary>
        public virtual ClrRuntime Runtime { get { return _runtime; } }

        /// <summary>
        /// Returns the type of this value.
        /// </summary>
        public virtual ClrType Type
        {
            get
            {
                HeapBase heap = _runtime.HeapBase;
                ClrElementType el = ElementType;

                if (ClrRuntime.IsPrimitive(el))
                    return heap.GetBasicType(el);

                if (ClrRuntime.IsObjectReference(el))
                    return AsObject().Type;

                return GetStructElementType();
            }
        }

        /// <summary>
        /// Obtains the element type when ElementType is a struct.
        /// </summary>
        /// <returns>The ClrType of this value, or ClrHeap.ErrorType if it could not be obtained.</returns>
        protected abstract ClrType GetStructElementType();

        #region Converters

        /// <summary>
        /// Returns a ClrObject for this value.
        /// </summary>
        /// <returns>A ClrObject for this value.  If the value is null, then ClrObject.IsNull will the true and ClrObject.Type
        /// will equal ClrHeap.ErrorType.</returns>
        public virtual ClrObject AsObject()
        {
            if (!ClrRuntime.IsObjectReference(ElementType))
                throw new InvalidOperationException("Value is not an object.");

            if (IsNull)
                throw new NullReferenceException();

            ClrHeap heap = Runtime.GetHeap();

            ulong obj = Object;
            return new ClrObject(obj, obj != 0 ? heap.GetObjectType(obj) : heap.NullType);
        }

        /// <summary>
        /// Converts the value to a boolean.  Note that this method is a conversion, not a cast.  Similarly sized data will be converted.
        /// </summary>
        /// <returns>This value as a boolean.</returns>
        public virtual bool AsBoolean()
        {
            if (ElementType != ClrElementType.Boolean && ElementType != ClrElementType.Int8 && ElementType != ClrElementType.UInt8)
                throw new InvalidOperationException("Value is not a boolean.");

            bool result;
            if (!_runtime.ReadBoolean(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }


        /// <summary>
        /// Converts the value to a byte.  Note that this method is a conversion, not a cast.  Similarly sized data will be converted.
        /// </summary>
        /// <returns>This value as a byte.</returns>
        public virtual byte AsByte()
        {
            if (ElementType != ClrElementType.Boolean && ElementType != ClrElementType.Int8 && ElementType != ClrElementType.UInt8)
                throw new InvalidOperationException("Value is not a byte.");

            byte result;
            if (!_runtime.ReadByte(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }


        /// <summary>
        /// Converts the value to a sbyte.  Note that this method is a conversion, not a cast.  Similarly sized data will be converted.
        /// </summary>
        /// <returns>This value as a sbyte.</returns>
        public virtual sbyte AsSByte()
        {
            if (ElementType != ClrElementType.Boolean && ElementType != ClrElementType.Int8 && ElementType != ClrElementType.UInt8)
                throw new InvalidOperationException("Value is not a byte.");

            sbyte result;
            if (!_runtime.ReadByte(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }


        /// <summary>
        /// Converts the value to a char.  Note that this method is a conversion, not a cast.  Similarly sized data will be converted.
        /// </summary>
        /// <returns>This value as a char.</returns>
        public virtual char AsChar()
        {
            if (ElementType != ClrElementType.Char && ElementType != ClrElementType.Int16 && ElementType != ClrElementType.UInt16)
                throw new InvalidOperationException("Value is not a char.");

            char result;
            if (!_runtime.ReadChar(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }



        /// <summary>
        /// Converts the value to a short.  Note that this method is a conversion, not a cast.  Similarly sized data will be converted.
        /// </summary>
        /// <returns>This value as a boolean.</returns>
        public virtual short AsInt16()
        {
            if (ElementType != ClrElementType.Char && ElementType != ClrElementType.Int16 && ElementType != ClrElementType.UInt16)
                throw new InvalidOperationException("Value is not a short.");

            short result;
            if (!_runtime.ReadShort(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }



        /// <summary>
        /// Converts the value to a ushort.  Note that this method is a conversion, not a cast.  Similarly sized data will be converted.
        /// </summary>
        /// <returns>This value as a boolean.</returns>
        public virtual ushort AsUInt16()
        {
            if (ElementType != ClrElementType.Char && ElementType != ClrElementType.Int16 && ElementType != ClrElementType.UInt16)
                throw new InvalidOperationException("Value is not a short.");

            ushort result;
            if (!_runtime.ReadShort(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }


        /// <summary>
        /// Converts the value to an int.  Note that this method is a conversion, not a cast.  Similarly sized data will be converted.
        /// </summary>
        /// <returns>This value as an int.</returns>
        public virtual int AsInt32()
        {
            if (ElementType != ClrElementType.Int32 && ElementType != ClrElementType.UInt32)
                throw new InvalidOperationException("Value is not an integer.");

            int result;
            if (!_runtime.ReadDword(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }


        /// <summary>
        /// Converts the value to a uint.  Note that this method is a conversion, not a cast.  Similarly sized data will be converted.
        /// </summary>
        /// <returns>This value as a uint.</returns>
        public virtual uint AsUInt32()
        {
            if (ElementType != ClrElementType.Int32 && ElementType != ClrElementType.UInt32)
                throw new InvalidOperationException("Value is not an integer.");

            uint result;
            if (!_runtime.ReadDword(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }


        /// <summary>
        /// Converts the value to a ulong.  Note that this method is a conversion, not a cast.  Similarly sized data will be converted.
        /// </summary>
        /// <returns>This value as a ulong.</returns>
        public virtual ulong AsUInt64()
        {
            if (ElementType != ClrElementType.UInt64 && ElementType != ClrElementType.Int64)
                throw new InvalidOperationException("Value is not a long.");

            ulong result;
            if (!_runtime.ReadQword(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }



        /// <summary>
        /// Converts the value to a long.  Note that this method is a conversion, not a cast.  Similarly sized data will be converted.
        /// </summary>
        /// <returns>This value as a long.</returns>
        public virtual long AsInt64()
        {
            if (ElementType != ClrElementType.UInt64 && ElementType != ClrElementType.Int64)
                throw new InvalidOperationException("Value is not a long.");

            long result;
            if (!_runtime.ReadQword(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }


        /// <summary>
        /// Converts the value to a float.
        /// </summary>
        /// <returns>This value as a float.</returns>
        public virtual float AsFloat()
        {
            if (ElementType != ClrElementType.Float)
                throw new InvalidOperationException("Value is not a float.");

            float result;
            if (!_runtime.ReadFloat(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }



        /// <summary>
        /// Converts the value to a double.
        /// </summary>
        /// <returns>This value as a double.</returns>
        public virtual double AsDouble()
        {
            if (ElementType != ClrElementType.Double)
                throw new InvalidOperationException("Value is not a double.");

            double result;
            if (!_runtime.ReadFloat(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }


        /// <summary>
        /// Converts the value to a raw string.  This method will throw an InvalidOperationException if the target value is not a string.
        /// </summary>
        /// <returns>The string contents of this value.</returns>
        public virtual string AsString()
        {
            if (ElementType != ClrElementType.String && (!ClrRuntime.IsObjectReference(ElementType) || !Type.IsString))
                throw new InvalidOperationException("Value is not a string.");

            ulong str;
            if (!_runtime.ReadPointer(Address, out str))
                throw new MemoryReadException(Address);

            if (str == 0)
                return null;

            string result;
            if (!_runtime.ReadString(str, out result))
                throw new MemoryReadException(str);

            return result;
        }


        /// <summary>
        /// Converts the value to an IntPtr.  Note that this method is a conversion, not a cast.  Similarly sized data will be converted.
        /// </summary>
        /// <returns>This value as an IntPtr.</returns>
        public virtual IntPtr AsIntPtr()
        {
            if (ElementType != ClrElementType.Pointer && ElementType != ClrElementType.FunctionPointer && ElementType != ClrElementType.NativeInt && ElementType != ClrElementType.NativeUInt)
                throw new InvalidOperationException("Value is not a pointer.");

            IntPtr result;
            if (!_runtime.ReadPointer(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }


        /// <summary>
        /// Converts the value to a UIntPtr.  Note that this method is a conversion, not a cast.  Similarly sized data will be converted.
        /// </summary>
        /// <returns>This value as a UIntPtr.</returns>
        public virtual UIntPtr AsUIntPtr()
        {
            if (ElementType != ClrElementType.Pointer && ElementType != ClrElementType.FunctionPointer && ElementType != ClrElementType.NativeInt && ElementType != ClrElementType.NativeUInt)
                throw new InvalidOperationException("Value is not a pointer.");

            UIntPtr result;
            if (!_runtime.ReadPointer(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }
        #endregion

        #region Field Access

        /// <summary>
        /// Gets an object reference field from ClrObject.  Any field which is a subclass of System.Object
        /// </summary>
        /// <param name="fieldName">The name of the field to retrieve.</param>
        /// <returns></returns>
        public ClrObject GetObject(string fieldName)
        {
            if (IsNull)
                throw new NullReferenceException();

            ClrType type = Type;
            ClrInstanceField field = type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{type.Name}' does not contain a field named '{fieldName}'");

            if (!field.IsObjectReference)
                throw new ArgumentException($"Field '{type.Name}.{fieldName}' is not an object reference.");

            ClrHeap heap = Type.Heap;

            ulong addr = ClrRuntime.IsObjectReference(ElementType) ? Object : Address;
            addr = field.GetAddress(addr, Interior);
            ulong obj;

            if (!heap.ReadPointer(addr, out obj))
                throw new MemoryReadException(addr);
            
            return new ClrObject(obj, heap.GetObjectType(obj));
        }

        /// <summary>
        /// Retrieves a field from this value.
        /// </summary>
        /// <param name="name">The name of the field.</param>
        /// <returns>A ClrValue representing this field.</returns>
        public virtual ClrValue GetField(string name)
        {
            ClrElementType el = ElementType;
            if (ClrRuntime.IsPrimitive(el))
            {
                // Primitives only have one field, named m_value.
                if (name != "m_value")
                    throw new ArgumentException(string.Format("Field '{0}' does not exist in type '{1}'.", name, Type.Name));

                // Getting m_value is the same as this ClrValue...
                return this;
            }

            if (ClrRuntime.IsObjectReference(el) || !Interior)
                return AsObject().GetField(name);

            Debug.Assert(ClrRuntime.IsValueClass(el));

            ulong address = Address;
            if (address == 0)
                throw new NullReferenceException();

            ClrType type = Type;
            ClrInstanceField field = type.GetFieldByName(name);
            if (field == null)
                throw new ArgumentException(string.Format("Field '{0}' does not exist in type '{1}'.", name, Type.Name));

            ulong result = field.GetAddress(address, Interior);
            return new ClrValueImpl(_runtime, result, field);
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
            ClrType type;
            ulong address = GetFieldAddress(fieldName, ClrElementType.Boolean, "bool", out type);
            bool result;
            if (!((RuntimeBase)type.Heap.Runtime).ReadBoolean(address, out result))
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
            ClrType type;
            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt8, "byte", out type);
            byte result;
            if (!((RuntimeBase)type.Heap.Runtime).ReadByte(address, out result))
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
            ClrType type;
            ulong address = GetFieldAddress(fieldName, ClrElementType.Int8, "sbyte", out type);
            sbyte result;
            if (!((RuntimeBase)type.Heap.Runtime).ReadByte(address, out result))
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
            ClrType type;
            ulong address = GetFieldAddress(fieldName, ClrElementType.Char, "char", out type);
            char result;
            if (!((RuntimeBase)type.Heap.Runtime).ReadChar(address, out result))
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
            ClrType type;
            ulong address = GetFieldAddress(fieldName, ClrElementType.Int16, "short", out type);
            short result;
            if (!((RuntimeBase)type.Heap.Runtime).ReadShort(address, out result))
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
            ClrType type;
            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt16, "ushort", out type);
            ushort result;
            if (!((RuntimeBase)type.Heap.Runtime).ReadShort(address, out result))
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
            ClrType type;
            ulong address = GetFieldAddress(fieldName, ClrElementType.Int32, "int", out type);
            int result;
            if (!((RuntimeBase)type.Heap.Runtime).ReadDword(address, out result))
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
            ClrType type;
            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt32, "uint", out type);
            uint result;
            if (!((RuntimeBase)type.Heap.Runtime).ReadDword(address, out result))
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
            ClrType type;
            ulong address = GetFieldAddress(fieldName, ClrElementType.Int64, "long", out type);
            long result;
            if (!((RuntimeBase)type.Heap.Runtime).ReadQword(address, out result))
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
            ClrType type;
            ulong address = GetFieldAddress(fieldName, ClrElementType.UInt64, "ulong", out type);
            ulong result;
            if (!((RuntimeBase)type.Heap.Runtime).ReadQword(address, out result))
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
            ClrType type;
            ulong address = GetFieldAddress(fieldName, ClrElementType.Float, "float", out type);
            float result;
            if (!((RuntimeBase)type.Heap.Runtime).ReadFloat(address, out result))
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
            ClrType type;
            ulong address = GetFieldAddress(fieldName, ClrElementType.Double, "double", out type);
            double result;
            if (!((RuntimeBase)type.Heap.Runtime).ReadFloat(address, out result))
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
            ClrType type;
            ulong address = GetFieldAddress(fieldName, ClrElementType.String, "string", out type);
            ulong str;
            RuntimeBase runtime = (RuntimeBase)type.Heap.Runtime;

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
            ClrType type = Type;
            ClrInstanceField field = type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{type.Name}' does not contain a field named '{fieldName}'");

            if (field.ElementType != ClrElementType.NativeInt && field.ElementType != ClrElementType.Pointer && field.ElementType != ClrElementType.FunctionPointer)
                throw new InvalidOperationException($"Field '{type.Name}.{fieldName}' is not a pointer.");

            if (IsNull)
                throw new NullReferenceException();

            ulong address = field.GetAddress(Address);
            ulong value;
            if (!((RuntimeBase)type.Heap.Runtime).ReadPointer(address, out value))
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
            ClrType type = Type;
            ClrInstanceField field = type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{type.Name}' does not contain a field named '{fieldName}'");

            if (field.ElementType != ClrElementType.NativeUInt && field.ElementType != ClrElementType.Pointer && field.ElementType != ClrElementType.FunctionPointer)
                throw new InvalidOperationException($"Field '{type.Name}.{fieldName}' is not a pointer.");

            if (IsNull)
                throw new NullReferenceException();

            ulong address = field.GetAddress(Address);
            ulong value;
            if (!((RuntimeBase)type.Heap.Runtime).ReadPointer(address, out value))
                throw new MemoryReadException(address);

            return new UIntPtr((ulong)value);
        }


        private ulong GetFieldAddress(string fieldName, ClrElementType element, string typeName, out ClrType type)
        {
            if (IsNull)
                throw new NullReferenceException();

            type = Type;
            ClrInstanceField field = type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{type.Name}' does not contain a field named '{fieldName}'");

            if (field.ElementType != element)
                throw new InvalidOperationException($"Field '{type.Name}.{fieldName}' is not of type '{typeName}'.");

            ulong address = ClrRuntime.IsObjectReference(ElementType) ? Object : Address;
            address = field.GetAddress(address, Interior);
            return address;
        }
        #endregion

        /// <summary>
        /// ToString implementation.
        /// </summary>
        /// <returns>A string value of this type.</returns>
        public override string ToString()
        {
            ClrElementType element = ElementType;

            if (element == ClrElementType.String)
                return AsString();

            if (ClrRuntime.IsObjectReference(element))
                return AsObject().Address.ToString("x");

            if (ClrRuntime.IsValueClass(ElementType))
                return $"{Type.Name} @{Address:x}";

            switch (element)
            {
                case ClrElementType.Boolean:
                    return AsBoolean().ToString();

                case ClrElementType.Int8:
                case ClrElementType.UInt8:
                    return AsByte().ToString();

                case ClrElementType.Char:
                    return AsChar().ToString();

                case ClrElementType.UInt16:
                case ClrElementType.Int16:
                    return AsInt16().ToString();

                case ClrElementType.Int32:
                case ClrElementType.UInt32:
                    return AsInt32().ToString();

                case ClrElementType.Int64:
                case ClrElementType.UInt64:
                    return AsInt64().ToString();

                case ClrElementType.Pointer:
                case ClrElementType.FunctionPointer:
                case ClrElementType.NativeInt:
                    return AsIntPtr().ToInt64().ToString("x");

                case ClrElementType.NativeUInt:
                    return AsUIntPtr().ToUInt64().ToString("x");

                case ClrElementType.Float:
                    return AsFloat().ToString();

                case ClrElementType.Double:
                    return AsDouble().ToString();

                default:
                    throw new NotImplementedException();
            }
        }
    }

    internal class ClrValueImpl : ClrValue
    {
        private ClrType _type;
        private ulong _address;
        private ulong _obj;
        private bool _interior;

        protected override ulong Object
        {
            get
            {
                return _obj;
            }
        }

        public override int Size
        {
            get
            {
                return _type.IsObjectReference ? (int)_type.GetSize(_obj) : _type.BaseSize;
            }
        }

        public override ulong Address
        {
            get
            {
                return _address;
            }
        }

        public override bool Interior
        {
            get
            {
                return _interior;
            }
        }

        public override ClrElementType ElementType
        {
            get
            {
                return _type.ElementType;
            }
        }

        public ClrValueImpl(ClrRuntime runtime, ulong address, ClrInstanceField field)
            : base(runtime)
        {
            _address = address;
            _type = field.Type;
            _interior = field.IsValueClass;

            if (_type.IsObjectReference)
            {
                var heap = _type.Heap;
                if (!heap.ReadPointer(address, out _obj))
                    throw new MemoryReadException(address);

                _type = heap.GetObjectType(_obj);
            }
        }

        public ClrValueImpl(ClrHeap heap)
            : base(heap.Runtime)
        {
            _address = 0;
            _type = heap.NullType;
        }

        public override ClrObject AsObject()
        {
            if (Interior)
                throw new InvalidOperationException("Value is an interior pointer, not an object.");

            return new ClrObject(_obj, _type);
        }

        public override ClrType Type
        {
            get
            {
                return _type;
            }
        }

        protected override ClrType GetStructElementType()
        {
            throw new NotImplementedException();
        }
    }

    internal class CorDebugValue : ClrValue
    {
        ICorDebug.ICorDebugValue _value;
        int? _size;
        ulong? _address;
        ulong? _object;
        ClrElementType _elementType;

        protected override ulong Object
        {
            get
            {
                if (!_object.HasValue)
                {
                    ulong obj;
                    if (!Runtime.ReadPointer(Address, out obj))
                        throw new MemoryReadException(Address);

                    _object = obj;
                }

                return _object.Value;
            }
        }

        public override int Size
        {
            get
            {
                if (!_size.HasValue)
                {
                    uint size;
                    _value.GetSize(out size);
                    _size = (int)size;
                }

                return _size.Value;
            }
        }

        public override bool Interior
        {
            get
            {
                return _elementType == ClrElementType.Struct;
            }
        }

        public override ulong Address
        {
            get
            {
                if (!_address.HasValue)
                {
                    ulong addr;
                    _value.GetAddress(out addr);
                    _address = addr;
                }

                return _address.Value;
            }
        }

        public override ClrElementType ElementType
        {
            get
            {
                return _elementType;
            }
        }

        protected override ClrType GetStructElementType()
        {
            try
            {
                var value = (ICorDebug.ICorDebugValue2)_value;

                ICorDebug.ICorDebugType type;
                value.GetExactType(out type);

                ICorDebug.ICorDebugClass cls;
                type.GetClass(out cls);

                uint token;
                cls.GetToken(out token);

                ICorDebug.ICorDebugModule module;
                cls.GetModule(out module);

                ulong imageBase;
                module.GetBaseAddress(out imageBase);

                ClrModule clrModule = Runtime.Modules.Where(m => m.ImageBase == imageBase).SingleOrDefault();
                if (clrModule != null)
                    return clrModule.GetTypeByToken(token);
            }
            catch
            {
            }

            return Runtime.GetHeap().ErrorType;
        }

        public CorDebugValue(RuntimeBase runtime, ICorDebug.ICorDebugValue value)
            : base(runtime)
        {
            _value = value;

            ICorDebug.CorElementType el;
            value.GetType(out el);
            _elementType = (ClrElementType)el;
        }
    }
}
