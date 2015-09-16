// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Address = System.UInt64;
using System.Text;
using System.Collections;
using System.IO;
using System.Reflection;
using Microsoft.Diagnostics.Runtime;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;
using Dia2Lib;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal abstract class DesktopBaseModule : ClrModule
    {
        internal abstract Address GetDomainModule(ClrAppDomain appDomain);

        internal Address ModuleId { get; set; }

        internal virtual IMetadata GetMetadataImport()
        {
            return null;
        }

        public int Revision { get; set; }
    }

    internal class DesktopModule : DesktopBaseModule
    {
        private bool _reflection,_isPE;
        private string _name,_assemblyName;
        private DesktopRuntimeBase _runtime;
        private IMetadata _metadata;
        private Dictionary<ClrAppDomain, ulong> _mapping = new Dictionary<ClrAppDomain, ulong>();
        private Address _imageBase,_size;
        private Address _metadataStart;
        private Address _metadataLength;
        private DebuggableAttribute.DebuggingModes? _debugMode;
        private Address _address;
        private Address _assemblyAddress;
        private bool _typesLoaded;
        private SymbolModule _symbols;
        private PEFile _peFile;


        public override SourceLocation GetSourceInformation(ClrMethod method, int ilOffset)
        {
            if (method == null)
                throw new ArgumentNullException("method");

            if (method.Type != null && method.Type.Module != this)
                throw new InvalidOperationException("Method not in this module.");

            return GetSourceInformation(method.MetadataToken, ilOffset);
        }

        public override SourceLocation GetSourceInformation(uint token, int ilOffset)
        {
            if (_symbols == null)
                return null;

            return _symbols.SourceLocationForManagedCode(token, ilOffset);
        }

        public override bool IsPdbLoaded { get { return _symbols != null; } }

        public override bool IsMatchingPdb(string pdbPath)
        {
            if (_peFile == null)
                _peFile = new PEFile(new ReadVirtualStream(_runtime.DataReader, (long)_imageBase, (long)_size), true);

            string pdbName;
            Guid pdbGuid;
            int rev;
            if (!_peFile.GetPdbSignature(out pdbName, out pdbGuid, out rev))
                throw new ClrDiagnosticsException("Failed to get PDB signature from module.", ClrDiagnosticsException.HR.DataRequestError);

            //todo: fix/release
            IDiaDataSource source = DiaLoader.GetDiaSourceObject();
            IDiaSession session;
            source.loadDataFromPdb(pdbPath);
            source.openSession(out session);
            return pdbGuid == session.globalScope.guid;
        }

        public override void LoadPdb(string path)
        {
            _symbols = _runtime.DataTarget.SymbolLocator.LoadPdb(path);
        }


        public override object PdbInterface
        {
            get
            {
                if (_symbols == null)
                    return null;

                return _symbols.Session;
            }
        }

        public override string TryDownloadPdb()
        {
            var dataTarget = _runtime.DataTarget;

            string pdbName;
            Guid pdbGuid;
            int rev;
            if (!_peFile.GetPdbSignature(out pdbName, out pdbGuid, out rev))
                throw new ClrDiagnosticsException("Failed to get PDB signature from module.", ClrDiagnosticsException.HR.DataRequestError);

            return _runtime.DataTarget.SymbolLocator.FindPdb(pdbName, pdbGuid, rev);
        }


        public DesktopModule(DesktopRuntimeBase runtime, ulong address, IModuleData data, string name, string assemblyName, ulong size)
        {
            Revision = runtime.Revision;
            _imageBase = data.ImageBase;
            _runtime = runtime;
            _assemblyName = assemblyName;
            _isPE = data.IsPEFile;
            _reflection = data.IsReflection || string.IsNullOrEmpty(name) || !name.Contains("\\");
            _name = name;
            ModuleId = data.ModuleId;
            ModuleIndex = data.ModuleIndex;
            _metadataStart = data.MetdataStart;
            _metadataLength = data.MetadataLength;
            _assemblyAddress = data.Assembly;
            _address = address;
            _size = size;

            // This is very expensive in the minidump case, as we may be heading out to the symbol server or
            // reading multiple files from disk. Only optimistically fetch this data if we have full memory.
            if (!runtime.DataReader.IsMinidump)
                _metadata = data.LegacyMetaDataImport as IMetadata;
        }

        public override IEnumerable<ClrType> EnumerateTypes()
        {
            var heap = (DesktopGCHeap)_runtime.GetHeap();
            var mtList = _runtime.GetMethodTableList(_address);
            if (_typesLoaded)
            {
                foreach (var type in heap.EnumerateTypes())
                    if (type.Module == this)
                        yield return type;
            }
            else
            {
                if (mtList != null)
                {
                    foreach (ulong mt in mtList)
                    {
                        if (mt != _runtime.ArrayMethodTable)
                        {
                            // prefetch element type, as this also can load types
                            var type = heap.GetGCHeapType(mt, 0, 0);
                            if (type != null)
                                yield return type;
                        }
                    }
                }

                _typesLoaded = true;
            }
        }

        public override string AssemblyName
        {
            get { return _assemblyName; }
        }

        public override string Name
        {
            get { return _name; }
        }

        public override bool IsDynamic
        {
            get { return _reflection; }
        }

        public override bool IsFile
        {
            get { return _isPE; }
        }

        public override string FileName
        {
            get { return _isPE ? _name : null; }
        }

        internal ulong ModuleIndex { get; private set; }

        internal void AddMapping(ClrAppDomain domain, ulong domainModule)
        {
            DesktopAppDomain appDomain = (DesktopAppDomain)domain;
            _mapping[domain] = domainModule;
        }

        internal override ulong GetDomainModule(ClrAppDomain domain)
        {
            _runtime.InitDomains();
            if (domain == null)
            {
                foreach (ulong addr in _mapping.Values)
                    return addr;

                return 0;
            }

            ulong value;
            if (_mapping.TryGetValue(domain, out value))
                return value;

            return 0;
        }

        internal override IMetadata GetMetadataImport()
        {
            if (Revision != _runtime.Revision)
                ClrDiagnosticsException.ThrowRevisionError(Revision, _runtime.Revision);

            if (_metadata != null)
                return _metadata;

            ulong module = GetDomainModule(null);
            if (module == 0)
                return null;

            _metadata = _runtime.GetMetadataImport(module);
            return _metadata;
        }

        public override Address ImageBase
        {
            get { return _imageBase; }
        }


        public override Address Size
        {
            get
            {
                return _size;
            }
        }

        internal void SetImageSize(Address size)
        {
            _size = size;
        }


        public override Address MetadataAddress
        {
            get { return _metadataStart; }
        }

        public override Address MetadataLength
        {
            get { return _metadataLength; }
        }

        public override object MetadataImport
        {
            get { return GetMetadataImport(); }
        }

        public override DebuggableAttribute.DebuggingModes DebuggingMode
        {
            get
            {
                if (_debugMode == null)
                    InitDebugAttributes();

                Debug.Assert(_debugMode != null);
                return _debugMode.Value;
            }
        }

        private void InitDebugAttributes()
        {
            IMetadata metadata = GetMetadataImport();
            if (metadata == null)
            {
                _debugMode = DebuggableAttribute.DebuggingModes.None;
                return;
            }

            try
            {
                IntPtr data;
                uint cbData;
                int hr = metadata.GetCustomAttributeByName(0x20000001, "System.Diagnostics.DebuggableAttribute", out data, out cbData);
                if (hr != 0 || cbData <= 4)
                {
                    _debugMode = DebuggableAttribute.DebuggingModes.None;
                    return;
                }

                unsafe
                {
                    byte* b = (byte*)data.ToPointer();
                    UInt16 opt = b[2];
                    UInt16 dbg = b[3];

                    _debugMode = (System.Diagnostics.DebuggableAttribute.DebuggingModes)((dbg << 8) | opt);
                }
            }
            catch (SEHException)
            {
                _debugMode = DebuggableAttribute.DebuggingModes.None;
            }
        }

        public override ClrType GetTypeByName(string name)
        {
            foreach (ClrType type in EnumerateTypes())
                if (type.Name == name)
                    return type;

            return null;
        }

        public override Address AssemblyId
        {
            get { return _assemblyAddress; }
        }
    }

    internal class ErrorModule : DesktopBaseModule
    {
        private static uint s_id = 0;
        private uint _id = s_id++;

        public override string AssemblyName
        {
            get { return "<error>"; }
        }

        public override string Name
        {
            get { return "<error>"; }
        }

        public override bool IsDynamic
        {
            get { return false; }
        }

        public override bool IsFile
        {
            get { return false; }
        }

        public override string FileName
        {
            get { return "<error>"; }
        }

        public override Address ImageBase
        {
            get { return 0; }
        }

        public override Address Size
        {
            get { return 0; }
        }

        public override IEnumerable<ClrType> EnumerateTypes()
        {
            return new ClrType[0];
        }

        public override Address MetadataAddress
        {
            get { return 0; }
        }

        public override Address MetadataLength
        {
            get { return 0; }
        }

        public override object MetadataImport
        {
            get { return null; }
        }

        internal override Address GetDomainModule(ClrAppDomain appDomain)
        {
            return 0;
        }

        public override DebuggableAttribute.DebuggingModes DebuggingMode
        {
            get { return DebuggableAttribute.DebuggingModes.None; }
        }

        public override ClrType GetTypeByName(string name)
        {
            return null;
        }

        public override Address AssemblyId
        {
            get { return _id; }
        }

        public override bool IsPdbLoaded
        {
            get { return false; }
        }

        public override bool IsMatchingPdb(string pdbPath)
        {
            return false;
        }

        public override void LoadPdb(string path)
        {
        }

        public override object PdbInterface
        {
            get { return null; }
        }

        public override string TryDownloadPdb()
        {
            return null;
        }

        public override SourceLocation GetSourceInformation(uint token, int ilOffset)
        {
            return null;
        }

        public override SourceLocation GetSourceInformation(ClrMethod method, int ilOffset)
        {
            return null;
        }
    }
}
