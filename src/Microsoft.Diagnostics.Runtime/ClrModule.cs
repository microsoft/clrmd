using System;
using System.Collections.Generic;
using System.Diagnostics;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime
{

    /// <summary>
    /// Represents a managed module in the target process.
    /// </summary>
    public abstract class ClrModule
    {
        #region Obsolete
        /// <summary>
        /// Returns true if ClrMD has loaded the the PDB for this module into memory.
        /// </summary>
        [Obsolete("Use Microsoft.Diagnostics.Utilities.Pdb classes instead.")]
        public virtual bool IsPdbLoaded { get { return false; } }

        /// <summary>
        /// Determines whether a PDB on disk matches this module.  (Note that TryDownloadPdb
        /// always provides a matching PDB if it finds one, so you do not need to check pdbs
        /// downloaded with TryDownloadPdb with this function.)
        /// </summary>
        /// <param name="pdbPath">The location of the PDB on disk.</param>
        /// <returns>True if the pdb matches, false otherwise.</returns>
        [Obsolete("Use Microsoft.Diagnostics.Utilities.Pdb classes instead.")]
        public virtual bool IsMatchingPdb(string pdbPath) { return false; }

        /// <summary>
        /// Loads the pdb for this module.
        /// </summary>
        /// <param name="path">The path to the PDB on disk.</param>
        [Obsolete("Use Microsoft.Diagnostics.Utilities.Pdb classes instead.")]
        public virtual void LoadPdb(string path) { }

        /// <summary>
        /// Attempts to download the PDB for this module from the symbol server.
        /// </summary>
        /// <returns>The path on disk of the downloaded PDB, or null if not found.</returns>
        [Obsolete("Use TryDownloadPdb() instead.")]
        public virtual string TryDownloadPdb(ISymbolNotification notification) { return null; }

        /// <summary>
        /// The ISymbolNotification to use if none is specified.
        /// </summary>
        [Obsolete("This value is currently ignored.")]
        public ISymbolNotification DefaultSymbolNotification { get; set; }

        /// <summary>
        /// Attempts to download the PDB for this module from the symbol server.
        /// </summary>
        /// <returns>The path on disk of the downloaded PDB, or null if not found.</returns>
        [Obsolete("Use Microsoft.Diagnostics.Utilities.Pdb classes instead.")]
        public virtual string TryDownloadPdb() { return null; }

        /// <summary>
        /// Returns the IDiaSession interface if the pdb is loaded for this module.
        /// </summary>
        [Obsolete("Use Microsoft.Diagnostics.Utilities.Pdb classes instead.")]
        public virtual object PdbInterface { get { return null; } }

        /// <summary>
        /// Gets the source location of a given metadata token for a function and offset.
        /// </summary>
        /// <param name="mdMethodToken">A method def token (ClrMethod.MetadataToken).</param>
        /// <param name="ilOffset">The il offset to look up the source information.</param>
        /// <returns>The SourceLocation for the given IL offset, or null if no mapping exists.</returns>
        [Obsolete("Use Microsoft.Diagnostics.Utilities.Pdb classes instead.")]
        public virtual SourceLocation GetSourceInformation(uint mdMethodToken, int ilOffset) { return null; }

        /// <summary>
        /// Gets the source location of a given metadata token for a function and offset.
        /// </summary>
        /// <param name="method">The method to look up the source information.</param>
        /// <param name="ilOffset">The il offset to look up the source information.</param>
        /// <returns>The SourceLocation for the given IL offset, or null if no mapping exists.</returns>
        [Obsolete("Use Microsoft.Diagnostics.Utilities.Pdb classes instead.")]
        public virtual SourceLocation GetSourceInformation(ClrMethod method, int ilOffset) { return null; }
        #endregion

        /// <summary>
        /// Returns a list of all AppDomains this module is loaded into.  Please note that unlike
        /// ClrRuntime.AppDomains, this list may include the shared AppDomain.
        /// </summary>
        public abstract IList<ClrAppDomain> AppDomains { get; }

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
        public abstract ulong AssemblyId { get; }

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
        public abstract bool IsFile { get; }

        /// <summary>
        /// Returns the filename of where the module was loaded from on disk.  Undefined results if
        /// IsPEFile returns false.
        /// </summary>
        public abstract string FileName { get; }

        /// <summary>
        /// Returns the base of the image loaded into memory.  This may be 0 if there is not a physical
        /// file backing it.
        /// </summary>
        public abstract Address ImageBase { get; }

        /// <summary>
        /// Returns the size of the image in memory.
        /// </summary>
        public abstract ulong Size { get; }

        /// <summary>
        /// Enumerate all types defined by this module.
        /// </summary>
        public abstract IEnumerable<ClrType> EnumerateTypes();

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
        public abstract object MetadataImport { get; }

        /// <summary>
        /// The debugging attributes for this module.
        /// </summary>
        public abstract DebuggableAttribute.DebuggingModes DebuggingMode { get; }

        /// <summary>
        /// Attempts to obtain a ClrType based on the name of the type.  Note this is a "best effort" due to
        /// the way that the dac handles types.  This function will fail for Generics, and types which have
        /// never been constructed in the target process.  Please be sure to null-check the return value of
        /// this function.
        /// </summary>
        /// <param name="name">The name of the type.  (This would be the EXACT value returned by ClrType.Name.</param>
        /// <returns>The requested ClrType, or null if the type doesn't exist or couldn't be constructed.</returns>
        public abstract ClrType GetTypeByName(string name);

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
    }
}
