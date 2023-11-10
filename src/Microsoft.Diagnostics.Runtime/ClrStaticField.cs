// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.Interfaces;
using FieldInfo = Microsoft.Diagnostics.Runtime.AbstractDac.FieldInfo;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a static field in the target process.
    /// </summary>
    public sealed class ClrStaticField : ClrField, IClrStaticField
    {
        internal ClrStaticField(ClrType containingType, ClrType? type, IAbstractTypeHelpers helpers, in FieldInfo data)
            : base(containingType, type, helpers, data)
        {
        }

        /// <summary>
        /// Returns whether this static field has been initialized in a particular AppDomain
        /// or not.  If a static variable has not been initialized, then its class constructor
        /// may have not been run yet.  Calling any of the Read* methods on an uninitialized static
        /// will result in returning either NULL or a value of 0.
        /// </summary>
        /// <param name="appDomain">The AppDomain to see if the variable has been initialized.</param>
        /// <returns>
        /// True if the field has been initialized (even if initialized to NULL or a default
        /// value), false if the runtime has not initialized this variable.
        /// </returns>
        public bool IsInitialized(ClrAppDomain appDomain) => GetAddress(appDomain) != 0;


        bool IClrStaticField.IsInitialized(IClrAppDomain appDomain) => GetAddress(appDomain) != 0;

        /// <summary>
        /// Gets the address of the static field's value in memory.
        /// </summary>
        /// <returns>The address of the field's value.</returns>
        public ulong GetAddress(ClrAppDomain appDomain) => _helpers.GetStaticFieldAddress(appDomain.AppDomainInfo, ContainingType.Module.ModuleInfo, ContainingType.TypeInfo, FieldInfo);

        public ulong GetAddress(IClrAppDomain appDomain) => 0;

        /// <summary>
        /// Reads the value of the field as an unmanaged struct or primitive type.
        /// </summary>
        /// <typeparam name="T">An unmanaged struct or primitive type.</typeparam>
        /// <returns>The value read.</returns>
        public T Read<T>(ClrAppDomain appDomain) where T : unmanaged
        {
            ulong address = GetAddress(appDomain);
            if (address == 0)
                return default;

            if (!ContainingType.Module.DataReader.Read(address, out T value))
                return default;

            return value;
        }

        T IClrStaticField.Read<T>(IClrAppDomain appDomain)
        {
            ulong address = GetAddress(appDomain);
            if (address == 0)
                return default;

            if (!ContainingType.Module.DataReader.Read(address, out T value))
                return default;

            return value;
        }

        /// <summary>
        /// Reads the value of an object field.
        /// </summary>
        /// <returns>The value read.</returns>
        public ClrObject ReadObject(ClrAppDomain appDomain)
        {
            ulong address = GetAddress(appDomain);
            if (address == 0 || !ContainingType.Module.DataReader.ReadPointer(address, out ulong obj) || obj == 0)
                return default;

            return ContainingType.Heap.GetObject(obj);
        }

        IClrValue IClrStaticField.ReadObject(IClrAppDomain appDomain)
        {
            ulong address = GetAddress(appDomain);
            if (address == 0 || !ContainingType.Module.DataReader.ReadPointer(address, out ulong obj) || obj == 0)
                return default(ClrObject);

            return ContainingType.Heap.GetObject(obj);
        }

        /// <summary>
        /// Reads a ValueType struct from the instance field.
        /// </summary>
        /// <returns>The value read.</returns>
        public ClrValueType ReadStruct(ClrAppDomain appDomain)
        {
            ulong address = GetAddress(appDomain);
            if (address == 0)
                return default;

            IDataReader dataReader = ContainingType.Module.DataReader;
            if (address == 0 || !dataReader.ReadPointer(address, out ulong obj) || obj == 0)
                return default;

            return new ClrValueType(obj + (uint)dataReader.PointerSize, Type, interior: true);
        }

        IClrValue IClrStaticField.ReadStruct(IClrAppDomain appDomain)
        {
            ulong address = GetAddress(appDomain);
            if (address == 0)
                return default(ClrValueType);

            return new ClrValueType(address, Type, interior: true);
        }

        /// <summary>
        /// Reads a string from the instance field.
        /// </summary>
        /// <returns>The value read.</returns>
        public string? ReadString(ClrAppDomain appDomain)
        {
            ClrObject obj = ReadObject(appDomain);
            if (obj.IsNull)
                return null;

            return obj.AsString();
        }

        string? IClrStaticField.ReadString(IClrAppDomain appDomain)
        {
            IClrStaticField field = this;
            IClrValue obj = field.ReadObject(appDomain);
            if (obj.IsNull)
                return null;

            return obj.AsString();
        }
    }
}