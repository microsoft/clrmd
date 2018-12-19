using Microsoft.Diagnostics.Runtime.Interop;
using System;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// A wrapper for the IMAGE_COR20_HEADER structure.
    /// </summary>
    public class CorHeader
    {
        private IMAGE_COR20_HEADER _header;

        internal CorHeader(IMAGE_COR20_HEADER header)
        {
            _header = header;
        }

        /// <summary>
        /// A set of COMIMAGE_FLAGS.
        /// </summary>
        public COMIMAGE_FLAGS Flags => (COMIMAGE_FLAGS)_header.Flags;
        public ushort MajorRuntimeVersion => _header.MajorRuntimeVersion;
        public ushort MinorRuntimeVersion => _header.MinorRuntimeVersion;

        // Symbol table and startup information
        public Interop.IMAGE_DATA_DIRECTORY Metadata => _header.MetaData;

        public uint NativeEntryPoint => (Flags & COMIMAGE_FLAGS.NATIVE_ENTRYPOINT) == COMIMAGE_FLAGS.NATIVE_ENTRYPOINT ? _header.EntryPoint.RVA : throw new InvalidOperationException();
        public uint ManagedEntryPoint => (Flags & COMIMAGE_FLAGS.NATIVE_ENTRYPOINT) != COMIMAGE_FLAGS.NATIVE_ENTRYPOINT ? _header.EntryPoint.Token : throw new InvalidOperationException();

        /// <summary>
        /// This is the blob of managed resources. Fetched using code:AssemblyNative.GetResource and
        /// code:PEFile.GetResource and accessible from managed code from
        /// System.Assembly.GetManifestResourceStream.  The meta data has a table that maps names to offsets into
        /// this blob, so logically the blob is a set of resources.
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY Resources => _header.Resources;

        /// <summary>
        /// IL assemblies can be signed with a public-private key to validate who created it.  The signature goes
        /// here if this feature is used.
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY StrongNameSignature => _header.StrongNameSignature;

        /// <summary>
        /// Used for managed codeethat has unmanaged code inside of it (or exports methods as unmanaged entry points) .
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY VTableFixups => _header.VTableFixups;
        public Interop.IMAGE_DATA_DIRECTORY ExportAddressTableJumps => _header.ExportAddressTableJumps;

        /// <summary>
        /// This is null for ordinary IL images.  NGEN images it points at a CORCOMPILE_HEADER structure.
        /// </summary>
        public Interop.IMAGE_DATA_DIRECTORY ManagedNativeHeader => _header.ManagedNativeHeader;
    }
}
