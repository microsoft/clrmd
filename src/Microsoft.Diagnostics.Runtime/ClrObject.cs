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
                throw new ArgumentException(string.Format("Type '{0}' does not contain a field named '{1}'", _type.Name, fieldName));

            if (!field.IsObjectReference)
                throw new ArgumentException(string.Format("Field '{0}.{1}' is not an object reference.", _type.Name, fieldName));
            
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
        /// <param name="name">The name of the field.</param>
        /// <returns>The value of the field.</returns>
        public ClrValue GetField(string name)
        {
            ClrInstanceField field = _type.GetFieldByName(name);
            if (field == null)
                throw new ArgumentException(string.Format("Type '{0}' does not contain a field named '{1}'", _type.Name, fieldName));
            
            ulong addr = Address;
            ulong fieldAddr = field.GetAddress(addr);
            if (fieldAddr == 0)
                throw new MemoryReadException(addr);

            return new ClrValueImpl(fieldAddr, field);
        }

        // TODO:  This implementation not finished.
    }
}
