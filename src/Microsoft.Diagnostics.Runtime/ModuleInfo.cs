// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Runtime.Utilities;
using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Provides information about loaded modules in a DataTarget
    /// </summary>
    [Serializable]
    public class ModuleInfo
    {
        /// <summary>
        /// The base address of the object.
        /// </summary>
        public virtual ulong ImageBase { get; set; }

        /// <summary>
        /// The filesize of the image.
        /// </summary>
        public virtual uint FileSize { get; set; }

        /// <summary>
        /// The build timestamp of the image.
        /// </summary>
        public virtual uint TimeStamp { get; set; }

        /// <summary>
        /// The filename of the module on disk.
        /// </summary>
        public virtual string FileName { get; set; }

        /// <summary>
        /// Returns true if this module is a native (non-managed) .Net runtime module.
        /// </summary>
        public bool IsRuntime { get; internal set; }

        /// <summary>
        /// Returns a PEFile from a stream constructed using instance fields of this object.
        /// If the PEFile cannot be constructed correctly, null is returned
        /// </summary>
        /// <returns></returns>
        public PEFile GetPEFile()
        {
            return PEFile.TryLoad(new ReadVirtualStream(_dataReader, (long)ImageBase, (long)FileSize), true);
        }

        /// <summary>
        /// Whether the module is managed or not.
        /// </summary>
        public virtual bool IsManaged
        {
            get
            {
                InitData();
                return _managed ?? false;
            }
        }

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>The filename of the module.</returns>
        public override string ToString()
        {
            return FileName;
        }

        /// <summary>
        /// The PDB associated with this module.
        /// </summary>
        public PdbInfo Pdb
        {
            get
            {
                if (_pdb != null || _dataReader == null)
                    return _pdb;

                InitData();
                return _pdb;
            }

            set
            {
                _pdb = value;
            }
        }

        private void InitData()
        {
            if (_dataReader == null)
                return;

            if (_pdb != null && _managed != null)
                return;
            
            PEFile file = null;
            try
            {
                file = PEFile.TryLoad(new ReadVirtualStream(_dataReader, (long)ImageBase, (long)FileSize), true);
                if (file == null)
                    return;

                _managed = file.Header.ComDescriptorDirectory.VirtualAddress != 0;

                if (file.GetPdbSignature(out string pdbName, out Guid guid, out int age))
                {
                    _pdb = new PdbInfo()
                    {
                        FileName = pdbName,
                        Guid = guid,
                        Revision = age
                    };
                }
            }
            catch
            {
            }
            finally
            {
                if (file != null)
                    file.Dispose();
            }
        }

        /// <summary>
        /// The version information for this file.
        /// </summary>
        public VersionInfo Version
        {
            get
            {
                if (_versionInit || _dataReader == null)
                    return _version;

                _dataReader.GetVersionInfo(ImageBase, out _version);
                _versionInit = true;
                return _version;
            }

            set
            {
                _version = value;
                _versionInit = true;
            }
        }



        /// <summary>
        /// Empty constructor for serialization.
        /// </summary>
        public ModuleInfo()
        {
        }

        /// <summary>
        /// Creates a ModuleInfo object with an IDataReader instance.  This is used when
        /// lazily evaluating VersionInfo. 
        /// </summary>
        /// <param name="reader"></param>
        public ModuleInfo(IDataReader reader)
        {
            _dataReader = reader;
        }
        [NonSerialized]

        private IDataReader _dataReader;
        private PdbInfo _pdb;
        private bool? _managed;
        private VersionInfo _version;
        private bool _versionInit;
    }
}
