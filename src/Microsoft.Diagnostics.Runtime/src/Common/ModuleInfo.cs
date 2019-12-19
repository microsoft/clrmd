// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Provides information about loaded modules in a DataTarget.
    /// </summary>
    public class ModuleInfo
    {
        private PEImage? _image;
        private readonly IDataReader _dataReader;
        private VersionInfo? _version;

        /// <summary>
        /// Gets the base address of the object.
        /// </summary>
        public ulong ImageBase { get; }

        /// <summary>
        /// Gets the file size of the image.
        /// </summary>
        public uint FileSize { get; }

        /// <summary>
        /// Gets the build timestamp of the image.
        /// </summary>
        public uint TimeStamp { get; }

        /// <summary>
        /// Gets the file name of the module on disk.
        /// </summary>
        public string? FileName { get; }

        /// <summary>
        /// Returns a PEImage from a stream constructed using instance fields of this object.
        /// If the PEImage cannot be constructed, <see langword="null"/> is returned.
        /// </summary>
        /// <returns></returns>
        public PEImage? GetPEImage()
        {
            if (_image != null)
                return _image;

            try
            {
                return _image = new PEImage(new ReadVirtualStream(_dataReader, (long)ImageBase, FileSize), isVirtual: true);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the Linux BuildId of this module.  This will be <see langword="null"/> if the module does not have a BuildId.
        /// </summary>
        public ImmutableArray<byte> BuildId { get; }

        /// <summary>
        /// Gets a value indicating whether the module is managed.
        /// </summary>
        public bool IsManaged => GetPEImage()?.IsManaged ?? false;

        public override string? ToString() => FileName;

        /// <summary>
        /// Gets the PDB associated with this module.
        /// </summary>
        public PdbInfo? Pdb => GetPEImage()?.DefaultPdb;

        /// <summary>
        /// Gets the version information for this file.
        /// </summary>
        public VersionInfo Version
        {
            get
            {
                if (_version is VersionInfo version)
                {
                    return version;
                }

                _dataReader.GetVersionInfo(ImageBase, out version);
                _version = version;

                return version;
            }
        }

        /// <summary>
        /// Creates a ModuleInfo object with an IDataReader instance.  This is used when
        /// lazily evaluating VersionInfo.
        /// </summary>
        public ModuleInfo(IDataReader reader, ulong imgBase, uint filesize, uint timestamp, string? fileName,
            ImmutableArray<byte> buildId = default, VersionInfo? version = null)
        {
            _dataReader = reader ?? throw new ArgumentNullException(nameof(reader));
            ImageBase = imgBase;
            FileSize = filesize;
            TimeStamp = timestamp;
            FileName = fileName;
            BuildId = buildId;
            _version = version;
        }
    }
}