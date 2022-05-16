// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Provides information about loaded modules in a <see cref="DataTarget"/>.
    /// </summary>
    public abstract class ModuleInfo
    {
        /// <summary>
        /// Gets the base address of the this image.
        /// </summary>
        public ulong ImageBase { get; }

        /// <summary>
        /// Retrieves the FileName of this loaded module.  May be empty if it is unknown.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// The size of this image (may be different from <see cref="IndexFileSize"/>).
        /// </summary>
        public virtual long Size => IndexFileSize;

        /// <summary>
        /// Gets the specific file size of the image used to index it on the symbol server.
        /// </summary>
        public virtual int IndexFileSize => 0;

        /// <summary>
        /// Gets the timestamp of the image used to index it on the symbol server.
        /// </summary>
        public virtual int IndexTimeStamp => 0;

        /// <summary>
        /// The version of this module.
        /// </summary>
        public virtual Version Version => new Version();

        /// <summary>
        /// Gets the Linux BuildId or Mach-O UUID of this module.
        /// </summary>
        public virtual ImmutableArray<byte> BuildId => ImmutableArray<byte>.Empty;

        /// <summary>
        /// Gets the PDB associated with this module.
        /// </summary>
        public virtual PdbInfo? Pdb => null;

        /// <summary>
        /// Gets a value indicating whether the module is managed.
        /// </summary>
        public virtual bool IsManaged => false;

        public virtual ulong GetSymbolAddress(string symbol) => 0;

        /// <summary>
        /// The root of the resource tree for this module if one exists, null otherwise.
        /// </summary>
        public virtual IResourceNode? ResourceRoot => null;

        public override string ToString() => FileName;

        public ModuleInfo(ulong imageBase, string fileName!!)
        {
            ImageBase = imageBase;
            FileName = fileName;
        }
    }
}
