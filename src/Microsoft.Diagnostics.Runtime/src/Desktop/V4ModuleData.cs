// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct V4ModuleData : IModuleData
    {
        public ulong peFile;
        public ulong ilBase;
        public ulong metadataStart;
        public IntPtr metadataSize;
        public ulong assembly;
        public uint bIsReflection;
        public uint bIsPEFile;
        public IntPtr dwBaseClassIndex;
        public IntPtr ModuleDefinition;
        public IntPtr dwModuleID;

        public uint dwTransientFlags;

        public ulong TypeDefToMethodTableMap;
        public ulong TypeRefToMethodTableMap;
        public ulong MethodDefToDescMap;
        public ulong FieldDefToDescMap;
        public ulong MemberRefToDescMap;
        public ulong FileReferencesMap;
        public ulong ManifestModuleReferencesMap;

        public ulong pLookupTableHeap;
        public ulong pThunkHeap;

        public IntPtr dwModuleIndex;

        public ulong PEFile => peFile;
        public ulong Assembly => assembly;
        public ulong ImageBase => ilBase;
        public ulong LookupTableHeap => pLookupTableHeap;
        public ulong ThunkHeap => pThunkHeap;
        public IntPtr LegacyMetaDataImport => ModuleDefinition;
        public ulong ModuleId => (ulong)dwModuleID.ToInt64();
        public ulong ModuleIndex => (ulong)dwModuleIndex.ToInt64();
        public bool IsReflection => bIsReflection != 0;
        public bool IsPEFile => bIsPEFile != 0;
        public ulong MetdataStart => metadataStart;
        public ulong MetadataLength => (ulong)metadataSize.ToInt64();
    }
}