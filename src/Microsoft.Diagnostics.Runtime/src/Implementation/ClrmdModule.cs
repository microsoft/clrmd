// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public sealed class ClrmdModule : ClrModule
    {
        private const int mdtTypeDef = 0x02000000;
        private const int mdtTypeRef = 0x01000000;

        private readonly IModuleData? _data;
        private readonly IModuleHelpers _helpers;
        private string? _simpleName;
        private string? _assemblyName;
        private int _debugMode = int.MaxValue;
        private MetadataImport? _metadata;
        private PdbInfo? _pdb;
        private (ulong MethodTable, int Token)[]? _typeDefMap;
        private (ulong MethodTable, int Token)[]? _typeRefMap;

        public override ClrAppDomain AppDomain { get; }
        public override string? Name { get; }
        public override string? SimpleName => _simpleName ??= _data?.SimpleName;
        public override string? AssemblyName => _assemblyName ??= _data?.AssemblyName;
        public override ulong AssemblyAddress { get; }
        public override ulong Address { get; }
        public override bool IsPEFile { get; }
        public override ulong ImageBase { get; }
        public override ModuleLayout Layout { get; }
        public override ulong Size { get; }
        public override ulong MetadataAddress { get; }
        public override ulong MetadataLength { get; }
        public override bool IsDynamic { get; }
        public override MetadataImport? MetadataImport => _metadata ??= _helpers.GetMetadataImport(this);

        public ClrmdModule(ClrAppDomain parent, IModuleData data)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));

            _data = data;
            _helpers = data.Helpers;
            AppDomain = parent;
            Name = data.Name;
            AssemblyAddress = data.AssemblyAddress;
            Address = data.Address;
            IsPEFile = data.IsPEFile;
            ImageBase = data.ILImageBase;
            Layout = data.IsFlatLayout ? ModuleLayout.Flat : ModuleLayout.Unknown;
            Size = data.Size;
            MetadataAddress = data.MetadataStart;
            MetadataLength = data.MetadataLength;
            IsDynamic = data.IsReflection || string.IsNullOrWhiteSpace(Name);
        }

        public ClrmdModule(ClrAppDomain parent, IModuleHelpers helpers, ulong addr)
        {
            AppDomain = parent;
            _helpers = helpers;
            Address = addr;
        }

        public override PdbInfo? Pdb
        {
            get
            {
                if (_pdb is null)
                {
                    // Not correct, but as close as we can get until we add more information to the dac.
                    bool virt = Layout != ModuleLayout.Flat;

                    using ReadVirtualStream stream = new ReadVirtualStream(_helpers.DataReader, (long)ImageBase, (long)(Size > 0 ? Size : int.MaxValue));
                    using PEImage pefile = new PEImage(stream, leaveOpen: true, isVirtual: virt);
                    if (pefile.IsValid)
                        _pdb = pefile.DefaultPdb;
                }

                return _pdb;
            }
        }

        public override DebuggableAttribute.DebuggingModes DebuggingMode
        {
            get
            {
                if (_debugMode == int.MaxValue)
                    _debugMode = GetDebugAttribute();

                DebugOnly.Assert(_debugMode != int.MaxValue);
                return (DebuggableAttribute.DebuggingModes)_debugMode;
            }
        }

        private unsafe int GetDebugAttribute()
        {
            MetadataImport? metadata = MetadataImport;
            if (metadata != null)
            {
                try
                {
                    if (metadata.GetCustomAttributeByName(0x20000001, "System.Diagnostics.DebuggableAttribute", out IntPtr data, out uint cbData) && cbData >= 4)
                    {
                        byte* b = (byte*)data.ToPointer();
                        ushort opt = b[2];
                        ushort dbg = b[3];

                        return (dbg << 8) | opt;
                    }
                }
                catch (SEHException)
                {
                }
            }

            return (int)DebuggableAttribute.DebuggingModes.None;
        }

        public override IEnumerable<(ulong MethodTable, int Token)> EnumerateTypeDefToMethodTableMap()
        {
            _typeDefMap ??= _helpers.GetSortedTypeDefMap(this);
            return _typeDefMap.Select(t => (t.MethodTable, t.Token | mdtTypeDef));
        }

        public override ClrType? GetTypeByName(string name)
        {
            if (name is null)
                throw new ArgumentNullException(nameof(name));

            if (name.Length == 0)
                throw new ArgumentException($"{nameof(name)} cannot be empty");

            // First, look for already constructed types and see if their name matches.
            List<ulong> lookup = new List<ulong>(256);
            foreach ((ulong mt, _) in EnumerateTypeDefToMethodTableMap())
            {
                ClrType? type = _helpers.TryGetType(mt);
                if (type is null)
                    lookup.Add(mt);
                else if (type.Name == name)
                    return type;
            }

            // Since we didn't find pre-constructed types matching, look up the names for all
            // remaining types without constructing them until we find the right one.
            foreach (ulong mt in lookup)
            {
                string? typeName = _helpers.GetTypeName(mt);
                if (typeName == name)
                    return _helpers.Factory.GetOrCreateType(mt, 0);
            }

            return null;
        }

        public override ClrType? ResolveToken(int typeDefOrRefToken)
        {
            if (typeDefOrRefToken == 0)
                return null;

            ClrHeap? heap = AppDomain?.Runtime?.Heap;
            if (heap is null)
                return null;

            (ulong MethodTable, int Token)[] map;
            if ((typeDefOrRefToken & mdtTypeDef) != 0)
                map = _typeDefMap ??= _helpers.GetSortedTypeDefMap(this);
            else if ((typeDefOrRefToken & mdtTypeRef) != 0)
                map = _typeRefMap ??= _helpers.GetSortedTypeRefMap(this);
            else
                throw new NotSupportedException($"ResolveToken does not support this token type: {typeDefOrRefToken:x}");

            int index = map.Search(typeDefOrRefToken & (int)~0xff000000, CompareTo);
            if (index == -1)
                return null;

            return _helpers.Factory.GetOrCreateType(map[index].MethodTable, 0);
        }

        private static int CompareTo((ulong MethodTable, int Token) entry, int token) => entry.Token.CompareTo(token);
    }
}