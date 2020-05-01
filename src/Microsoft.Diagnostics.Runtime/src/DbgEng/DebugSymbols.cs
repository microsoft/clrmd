// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DbgEng
{
    internal unsafe sealed class DebugSymbols : CallableCOMWrapper
    {
        internal static readonly Guid IID_IDebugSymbols3 = new Guid("f02fbecc-50ac-4f36-9ad9-c975e8f32ff8");

        public DebugSymbols(RefCountedFreeLibrary library, IntPtr pUnk, DebugSystemObjects sys)
            : base(library, IID_IDebugSymbols3, pUnk)
        {
            _sys = sys;
            SuppressRelease();
        }

        private ref readonly IDebugSymbols3VTable VTable => ref Unsafe.AsRef<IDebugSymbols3VTable>(_vtable);

        public string? GetModuleNameStringWide(DebugModuleName which, int index, ulong imageBase)
        {
            InitDelegate(ref _getModuleNameStringWide, VTable.GetModuleNameStringWide);

            using IDisposable holder = _sys.Enter();
            HResult hr = _getModuleNameStringWide(Self, which, index, imageBase, null, 0, out int needed);
            if (!hr)
                return null;

            string nameResult = new string('\0', needed - 1);
            fixed (char* nameResultPtr = nameResult)
                if (_getModuleNameStringWide(Self, which, index, imageBase, nameResultPtr, needed, out _))
                    return nameResult;

            return null;
        }

        public int GetNumberModules()
        {
            InitDelegate(ref _getNumberModules, VTable.GetNumberModules);

            using IDisposable holder = _sys.Enter();
            HResult hr = _getNumberModules(Self, out int count, out _);
            return hr ? count : 0;
        }

        public ulong GetModuleByIndex(int i)
        {
            InitDelegate(ref _getModuleByIndex, VTable.GetModuleByIndex);

            using IDisposable holder = _sys.Enter();
            HResult hr = _getModuleByIndex(Self, i, out ulong imageBase);
            return hr ? imageBase : 0;
        }

        public HResult GetModuleParameters(ulong[] bases, out DEBUG_MODULE_PARAMETERS[] parameters)
        {
            InitDelegate(ref _getModuleParameters, VTable.GetModuleParameters);

            parameters = new DEBUG_MODULE_PARAMETERS[bases.Length];

            fixed (ulong* pBases = bases)
            fixed (DEBUG_MODULE_PARAMETERS* pParams = parameters)
            {
                using IDisposable holder = _sys.Enter();
                HResult hr = _getModuleParameters(Self, bases.Length, pBases, 0, pParams);
                return hr;
            }
        }

        public VersionInfo GetModuleVersionInformation(int index, ulong imgBase)
        {
            InitDelegate(ref _getModuleVersionInformation, VTable.GetModuleVersionInformation);

            byte* item = stackalloc byte[3] { (byte)'\\', (byte)'\\', 0 };

            using IDisposable holder = _sys.Enter();
            HResult hr = _getModuleVersionInformation(Self, index, imgBase, item, null, 0, out int needed);
            if (!hr)
                return default;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(needed);
            try
            {
                fixed (byte* pBuffer = buffer)
                    hr = _getModuleVersionInformation(Self, index, imgBase, item, pBuffer, buffer.Length, out needed);

                if (!hr)
                    return default;

                int minor = Unsafe.As<byte, ushort>(ref buffer[8]);
                int major = Unsafe.As<byte, ushort>(ref buffer[10]);
                int patch = Unsafe.As<byte, ushort>(ref buffer[12]);
                int revision = Unsafe.As<byte, ushort>(ref buffer[14]);

                return new VersionInfo(major, minor, revision, patch, true);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public HResult GetModuleByOffset(ulong address, int index, out int outIndex, out ulong imgBase)
        {
            InitDelegate(ref _getModuleByOffset, VTable.GetModuleByOffset);

            using IDisposable holder = _sys.Enter();
            return _getModuleByOffset(Self, address, index, out outIndex, out imgBase);
        }

        private GetModuleByOffsetDelegate? _getModuleByOffset;
        private GetModuleVersionInformationDelegate? _getModuleVersionInformation;
        private GetModuleParametersDelegate? _getModuleParameters;
        private GetModuleByIndexDelegate? _getModuleByIndex;
        private GetNumberModulesDelegate? _getNumberModules;
        private GetModuleNameStringWideDelegate? _getModuleNameStringWide;
        private readonly DebugSystemObjects _sys;

        private delegate HResult GetModuleNameStringWideDelegate(IntPtr self, DebugModuleName Which, int Index, ulong Base, char* Buffer, int BufferSize, out int NameSize);
        private delegate HResult GetNumberModulesDelegate(IntPtr self, out int count, out int unloaded);
        private delegate HResult GetModuleByIndexDelegate(IntPtr self, int index, out ulong imageBase);
        private delegate HResult GetModuleParametersDelegate(IntPtr self, int count, ulong* bases, int start, DEBUG_MODULE_PARAMETERS* parameters);
        private delegate HResult GetModuleVersionInformationDelegate(IntPtr self, int index, ulong baseAddress, byte* item, byte* buffer, int bufferSize, out int needed);
        private delegate HResult GetModuleByOffsetDelegate(IntPtr self, ulong offset, int index, out int outIndex, out ulong imgBase);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct IDebugSymbols3VTable
    {
        public readonly IntPtr GetSymbolOptions;
        public readonly IntPtr AddSymbolOptions;
        public readonly IntPtr RemoveSymbolOptions;
        public readonly IntPtr SetSymbolOptions;
        public readonly IntPtr GetNameByOffset;
        public readonly IntPtr GetOffsetByName;
        public readonly IntPtr GetNearNameByOffset;
        public readonly IntPtr GetLineByOffset;
        public readonly IntPtr GetOffsetByLine;
        public readonly IntPtr GetNumberModules;
        public readonly IntPtr GetModuleByIndex;
        public readonly IntPtr GetModuleByModuleName;
        public readonly IntPtr GetModuleByOffset;
        public readonly IntPtr GetModuleNames;
        public readonly IntPtr GetModuleParameters;
        public readonly IntPtr GetSymbolModule;
        public readonly IntPtr GetTypeName;
        public readonly IntPtr GetTypeId;
        public readonly IntPtr GetTypeSize;
        public readonly IntPtr GetFieldOffset;
        public readonly IntPtr GetSymbolTypeId;
        public readonly IntPtr GetOffsetTypeId;
        public readonly IntPtr ReadTypedDataVirtual;
        public readonly IntPtr WriteTypedDataVirtual;
        public readonly IntPtr OutputTypedDataVirtual;
        public readonly IntPtr ReadTypedDataPhysical;
        public readonly IntPtr WriteTypedDataPhysical;
        public readonly IntPtr OutputTypedDataPhysical;
        public readonly IntPtr GetScope;
        public readonly IntPtr SetScope;
        public readonly IntPtr ResetScope;
        public readonly IntPtr GetScopeSymbolGroup;
        public readonly IntPtr CreateSymbolGroup;
        public readonly IntPtr StartSymbolMatch;
        public readonly IntPtr GetNextSymbolMatch;
        public readonly IntPtr EndSymbolMatch;
        public readonly IntPtr Reload;
        public readonly IntPtr GetSymbolPath;
        public readonly IntPtr SetSymbolPath;
        public readonly IntPtr AppendSymbolPath;
        public readonly IntPtr GetImagePath;
        public readonly IntPtr SetImagePath;
        public readonly IntPtr AppendImagePath;
        public readonly IntPtr GetSourcePath;
        public readonly IntPtr GetSourcePathElement;
        public readonly IntPtr SetSourcePath;
        public readonly IntPtr AppendSourcePath;
        public readonly IntPtr FindSourceFile;
        public readonly IntPtr GetSourceFileLineOffsets;
        public readonly IntPtr GetModuleVersionInformation;
        public readonly IntPtr GetModuleNameString;
        public readonly IntPtr GetConstantName;
        public readonly IntPtr GetFieldName;
        public readonly IntPtr GetTypeOptions;
        public readonly IntPtr AddTypeOptions;
        public readonly IntPtr RemoveTypeOptions;
        public readonly IntPtr SetTypeOptions;
        public readonly IntPtr GetNameByOffsetWide;
        public readonly IntPtr GetOffsetByNameWide;
        public readonly IntPtr GetNearNameByOffsetWide;
        public readonly IntPtr GetLineByOffsetWide;
        public readonly IntPtr GetOffsetByLineWide;
        public readonly IntPtr GetModuleByModuleNameWide;
        public readonly IntPtr GetSymbolModuleWide;
        public readonly IntPtr GetTypeNameWide;
        public readonly IntPtr GetTypeIdWide;
        public readonly IntPtr GetFieldOffsetWide;
        public readonly IntPtr GetSymbolTypeIdWide;
        public readonly IntPtr GetScopeSymbolGroup2;
        public readonly IntPtr CreateSymbolGroup2;
        public readonly IntPtr StartSymbolMatchWide;
        public readonly IntPtr GetNextSymbolMatchWide;
        public readonly IntPtr ReloadWide;
        public readonly IntPtr GetSymbolPathWide;
        public readonly IntPtr SetSymbolPathWide;
        public readonly IntPtr AppendSymbolPathWide;
        public readonly IntPtr GetImagePathWide;
        public readonly IntPtr SetImagePathWide;
        public readonly IntPtr AppendImagePathWide;
        public readonly IntPtr GetSourcePathWide;
        public readonly IntPtr GetSourcePathElementWide;
        public readonly IntPtr SetSourcePathWide;
        public readonly IntPtr AppendSourcePathWide;
        public readonly IntPtr FindSourceFileWide;
        public readonly IntPtr GetSourceFileLineOffsetsWide;
        public readonly IntPtr GetModuleVersionInformationWide;
        public readonly IntPtr GetModuleNameStringWide;
        public readonly IntPtr GetConstantNameWide;
        public readonly IntPtr GetFieldNameWide;
        public readonly IntPtr IsManagedModule;
        public readonly IntPtr GetModuleByModuleName2;
        public readonly IntPtr GetModuleByModuleName2Wide;
        public readonly IntPtr GetModuleByOffset2;
        public readonly IntPtr AddSyntheticModule;
        public readonly IntPtr AddSyntheticModuleWide;
        public readonly IntPtr RemoveSyntheticModule;
        public readonly IntPtr GetCurrentScopeFrameIndex;
        public readonly IntPtr SetScopeFrameByIndex;
        public readonly IntPtr SetScopeFromJitDebugInfo;
        public readonly IntPtr SetScopeFromStoredEvent;
        public readonly IntPtr OutputSymbolByOffset;
        public readonly IntPtr GetFunctionEntryByOffset;
        public readonly IntPtr GetFieldTypeAndOffset;
        public readonly IntPtr GetFieldTypeAndOffsetWide;
        public readonly IntPtr AddSyntheticSymbol;
        public readonly IntPtr AddSyntheticSymbolWide;
        public readonly IntPtr RemoveSyntheticSymbol;
        public readonly IntPtr GetSymbolEntriesByOffset;
        public readonly IntPtr GetSymbolEntriesByName;
        public readonly IntPtr GetSymbolEntriesByNameWide;
        public readonly IntPtr GetSymbolEntryByToken;
        public readonly IntPtr GetSymbolEntryInformation;
        public readonly IntPtr GetSymbolEntryString;
        public readonly IntPtr GetSymbolEntryStringWide;
        public readonly IntPtr GetSymbolEntryOffsetRegions;
        public readonly IntPtr GetSymbolEntryBySymbolEntry;
        public readonly IntPtr GetSourceEntriesByOffset;
        public readonly IntPtr GetSourceEntriesByLine;
        public readonly IntPtr GetSourceEntriesByLineWide;
        public readonly IntPtr GetSourceEntryString;
        public readonly IntPtr GetSourceEntryStringWide;
        public readonly IntPtr GetSourceEntryOffsetRegions;
        public readonly IntPtr GetSourceEntryBySourceEntry;
    }
}
