// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Provides information about loaded modules in a <see cref="DataTarget"/>.
    /// </summary>
    public sealed class ModuleInfo
    {
        private bool? _isManaged;
        private ImmutableArray<byte> _buildId;
        private VersionInfo? _version;
        private readonly bool _isVirtual;

        /// <summary>
        /// The DataTarget which contains this module.
        /// </summary>
        public DataTarget DataTarget { get; internal set; }

        /// <summary>
        /// Gets the base address of the object.
        /// </summary>
        public ulong ImageBase { get; }

        /// <summary>
        /// Gets the specific file size of the image used to index it on the symbol server.
        /// </summary>
        public int IndexFileSize { get; }

        /// <summary>
        /// Gets the timestamp of the image used to index it on the symbol server.
        /// </summary>
        public int IndexTimeStamp { get; }

        /// <summary>
        /// Gets the file name of the module on disk.
        /// </summary>
        public string? FileName { get; }

        /// <summary>
        /// Returns a <see cref="PEImage"/> from a stream constructed using instance fields of this object.
        /// If the PEImage cannot be constructed, <see langword="null"/> is returned.
        /// </summary>
        /// <returns></returns>
        public PEImage? GetPEImage()
        {
            try
            {
                PEImage image = new PEImage(new ReadVirtualStream(DataTarget.DataReader, (long)ImageBase, IndexFileSize), leaveOpen: false, isVirtual: _isVirtual);
                if (!_isManaged.HasValue)
                    _isManaged = image.IsManaged;

                return image.IsValid ? image : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the Linux BuildId of this module.  This will be <see langword="null"/> if the module does not have a BuildId.
        /// </summary>
        public ImmutableArray<byte> BuildId
        {
            get
            {
                if (_buildId.IsDefault)
                {
                    return _buildId = DataTarget.DataReader.GetBuildId(ImageBase);
                }

                return _buildId;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the module is managed.
        /// </summary>
        public bool IsManaged
        {
            get
            {
                if (!_isManaged.HasValue)
                {
                    // this can assign _isManaged
                    using PEImage? image = GetPEImage();

                    if (!_isManaged.HasValue)
                        _isManaged = image?.IsManaged ?? false;
                }

                return _isManaged.Value;
            }
        }

        public override string? ToString() => FileName;

        /// <summary>
        /// Gets the PDB associated with this module.
        /// </summary>
        public PdbInfo? Pdb
        {
            get
            {
                using PEImage? image = GetPEImage();
                if (image != null)
                {
                    if (!_isManaged.HasValue)
                        _isManaged = image.IsManaged;

                    return image.DefaultPdb;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the version information for this file.
        /// </summary>
        public VersionInfo Version
        {
            get
            {
                if (_version.HasValue)
                    return _version.Value;

                _version = DataTarget.DataReader.GetVersionInfo(ImageBase, out VersionInfo version) ? version : default;
                return version;
            }
        }


        // DataTarget is one of the few "internal set" properties, and is initialized as soon as DataTarget asks
        // IDataReader to create ModuleInfo.  So even though we don't set it here, we will immediately set the
        // value to non-null and never change it.

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="imgBase">The base of the image as loaded into the virtual address space.</param>
        /// <param name="fileName">The full path of the file as loaded from disk (if possible), otherwise only the filename.</param>
        /// <param name="isVirtual">Whether this image is mapped into the virtual address space.  (This is as opposed to a memmap'ed file.)</param>
        /// <param name="indexFileSize">The index file size used by the symbol server to archive and request this binary.  Only for PEImages (not Elf or Mach-O binaries).</param>
        /// <param name="indexTimeStamp">The index timestamp used by the symbol server to archive and request this binary.  Only for PEImages (not Elf or Mach-O binaries).</param>
        /// <param name="buildId">The ELF buildid of this image.  Not valid for PEImages.</param>
        public ModuleInfo(ulong imgBase, string? fileName, bool isVirtual, int indexFileSize, int indexTimeStamp, ImmutableArray<byte> buildId)
        {
            ImageBase = imgBase;
            IndexFileSize = indexFileSize;
            IndexTimeStamp = indexTimeStamp;
            FileName = fileName;
            _isVirtual = isVirtual;
            _buildId = buildId;
        }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
    }
}
