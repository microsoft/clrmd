// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct V4ModuleData : IModuleData
    {
        public readonly ulong PEFile;
        public readonly ulong ILBase;
        public readonly ulong MetadataStart;
        public readonly IntPtr MetadataSize;
        public readonly ulong Assembly;
        public readonly uint IsReflection;
        public readonly uint IsPEFile;
        public readonly IntPtr BaseClassIndex;
        public readonly IntPtr MetaDataImport;
        public readonly IntPtr ModuleID;

        public readonly uint TransientFlags;

        public readonly ulong TypeDefToMethodTableMap;
        public readonly ulong TypeRefToMethodTableMap;
        public readonly ulong MethodDefToDescMap;
        public readonly ulong FieldDefToDescMap;
        public readonly ulong MemberRefToDescMap;
        public readonly ulong FileReferencesMap;
        public readonly ulong ManifestModuleReferencesMap;

        public readonly ulong LookupTableHeap;
        public readonly ulong ThunkHeap;
        public readonly IntPtr ModuleIndex;

        ulong IModuleData.PEFile => PEFile;
        ulong IModuleData.Assembly => Assembly;
        ulong IModuleData.ImageBase => ILBase;
        ulong IModuleData.LookupTableHeap => LookupTableHeap;
        ulong IModuleData.ThunkHeap => ThunkHeap;
        ulong IModuleData.ModuleId => (ulong)ModuleID.ToInt64();
        ulong IModuleData.ModuleIndex => (ulong)ModuleIndex.ToInt64();
        bool IModuleData.IsReflection => IsReflection != 0;
        bool IModuleData.IsPEFile => IsPEFile != 0;
        ulong IModuleData.MetdataStart => MetadataStart;
        ulong IModuleData.MetadataLength => (ulong)MetadataSize.ToInt64();
    }
}