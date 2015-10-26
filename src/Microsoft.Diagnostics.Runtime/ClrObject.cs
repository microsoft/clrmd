using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
        public bool GetBooleanField(string fieldName)
        {
            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            if (field.ElementType != ClrElementType.Boolean)
                throw new InvalidOperationException($"Field '{field.Type.Name}.{field.Name}' is not a boolean.");

            if (IsNull)
                throw new NullReferenceException();
            
            ulong address = field.GetAddress(Address);
            bool result;
            if (!((RuntimeBase)_type.Heap.Runtime).ReadBoolean(address, out result))
                throw new MemoryReadException(address);

            return result;
        }

        // TODO:  This implementation not finished.
    }
}
