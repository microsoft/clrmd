// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.Interfaces;
using FieldInfo = Microsoft.Diagnostics.Runtime.AbstractDac.FieldInfo;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents an instance field of a type.   Fundamentally it represents a name and a type
    /// </summary>
    public sealed class ClrInstanceField : ClrField, IClrInstanceField
    {
        internal ClrInstanceField(ClrType containingType, ClrType? type, IAbstractTypeHelpers helpers, in FieldInfo data)
            : base(containingType, type, helpers, data)
        {
        }

        /// <summary>
        /// Reads the value of the field as an unmanaged struct or primitive type.
        /// </summary>
        /// <typeparam name="T">An unmanaged struct or primitive type.</typeparam>
        /// <param name="objRef">The object to read the instance field from.</param>
        /// <param name="interior">Whether or not the field is interior to a struct.</param>
        /// <returns>The value read.</returns>
        public T Read<T>(ulong objRef, bool interior) where T : unmanaged
        {
            ulong address = GetAddress(objRef, interior);
            if (address == 0)
                return default;

            if (!ContainingType.Module.DataReader.Read(address, out T value))
                return default;

            return value;
        }

        /// <summary>
        /// Reads the value of an object field.
        /// </summary>
        /// <param name="objRef">The object to read the instance field from.</param>
        /// <param name="interior">Whether or not the field is interior to a struct.</param>
        /// <returns>The value read.</returns>
        public ClrObject ReadObject(ulong objRef, bool interior)
        {
            ulong address = GetAddress(objRef, interior);
            if (address == 0 || !ContainingType.Module.DataReader.ReadPointer(address, out ulong obj) || obj == 0)
                return default;

            return ContainingType.Heap.GetObject(obj);
        }

        IClrValue IClrInstanceField.ReadObject(ulong objRef, bool interior) => ReadObject(objRef, interior);

        /// <summary>
        /// Reads a ValueType struct from the instance field.
        /// </summary>
        /// <param name="objRef">The object to read the instance field from.</param>
        /// <param name="interior">Whether or not the field is interior to a struct.</param>
        /// <returns>The value read.</returns>
        public ClrValueType ReadStruct(ulong objRef, bool interior)
        {
            ulong address = GetAddress(objRef, interior);
            if (address == 0)
                return default;

            return new ClrValueType(address, Type, interior: true);
        }

        IClrValue IClrInstanceField.ReadStruct(ulong objRef, bool interior) => ReadStruct(objRef, interior);

        /// <summary>
        /// Reads a string from the instance field.
        /// </summary>
        /// <param name="objRef">The object to read the instance field from.</param>
        /// <param name="interior">Whether or not the field is interior to a struct.</param>
        /// <returns>The value read.</returns>
        public string? ReadString(ulong objRef, bool interior)
        {
            ClrObject obj = ReadObject(objRef, interior);
            if (obj.IsNull)
                return null;

            return obj.AsString();
        }

        /// <summary>
        /// Returns the address of the value of this field.  Equivalent to GetFieldAddress(objRef, false).
        /// </summary>
        /// <param name="objRef">The object to get the field address for.</param>
        /// <returns>The value of the field.</returns>
        public ulong GetAddress(ulong objRef)
        {
            return GetAddress(objRef, false);
        }

        /// <summary>
        /// Returns the address of the value of this field.  Equivalent to GetFieldAddress(objRef, false).
        /// </summary>
        /// <param name="objRef">The object to get the field address for.</param>
        /// <param name="interior">
        /// Whether the enclosing type of this field is a value class,
        /// and that value class is embedded in another object.
        /// </param>
        /// <returns>The value of the field.</returns>
        public ulong GetAddress(ulong objRef, bool interior)
        {
            if (interior)
                return objRef + (ulong)Offset;

            return objRef + (ulong)(Offset + IntPtr.Size);
        }
    }
}