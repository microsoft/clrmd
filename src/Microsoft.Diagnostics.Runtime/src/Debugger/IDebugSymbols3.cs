// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("f02fbecc-50ac-4f36-9ad9-c975e8f32ff8")]
    public interface IDebugSymbols3 : IDebugSymbols2
    {
        /* IDebugSymbols */

        [PreserveSig]
        new int GetSymbolOptions(
            [Out] out SYMOPT Options);

        [PreserveSig]
        new int AddSymbolOptions(
            [In] SYMOPT Options);

        [PreserveSig]
        new int RemoveSymbolOptions(
            [In] SYMOPT Options);

        [PreserveSig]
        new int SetSymbolOptions(
            [In] SYMOPT Options);

        [PreserveSig]
        new int GetNameByOffset(
            [In] ulong Offset,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] int NameBufferSize,
            [Out] out uint NameSize,
            [Out] out ulong Displacement);

        [PreserveSig]
        new int GetOffsetByName(
            [In][MarshalAs(UnmanagedType.LPStr)] string Symbol,
            [Out] out ulong Offset);

        [PreserveSig]
        new int GetNearNameByOffset(
            [In] ulong Offset,
            [In] int Delta,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] int NameBufferSize,
            [Out] out uint NameSize,
            [Out] out ulong Displacement);

        [PreserveSig]
        new int GetLineByOffset(
            [In] ulong Offset,
            [Out] out uint Line,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder FileBuffer,
            [In] int FileBufferSize,
            [Out] out uint FileSize,
            [Out] out ulong Displacement);

        [PreserveSig]
        new int GetOffsetByLine(
            [In] uint Line,
            [In][MarshalAs(UnmanagedType.LPStr)] string File,
            [Out] out ulong Offset);

        [PreserveSig]
        new int GetNumberModules(
            [Out] out uint Loaded,
            [Out] out uint Unloaded);

        [PreserveSig]
        new int GetModuleByIndex(
            [In] uint Index,
            [Out] out ulong Base);

        [PreserveSig]
        new int GetModuleByModuleName(
            [In][MarshalAs(UnmanagedType.LPStr)] string Name,
            [In] uint StartIndex,
            [Out] out uint Index,
            [Out] out ulong Base);

        [PreserveSig]
        new int GetModuleByOffset(
            [In] ulong Offset,
            [In] uint StartIndex,
            [Out] out uint Index,
            [Out] out ulong Base);

        [PreserveSig]
        new int GetModuleNames(
            [In] uint Index,
            [In] ulong Base,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder ImageNameBuffer,
            [In] int ImageNameBufferSize,
            [Out] out uint ImageNameSize,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder ModuleNameBuffer,
            [In] int ModuleNameBufferSize,
            [Out] out uint ModuleNameSize,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder LoadedImageNameBuffer,
            [In] int LoadedImageNameBufferSize,
            [Out] out uint LoadedImageNameSize);

        [PreserveSig]
        new int GetModuleParameters(
            [In] uint Count,
            [In][MarshalAs(UnmanagedType.LPArray)] ulong[] Bases,
            [In] uint Start,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_MODULE_PARAMETERS[] Params);

        [PreserveSig]
        new int GetSymbolModule(
            [In][MarshalAs(UnmanagedType.LPStr)] string Symbol,
            [Out] out ulong Base);

        [PreserveSig]
        new int GetTypeName(
            [In] ulong Module,
            [In] uint TypeId,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] int NameBufferSize,
            [Out] out uint NameSize);

        [PreserveSig]
        new int GetTypeId(
            [In] ulong Module,
            [In][MarshalAs(UnmanagedType.LPStr)] string Name,
            [Out] out uint TypeId);

        [PreserveSig]
        new int GetTypeSize(
            [In] ulong Module,
            [In] uint TypeId,
            [Out] out uint Size);

        [PreserveSig]
        new int GetFieldOffset(
            [In] ulong Module,
            [In] uint TypeId,
            [In][MarshalAs(UnmanagedType.LPStr)] string Field,
            [Out] out uint Offset);

        [PreserveSig]
        new int GetSymbolTypeId(
            [In][MarshalAs(UnmanagedType.LPStr)] string Symbol,
            [Out] out uint TypeId,
            [Out] out ulong Module);

        [PreserveSig]
        new int GetOffsetTypeId(
            [In] ulong Offset,
            [Out] out uint TypeId,
            [Out] out ulong Module);

        [PreserveSig]
        new int ReadTypedDataVirtual(
            [In] ulong Offset,
            [In] ulong Module,
            [In] uint TypeId,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]
            byte[] Buffer,
            [In] uint BufferSize,
            [Out] out uint BytesRead);

        [PreserveSig]
        new int WriteTypedDataVirtual(
            [In] ulong Offset,
            [In] ulong Module,
            [In] uint TypeId,
            [In] IntPtr Buffer,
            [In] uint BufferSize,
            [Out] out uint BytesWritten);

        [PreserveSig]
        new int OutputTypedDataVirtual(
            [In] DEBUG_OUTCTL OutputControl,
            [In] ulong Offset,
            [In] ulong Module,
            [In] uint TypeId,
            [In] DEBUG_TYPEOPTS Flags);

        [PreserveSig]
        new int ReadTypedDataPhysical(
            [In] ulong Offset,
            [In] ulong Module,
            [In] uint TypeId,
            [In] IntPtr Buffer,
            [In] uint BufferSize,
            [Out] out uint BytesRead);

        [PreserveSig]
        new int WriteTypedDataPhysical(
            [In] ulong Offset,
            [In] ulong Module,
            [In] uint TypeId,
            [In] IntPtr Buffer,
            [In] uint BufferSize,
            [Out] out uint BytesWritten);

        [PreserveSig]
        new int OutputTypedDataPhysical(
            [In] DEBUG_OUTCTL OutputControl,
            [In] ulong Offset,
            [In] ulong Module,
            [In] uint TypeId,
            [In] DEBUG_TYPEOPTS Flags);

        [PreserveSig]
        new int GetScope(
            [Out] out ulong InstructionOffset,
            [Out] out DEBUG_STACK_FRAME ScopeFrame,
            [In] IntPtr ScopeContext,
            [In] uint ScopeContextSize);

        [PreserveSig]
        new int SetScope(
            [In] ulong InstructionOffset,
            [In] ref DEBUG_STACK_FRAME ScopeFrame,
            [In] IntPtr ScopeContext,
            [In] uint ScopeContextSize);

        [PreserveSig]
        new int ResetScope();

        [PreserveSig]
        new int GetScopeSymbolGroup(
            [In] DEBUG_SCOPE_GROUP Flags,
            [In][MarshalAs(UnmanagedType.Interface)]
            IDebugSymbolGroup Update,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugSymbolGroup Symbols);

        [PreserveSig]
        new int CreateSymbolGroup(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugSymbolGroup Group);

        [PreserveSig]
        new int StartSymbolMatch(
            [In][MarshalAs(UnmanagedType.LPStr)] string Pattern,
            [Out] out ulong Handle);

        [PreserveSig]
        new int GetNextSymbolMatch(
            [In] ulong Handle,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint MatchSize,
            [Out] out ulong Offset);

        [PreserveSig]
        new int EndSymbolMatch(
            [In] ulong Handle);

        [PreserveSig]
        new int Reload(
            [In][MarshalAs(UnmanagedType.LPStr)] string Module);

        [PreserveSig]
        new int GetSymbolPath(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint PathSize);

        [PreserveSig]
        new int SetSymbolPath(
            [In][MarshalAs(UnmanagedType.LPStr)] string Path);

        [PreserveSig]
        new int AppendSymbolPath(
            [In][MarshalAs(UnmanagedType.LPStr)] string Addition);

        [PreserveSig]
        new int GetImagePath(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint PathSize);

        [PreserveSig]
        new int SetImagePath(
            [In][MarshalAs(UnmanagedType.LPStr)] string Path);

        [PreserveSig]
        new int AppendImagePath(
            [In][MarshalAs(UnmanagedType.LPStr)] string Addition);

        [PreserveSig]
        new int GetSourcePath(
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint PathSize);

        [PreserveSig]
        new int GetSourcePathElement(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint ElementSize);

        [PreserveSig]
        new int SetSourcePath(
            [In][MarshalAs(UnmanagedType.LPStr)] string Path);

        [PreserveSig]
        new int AppendSourcePath(
            [In][MarshalAs(UnmanagedType.LPStr)] string Addition);

        [PreserveSig]
        new int FindSourceFile(
            [In] uint StartElement,
            [In][MarshalAs(UnmanagedType.LPStr)] string File,
            [In] DEBUG_FIND_SOURCE Flags,
            [Out] out uint FoundElement,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint FoundSize);

        [PreserveSig]
        new int GetSourceFileLineOffsets(
            [In][MarshalAs(UnmanagedType.LPStr)] string File,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            ulong[] Buffer,
            [In] int BufferLines,
            [Out] out uint FileLines);

        /* IDebugSymbols2 */

        [PreserveSig]
        new int GetModuleVersionInformation(
            [In] uint Index,
            [In] ulong Base,
            [In][MarshalAs(UnmanagedType.LPStr)] string Item,
            [Out][MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]
            byte[] buffer,
            [In] uint BufferSize,
            [Out] out uint VerInfoSize);

        [PreserveSig]
        new int GetModuleNameString(
            [In] DEBUG_MODNAME Which,
            [In] uint Index,
            [In] ulong Base,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] uint BufferSize,
            [Out] out uint NameSize);

        [PreserveSig]
        new int GetConstantName(
            [In] ulong Module,
            [In] uint TypeId,
            [In] ulong Value,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint NameSize);

        [PreserveSig]
        new int GetFieldName(
            [In] ulong Module,
            [In] uint TypeId,
            [In] uint FieldIndex,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint NameSize);

        [PreserveSig]
        new int GetTypeOptions(
            [Out] out DEBUG_TYPEOPTS Options);

        [PreserveSig]
        new int AddTypeOptions(
            [In] DEBUG_TYPEOPTS Options);

        [PreserveSig]
        new int RemoveTypeOptions(
            [In] DEBUG_TYPEOPTS Options);

        [PreserveSig]
        new int SetTypeOptions(
            [In] DEBUG_TYPEOPTS Options);

        /* IDebugSymbols3 */

        [PreserveSig]
        int GetNameByOffsetWide(
            [In] ulong Offset,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder NameBuffer,
            [In] int NameBufferSize,
            [Out] out uint NameSize,
            [Out] out ulong Displacement);

        [PreserveSig]
        int GetOffsetByNameWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Symbol,
            [Out] out ulong Offset);

        [PreserveSig]
        int GetNearNameByOffsetWide(
            [In] ulong Offset,
            [In] int Delta,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder NameBuffer,
            [In] int NameBufferSize,
            [Out] out uint NameSize,
            [Out] out ulong Displacement);

        [PreserveSig]
        int GetLineByOffsetWide(
            [In] ulong Offset,
            [Out] out uint Line,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder FileBuffer,
            [In] int FileBufferSize,
            [Out] out uint FileSize,
            [Out] out ulong Displacement);

        [PreserveSig]
        int GetOffsetByLineWide(
            [In] uint Line,
            [In][MarshalAs(UnmanagedType.LPWStr)] string File,
            [Out] out ulong Offset);

        [PreserveSig]
        int GetModuleByModuleNameWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Name,
            [In] uint StartIndex,
            [Out] out uint Index,
            [Out] out ulong Base);

        [PreserveSig]
        int GetSymbolModuleWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Symbol,
            [Out] out ulong Base);

        [PreserveSig]
        int GetTypeNameWide(
            [In] ulong Module,
            [In] uint TypeId,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder NameBuffer,
            [In] int NameBufferSize,
            [Out] out uint NameSize);

        [PreserveSig]
        int GetTypeIdWide(
            [In] ulong Module,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Name,
            [Out] out uint TypeId);

        [PreserveSig]
        int GetFieldOffsetWide(
            [In] ulong Module,
            [In] uint TypeId,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Field,
            [Out] out uint Offset);

        [PreserveSig]
        int GetSymbolTypeIdWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Symbol,
            [Out] out uint TypeId,
            [Out] out ulong Module);

        [PreserveSig]
        int GetScopeSymbolGroup2(
            [In] DEBUG_SCOPE_GROUP Flags,
            [In][MarshalAs(UnmanagedType.Interface)]
            IDebugSymbolGroup2 Update,
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugSymbolGroup2 Symbols);

        [PreserveSig]
        int CreateSymbolGroup2(
            [Out][MarshalAs(UnmanagedType.Interface)]
            out IDebugSymbolGroup2 Group);

        [PreserveSig]
        int StartSymbolMatchWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Pattern,
            [Out] out ulong Handle);

        [PreserveSig]
        int GetNextSymbolMatchWide(
            [In] ulong Handle,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint MatchSize,
            [Out] out ulong Offset);

        [PreserveSig]
        int ReloadWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Module);

        [PreserveSig]
        int GetSymbolPathWide(
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint PathSize);

        [PreserveSig]
        int SetSymbolPathWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Path);

        [PreserveSig]
        int AppendSymbolPathWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Addition);

        [PreserveSig]
        int GetImagePathWide(
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint PathSize);

        [PreserveSig]
        int SetImagePathWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Path);

        [PreserveSig]
        int AppendImagePathWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Addition);

        [PreserveSig]
        int GetSourcePathWide(
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint PathSize);

        [PreserveSig]
        int GetSourcePathElementWide(
            [In] uint Index,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint ElementSize);

        [PreserveSig]
        int SetSourcePathWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Path);

        [PreserveSig]
        int AppendSourcePathWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Addition);

        [PreserveSig]
        int FindSourceFileWide(
            [In] uint StartElement,
            [In][MarshalAs(UnmanagedType.LPWStr)] string File,
            [In] DEBUG_FIND_SOURCE Flags,
            [Out] out uint FoundElement,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint FoundSize);

        [PreserveSig]
        int GetSourceFileLineOffsetsWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string File,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            ulong[] Buffer,
            [In] int BufferLines,
            [Out] out uint FileLines);

        [PreserveSig]
        int GetModuleVersionInformationWide(
            [In] uint Index,
            [In] ulong Base,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Item,
            [In] IntPtr Buffer,
            [In] int BufferSize,
            [Out] out uint VerInfoSize);

        [PreserveSig]
        int GetModuleNameStringWide(
            [In] DEBUG_MODNAME Which,
            [In] uint Index,
            [In] ulong Base,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint NameSize);

        [PreserveSig]
        int GetConstantNameWide(
            [In] ulong Module,
            [In] uint TypeId,
            [In] ulong Value,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint NameSize);

        [PreserveSig]
        int GetFieldNameWide(
            [In] ulong Module,
            [In] uint TypeId,
            [In] uint FieldIndex,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint NameSize);

        [PreserveSig]
        int IsManagedModule(
            [In] uint Index,
            [In] ulong Base
        );

        [PreserveSig]
        int GetModuleByModuleName2(
            [In][MarshalAs(UnmanagedType.LPStr)] string Name,
            [In] uint StartIndex,
            [In] DEBUG_GETMOD Flags,
            [Out] out uint Index,
            [Out] out ulong Base
        );

        [PreserveSig]
        int GetModuleByModuleName2Wide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Name,
            [In] uint StartIndex,
            [In] DEBUG_GETMOD Flags,
            [Out] out uint Index,
            [Out] out ulong Base
        );

        [PreserveSig]
        int GetModuleByOffset2(
            [In] ulong Offset,
            [In] uint StartIndex,
            [In] DEBUG_GETMOD Flags,
            [Out] out uint Index,
            [Out] out ulong Base
        );

        [PreserveSig]
        int AddSyntheticModule(
            [In] ulong Base,
            [In] uint Size,
            [In][MarshalAs(UnmanagedType.LPStr)] string ImagePath,
            [In][MarshalAs(UnmanagedType.LPStr)] string ModuleName,
            [In] DEBUG_ADDSYNTHMOD Flags
        );

        [PreserveSig]
        int AddSyntheticModuleWide(
            [In] ulong Base,
            [In] uint Size,
            [In][MarshalAs(UnmanagedType.LPWStr)] string ImagePath,
            [In][MarshalAs(UnmanagedType.LPWStr)] string ModuleName,
            [In] DEBUG_ADDSYNTHMOD Flags
        );

        [PreserveSig]
        int RemoveSyntheticModule(
            [In] ulong Base
        );

        [PreserveSig]
        int GetCurrentScopeFrameIndex(
            [Out] out uint Index
        );

        [PreserveSig]
        int SetScopeFrameByIndex(
            [In] uint Index
        );

        [PreserveSig]
        int SetScopeFromJitDebugInfo(
            [In] uint OutputControl,
            [In] ulong InfoOffset
        );

        [PreserveSig]
        int SetScopeFromStoredEvent(
        );

        [PreserveSig]
        int OutputSymbolByOffset(
            [In] uint OutputControl,
            [In] DEBUG_OUTSYM Flags,
            [In] ulong Offset
        );

        [PreserveSig]
        int GetFunctionEntryByOffset(
            [In] ulong Offset,
            [In] DEBUG_GETFNENT Flags,
            [In] IntPtr Buffer,
            [In] uint BufferSize,
            [Out] out uint BufferNeeded
        );

        [PreserveSig]
        int GetFieldTypeAndOffset(
            [In] ulong Module,
            [In] uint ContainerTypeId,
            [In][MarshalAs(UnmanagedType.LPStr)] string Field,
            [Out] out uint FieldTypeId,
            [Out] out uint Offset
        );

        [PreserveSig]
        int GetFieldTypeAndOffsetWide(
            [In] ulong Module,
            [In] uint ContainerTypeId,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Field,
            [Out] out uint FieldTypeId,
            [Out] out uint Offset
        );

        [PreserveSig]
        int AddSyntheticSymbol(
            [In] ulong Offset,
            [In] uint Size,
            [In][MarshalAs(UnmanagedType.LPStr)] string Name,
            [In] DEBUG_ADDSYNTHSYM Flags,
            [Out] out DEBUG_MODULE_AND_ID Id
        );

        [PreserveSig]
        int AddSyntheticSymbolWide(
            [In] ulong Offset,
            [In] uint Size,
            [In][MarshalAs(UnmanagedType.LPWStr)] string Name,
            [In] DEBUG_ADDSYNTHSYM Flags,
            [Out] out DEBUG_MODULE_AND_ID Id
        );

        [PreserveSig]
        int RemoveSyntheticSymbol(
            [In][MarshalAs(UnmanagedType.LPStruct)]
            DEBUG_MODULE_AND_ID Id
        );

        [PreserveSig]
        int GetSymbolEntriesByOffset(
            [In] ulong Offset,
            [In] uint Flags,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_MODULE_AND_ID[] Ids,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            ulong[] Displacements,
            [In] uint IdsCount,
            [Out] out uint Entries
        );

        [PreserveSig]
        int GetSymbolEntriesByName(
            [In][MarshalAs(UnmanagedType.LPStr)] string Symbol,
            [In] uint Flags,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_MODULE_AND_ID[] Ids,
            [In] uint IdsCount,
            [Out] out uint Entries
        );

        [PreserveSig]
        int GetSymbolEntriesByNameWide(
            [In][MarshalAs(UnmanagedType.LPWStr)] string Symbol,
            [In] uint Flags,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_MODULE_AND_ID[] Ids,
            [In] uint IdsCount,
            [Out] out uint Entries
        );

        [PreserveSig]
        int GetSymbolEntryByToken(
            [In] ulong ModuleBase,
            [In] uint Token,
            [Out] out DEBUG_MODULE_AND_ID Id
        );

        [PreserveSig]
        int GetSymbolEntryInformation(
            [In][MarshalAs(UnmanagedType.LPStruct)]
            DEBUG_MODULE_AND_ID Id,
            [Out] out DEBUG_SYMBOL_ENTRY Info
        );

        [PreserveSig]
        int GetSymbolEntryString(
            [In][MarshalAs(UnmanagedType.LPStruct)]
            DEBUG_MODULE_AND_ID Id,
            [In] uint Which,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint StringSize
        );

        [PreserveSig]
        int GetSymbolEntryStringWide(
            [In][MarshalAs(UnmanagedType.LPStruct)]
            DEBUG_MODULE_AND_ID Id,
            [In] uint Which,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint StringSize
        );

        [PreserveSig]
        int GetSymbolEntryOffsetRegions(
            [In][MarshalAs(UnmanagedType.LPStruct)]
            DEBUG_MODULE_AND_ID Id,
            [In] uint Flags,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_OFFSET_REGION[] Regions,
            [In] uint RegionsCount,
            [Out] out uint RegionsAvail
        );

        [Obsolete("Do not use: no longer implemented.", true)]
        [PreserveSig]
        int GetSymbolEntryBySymbolEntry(
            [In][MarshalAs(UnmanagedType.LPStruct)]
            DEBUG_MODULE_AND_ID FromId,
            [In] uint Flags,
            [Out] out DEBUG_MODULE_AND_ID ToId
        );

        [PreserveSig]
        int GetSourceEntriesByOffset(
            [In] ulong Offset,
            [In] uint Flags,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_SYMBOL_SOURCE_ENTRY[] Entries,
            [In] uint EntriesCount,
            [Out] out uint EntriesAvail
        );

        [PreserveSig]
        int GetSourceEntriesByLine(
            [In] uint Line,
            [In][MarshalAs(UnmanagedType.LPStr)] string File,
            [In] uint Flags,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_SYMBOL_SOURCE_ENTRY[] Entries,
            [In] uint EntriesCount,
            [Out] out uint EntriesAvail
        );

        [PreserveSig]
        int GetSourceEntriesByLineWide(
            [In] uint Line,
            [In][MarshalAs(UnmanagedType.LPWStr)] string File,
            [In] uint Flags,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_SYMBOL_SOURCE_ENTRY[] Entries,
            [In] uint EntriesCount,
            [Out] out uint EntriesAvail
        );

        [PreserveSig]
        int GetSourceEntryString(
            [In][MarshalAs(UnmanagedType.LPStruct)]
            DEBUG_SYMBOL_SOURCE_ENTRY Entry,
            [In] uint Which,
            [Out][MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint StringSize
        );

        [PreserveSig]
        int GetSourceEntryStringWide(
            [In][MarshalAs(UnmanagedType.LPStruct)]
            DEBUG_SYMBOL_SOURCE_ENTRY Entry,
            [In] uint Which,
            [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] int BufferSize,
            [Out] out uint StringSize
        );

        [PreserveSig]
        int GetSourceEntryOffsetRegions(
            [In][MarshalAs(UnmanagedType.LPStruct)]
            DEBUG_SYMBOL_SOURCE_ENTRY Entry,
            [In] uint Flags,
            [Out][MarshalAs(UnmanagedType.LPArray)]
            DEBUG_OFFSET_REGION[] Regions,
            [In] uint RegionsCount,
            [Out] out uint RegionsAvail
        );

        [PreserveSig]
        int GetSourceEntryBySourceEntry(
            [In][MarshalAs(UnmanagedType.LPStruct)]
            DEBUG_SYMBOL_SOURCE_ENTRY FromEntry,
            [In] uint Flags,
            [Out] out DEBUG_SYMBOL_SOURCE_ENTRY ToEntry
        );
    }
}