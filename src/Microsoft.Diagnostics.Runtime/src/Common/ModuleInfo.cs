﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.Diagnostics.Runtime.Utilities;

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
        public virtual System.Version Version => new System.Version();

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

        public override string ToString() => FileName;

        internal virtual T ReadResource<T>(params string[] path) where T : unmanaged => default;

        public ModuleInfo(ulong imageBase, string fileName!!)
        {
            ImageBase = imageBase;
            FileName = fileName;
        }
    }
}
