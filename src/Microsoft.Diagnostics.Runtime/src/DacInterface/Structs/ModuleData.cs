// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Desktop;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ModuleData : IModuleData
    {
        public readonly ulong Address;
        public readonly ulong PEFile;
        public readonly ulong ILBase;
        public readonly ulong MetadataStart;
        public readonly ulong MetadataSize;
        public readonly ulong Assembly;
        public readonly uint IsReflection;
        public readonly uint IsPEFile;
        public readonly ulong BaseClassIndex;
        public readonly ulong ModuleID;
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
        public readonly ulong ModuleIndex;

        ulong IModuleData.Assembly => Assembly;
        ulong IModuleData.PEFile => IsPEFile == 0 ? ILBase : PEFile;
        ulong IModuleData.LookupTableHeap => LookupTableHeap;
        ulong IModuleData.ThunkHeap => ThunkHeap;
        ulong IModuleData.ModuleId => ModuleID;
        ulong IModuleData.ModuleIndex => ModuleIndex;
        bool IModuleData.IsReflection => IsReflection != 0;
        bool IModuleData.IsPEFile => IsPEFile != 0;
        ulong IModuleData.ImageBase => ILBase;
        ulong IModuleData.MetdataStart => MetadataStart;
        ulong IModuleData.MetadataLength => MetadataSize;
    }
}