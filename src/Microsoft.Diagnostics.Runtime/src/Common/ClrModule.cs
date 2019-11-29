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
        public abstract ClrAppDomain AppDomain { get; }

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
        public abstract string FileName { get; }

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
        public virtual MetaDataImport MetadataImport => null;

        /// <summary>
        /// The debugging attributes for this module.
        /// </summary>
        public abstract DebuggableAttribute.DebuggingModes DebuggingMode { get; }
        
        public abstract IEnumerable<(ulong, uint)> EnumerateMethodTables();
        public abstract ClrType ResolveToken(uint typeDefOrRefToken);

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
    }
}