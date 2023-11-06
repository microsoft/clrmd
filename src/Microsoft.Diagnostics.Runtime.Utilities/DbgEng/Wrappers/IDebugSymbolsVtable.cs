// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

#pragma warning disable CS0169 // field is never used
#pragma warning disable CS0649 // field is never assigned
namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct IDebugSymbolsVtable
    {
        private readonly nint QueryInterface;
        private readonly nint AddRef;
        private readonly nint Release;

        private readonly nint GetSymbolOptions;
        private readonly nint AddSymbolOptions;
        private readonly nint RemoveSymbolOptions;
        private readonly nint SetSymbolOptions;
        private readonly nint GetNameByOffset;
        private readonly nint GetOffsetByName;
        private readonly nint GetNearNameByOffset;
        private readonly nint GetLineByOffset;
        private readonly nint GetOffsetByLine;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, out int, out int, int> GetNumberModules;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, int, out ulong, int> GetModuleByIndex;
        private readonly nint GetModuleByModuleName;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong, int, out int, out ulong, int> GetModuleByOffset;
        private readonly nint GetModuleNames;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, int, ulong*, int, DEBUG_MODULE_PARAMETERS*, int> GetModuleParameters;
        private readonly nint GetSymbolModule;
        private readonly nint GetTypeName;
        private readonly nint GetTypeId;
        private readonly nint GetTypeSize;
        private readonly nint GetFieldOffset;
        private readonly nint GetSymbolTypeId;
        private readonly nint GetOffsetTypeId;
        private readonly nint ReadTypedDataVirtual;
        private readonly nint WriteTypedDataVirtual;
        private readonly nint OutputTypedDataVirtual;
        private readonly nint ReadTypedDataPhysical;
        private readonly nint WriteTypedDataPhysical;
        private readonly nint OutputTypedDataPhysical;
        private readonly nint GetScope;
        private readonly nint SetScope;
        private readonly nint ResetScope;
        private readonly nint GetScopeSymbolGroup;
        private readonly nint CreateSymbolGroup;
        private readonly nint StartSymbolMatch;
        private readonly nint GetNextSymbolMatch;
        private readonly nint EndSymbolMatch;
        private readonly nint Reload;
        private readonly nint GetSymbolPath;
        private readonly nint SetSymbolPath;
        private readonly nint AppendSymbolPath;
        private readonly nint GetImagePath;
        private readonly nint SetImagePath;
        private readonly nint AppendImagePath;
        private readonly nint GetSourcePath;
        private readonly nint GetSourcePathElement;
        private readonly nint SetSourcePath;
        private readonly nint AppendSourcePath;
        private readonly nint FindSourceFile;
        private readonly nint GetSourceFileLineOffsets;
        public readonly delegate* unmanaged[Stdcall]<IntPtr, int, ulong, byte*, byte*, int, out int, int> GetModuleVersionInformation;
        private readonly nint GetModuleNameString;
        private readonly nint GetConstantName;
        private readonly nint GetFieldName;
        private readonly nint GetTypeOptions;
        private readonly nint AddTypeOptions;
        private readonly nint RemoveTypeOptions;
        private readonly nint SetTypeOptions;
        public readonly delegate* unmanaged[Stdcall]<nint, ulong, char*, int, out int, out ulong, int> GetNameByOffsetWide;
        public readonly delegate* unmanaged[Stdcall]<nint, char*, out ulong, int> GetOffsetByNameWide;
        private readonly nint GetNearNameByOffsetWide;
        private readonly nint GetLineByOffsetWide;
        private readonly nint GetOffsetByLineWide;
        private readonly nint GetModuleByModuleNameWide;
        private readonly nint GetSymbolModuleWide;
        private readonly nint GetTypeNameWide;
        public readonly delegate* unmanaged[Stdcall]<nint, ulong, char*, out ulong, int> GetTypeIdWide;
        public readonly delegate* unmanaged[Stdcall]<nint, ulong, ulong, char*, out ulong, int> GetFieldOffsetWide;
        private readonly nint GetSymbolTypeIdWide;
        private readonly nint GetScopeSymbolGroup2;
        private readonly nint CreateSymbolGroup2;
        private readonly nint StartSymbolMatchWide;
        private readonly nint GetNextSymbolMatchWide;
        public readonly delegate* unmanaged[Stdcall]<nint, char*, int> ReloadWide;
        public readonly delegate* unmanaged[Stdcall]<nint, char*, int, out int, int> GetSymbolPathWide;
        public readonly delegate* unmanaged[Stdcall]<nint, char*, int> SetSymbolPathWide;
        private readonly nint AppendSymbolPathWide;
        private readonly nint GetImagePathWide;
        private readonly nint SetImagePathWide;
        private readonly nint AppendImagePathWide;
        private readonly nint GetSourcePathWide;
        private readonly nint GetSourcePathElementWide;
        private readonly nint SetSourcePathWide;
        private readonly nint AppendSourcePathWide;
        private readonly nint FindSourceFileWide;
        private readonly nint GetSourceFileLineOffsetsWide;
        private readonly nint GetModuleVersionInformationWide;
        public readonly delegate* unmanaged[Stdcall]<nint, DEBUG_MODNAME, int, ulong, char*, int, out int, int> GetModuleNameStringWide;
        private readonly nint GetConstantNameWide;
        private readonly nint GetFieldNameWide;
        private readonly nint IsManagedModule;
        private readonly nint GetModuleByModuleName2;
        private readonly nint GetModuleByModuleName2Wide;
        private readonly nint GetModuleByOffset2;
        private readonly nint AddSyntheticModule;
        private readonly nint AddSyntheticModuleWide;
        private readonly nint RemoveSyntheticModule;
        private readonly nint GetCurrentScopeFrameIndex;
        private readonly nint SetScopeFrameByIndex;
        private readonly nint SetScopeFromJitDebugInfo;
        private readonly nint SetScopeFromStoredEvent;
        private readonly nint OutputSymbolByOffset;
        private readonly nint GetFunctionEntryByOffset;
        private readonly nint GetFieldTypeAndOffset;
        private readonly nint GetFieldTypeAndOffsetWide;
        private readonly nint AddSyntheticSymbol;
        private readonly nint AddSyntheticSymbolWide;
        private readonly nint RemoveSyntheticSymbol;
        private readonly nint GetSymbolEntriesByOffset;
        private readonly nint GetSymbolEntriesByName;
        private readonly nint GetSymbolEntriesByNameWide;
        private readonly nint GetSymbolEntryByToken;
        private readonly nint GetSymbolEntryInformation;
        private readonly nint GetSymbolEntryString;
        private readonly nint GetSymbolEntryStringWide;
        private readonly nint GetSymbolEntryOffsetRegions;
        private readonly nint GetSymbolEntryBySymbolEntry;
        private readonly nint GetSourceEntriesByOffset;
        private readonly nint GetSourceEntriesByLine;
        private readonly nint GetSourceEntriesByLineWide;
        private readonly nint GetSourceEntryString;
        private readonly nint GetSourceEntryStringWide;
        private readonly nint GetSourceEntryOffsetRegions;
        private readonly nint GetSourceEntryBySourceEntry;

        // IDebugSymbols4
        private readonly nint GetScopeEx;
        private readonly nint SetScopeEx;
        private readonly nint GetNameByInlineContext;
        public readonly delegate* unmanaged[Stdcall]<nint, ulong, uint, char*, int, out int, out ulong, int> GetNameByInlineContextWide;
        private readonly nint GetLineByInlineContext;
        private readonly nint GetLineByInlineContextWide;
        private readonly nint OutputSymbolByInlineContext;
    }
}