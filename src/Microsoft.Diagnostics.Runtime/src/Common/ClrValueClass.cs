using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents an instance of a type which inherits from System.ValueClass
    /// </summary>
    public struct ClrValueClass
    {
        private ulong _address;
        private bool _interior;
        private ClrType _type;

        /// <summary>
        /// The address of the object.
        /// </summary>
        public ulong Address { get { return _address; } }

        /// <summary>
        /// The address of the object in Hex format.
        /// </summary>
        public string HexAddress { get { return _address.ToString("x"); } }

        /// <summary>
        /// The type of the object.
        /// </summary>
        public ClrType Type { get { return _type; } }

        internal ClrValueClass(ulong address, ClrType type, bool interior)
        {
            _address = address;
            _type = type;
            _interior = interior;

            Debug.Assert(type.IsValueClass);
        }

        #region GetField
        /// <summary>
        /// Gets the given object reference field from this ClrObject.  Throws ArgumentException if the given field does
        /// not exist in the object.  Throws NullReferenceException if IsNull is true.
        /// </summary>
        /// <param name="fieldName">The name of the field to retrieve.</param>
        /// <returns>A ClrObject of the given field.</returns>
        public ClrObject GetObjectField(string fieldName)
        {
            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            if (!field.IsObjectReference)
                throw new ArgumentException($"Field '{_type.Name}.{fieldName}' is not an object reference.");

            ClrHeap heap = _type.Heap;

            ulong addr = field.GetAddress(_address, _interior);
            if (!heap.ReadPointer(addr, out ulong obj))
                throw new MemoryReadException(addr);

            ClrType type = heap.GetObjectType(obj);
            return new ClrObject(obj, type);
        }

        /// <summary>
        /// Gets the value of a primitive field.  This will throw an InvalidCastException if the type parameter
        /// does not match the field's type.
        /// </summary>
        /// <typeparam name="T">The type of the field itself.</typeparam>
        /// <param name="fieldName">The name of the field.</param>
        /// <returns>The value of this field.</returns>
        public T GetField<T>(string fieldName) where T : struct
        {
            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            object value = field.GetValue(_address, _interior);
            return (T)value;
        }

        /// <summary>
        /// </summary>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public ClrValueClass GetValueClassField(string fieldName)
        {
            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            if (!field.IsValueClass)
                throw new ArgumentException($"Field '{_type.Name}.{fieldName}' is not a ValueClass.");

            if (field.Type == null)
                throw new Exception("Field does not have an associated class.");

            ClrHeap heap = _type.Heap;

            ulong addr = field.GetAddress(_address, _interior);
            return new ClrValueClass(addr, field.Type, true);
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
        public string GetStringField(string fieldName)
        {
            ulong address = GetFieldAddress(fieldName, ClrElementType.String, "string");
            RuntimeBase runtime = (RuntimeBase)_type.Heap.Runtime;

            if (!runtime.ReadPointer(address, out ulong str))
                throw new MemoryReadException(address);
            
            if (str == 0)
                return null;

            if (!runtime.ReadString(str, out string result))
                throw new MemoryReadException(str);

            return result;
        }

        private ulong GetFieldAddress(string fieldName, ClrElementType element, string typeName)
        {
            ClrInstanceField field = _type.GetFieldByName(fieldName);
            if (field == null)
                throw new ArgumentException($"Type '{_type.Name}' does not contain a field named '{fieldName}'");

            if (field.ElementType != element)
                throw new InvalidOperationException($"Field '{_type.Name}.{fieldName}' is not of type '{typeName}'.");

            ulong address = field.GetAddress(_address, _interior);
            return address;
        }
        #endregion

    }
}
