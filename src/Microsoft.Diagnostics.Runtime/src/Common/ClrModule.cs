// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents a managed module in the target process.
    /// </summary>
    public abstract class ClrModule
    {
        /// <summary>
        /// This is the address of the clr!Module object.
        /// </summary>
        public abstract ulong Address { get; }

        /// <summary>
        /// Returns the AppDomain parent of this module.
        /// </summary>
        public abstract ClrAppDomain? AppDomain { get; }

        /// <summary>
        /// Returns the name of the assembly that this module is defined in.
        /// </summary>
        public abstract string AssemblyName { get; }

        /// <summary>
        /// Returns an identifier to uniquely represent this assembly.  This value is not used by any other
        /// function in ClrMD, but can be used to group modules by their assembly.  (Do not use AssemblyName
        /// for this, as reflection and other special assemblies can share the same name, but actually be
        /// different.)
        /// </summary>
        public abstract ulong AssemblyAddress { get; }

        /// <summary>
        /// Returns the name of the module.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Returns true if this module was created through Reflection.Emit (and thus has no associated
        /// file).
        /// </summary>
        public abstract bool IsDynamic { get; }

        /// <summary>
        /// Returns true if this module is an actual PEFile on disk.
        /// </summary>
        public abstract bool IsPEFile { get; }

        /// <summary>
        /// Returns the filename of where the module was loaded from on disk.  Undefined results if
        /// IsPEFile returns false.
        /// </summary>
        public abstract string? FileName { get; }

        /// <summary>
        /// Returns the base of the image loaded into memory.  This may be 0 if there is not a physical
        /// file backing it.
        /// </summary>
        public abstract ulong ImageBase { get; }

        /// <summary>
        /// Returns the size of the image in memory.
        /// </summary>
        public abstract ulong Size { get; }

        /// <summary>
        /// The location of metadata for this module in the process's memory.  This is useful if you
        /// need to manually create IMetaData* objects.
        /// </summary>
        public abstract ulong MetadataAddress { get; }

        /// <summary>
        /// The length of the metadata for this module.
        /// </summary>
        public abstract ulong MetadataLength { get; }

        /// <summary>
        /// The IMetaDataImport interface for this module.  Note that this API does not provide a
        /// wrapper for IMetaDataImport.  You will need to wrap the API yourself if you need to use this.
        /// </summary>
        public virtual MetaDataImport? MetadataImport => null;

        /// <summary>
        /// The debugging attributes for this module.
        /// </summary>
        public abstract DebuggableAttribute.DebuggingModes DebuggingMode { get; }

        /// <summary>
        /// Enumerates the constructed methodtables in this module which correspond to typedef tokens defined by this module.
        /// </summary>
        /// <returns>An enumeration of (ulong methodTable, uint typeDef).</returns>
        public abstract IEnumerable<(ulong, uint)> EnumerateTypeDefToMethodTableMap();

        /// <summary>
        /// Resolves the give metdata token for this module.
        /// </summary>
        /// <param name="typeDefOrRefToken">A typedef or typeref token.</param>
        /// <returns>The ClrType of the resolved token, null if not found or if a type for the token hasn't been constructed by the runtime.</returns>
        public abstract ClrType? ResolveToken(uint typeDefOrRefToken);

        /// <summary>
        /// Attempts to obtain a ClrType based on the name of the type.  Note this is a "best effort" due to
        /// the way that the dac handles types.  This function will fail for Generics, and types which have
        /// never been constructed in the target process.  Please be sure to null-check the return value of
        /// this function.
        /// </summary>
        /// <param name="name">The name of the type.  (This would be the EXACT value returned by ClrType.Name.)</param>
        /// <returns>The requested ClrType, or null if the type doesn't exist or if the runtime hasn't constructed it.</returns>
        public abstract ClrType? GetTypeByName(string name);

        /// <summary>
        /// Returns a name for the assembly.
        /// </summary>
        /// <returns>A name for the assembly.</returns>
        public override string ToString()
        {
            if (string.IsNullOrEmpty(Name))
            {
                if (!string.IsNullOrEmpty(AssemblyName))
                    return AssemblyName;

                if (IsDynamic)
                    return "dynamic";
            }

            return Name;
        }

        /// <summary>
        /// Returns the pdb information for this module.
        /// </summary>
        public abstract PdbInfo Pdb { get; }


        public override bool Equals(object? obj)
        {
            return obj is ClrModule other && Address == other.Address;
        }

        public override int GetHashCode() => Address.GetHashCode();

        public static bool operator ==(ClrModule? left, ClrModule? right)
        {
            if (left is null)
                return right is null;

            return left.Equals(right);
        }

        public static bool operator !=(ClrModule? left, ClrModule? right)
        {
            return !(left == right);
        }
    }
}