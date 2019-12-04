// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DbgEng
{
    internal unsafe sealed class DebugSymbols : CallableCOMWrapper
    {
        internal static Guid IID_IDebugSymbols3 = new Guid("f02fbecc-50ac-4f36-9ad9-c975e8f32ff8");
        private IDebugSymbols3VTable* VTable => (IDebugSymbols3VTable*)_vtable;
        public DebugSymbols(RefCountedFreeLibrary library, IntPtr pUnk, DebugSystemObjects sys)
            : base(library, ref IID_IDebugSymbols3, pUnk)
        {
            _sys = sys;
            SuppressRelease();
        }

        public string GetModuleNameStringWide(DebugModuleName which, int index, ulong imageBase)
        {
            InitDelegate(ref _getModuleNameStringWide, VTable->GetModuleNameStringWide);

            using IDisposable holder = _sys.Enter();
            int hr = _getModuleNameStringWide(Self, which, index, imageBase, null, 0, out int needed);
            if (hr < 0)
                return null;

            StringBuilder sb = new StringBuilder(needed);
            hr = _getModuleNameStringWide(Self, which, index, imageBase, sb, sb.Capacity, out _);
            if (hr != 0)
                return null;

            return sb.ToString();
        }

        public int GetNumberModules()
        {
            InitDelegate(ref _getNumberModules, VTable->GetNumberModules);

            using IDisposable holder = _sys.Enter();
            int hr = _getNumberModules(Self, out int count, out _);
            return hr == 0 ? count : 0;
        }

        public ulong GetModuleByIndex(int i)
        {
            InitDelegate(ref _getModuleByIndex, VTable->GetModuleByIndex);

            using IDisposable holder = _sys.Enter();
            int hr = _getModuleByIndex(Self, i, out ulong imageBase);
            return hr == S_OK ? imageBase : 0;
        }

        public bool GetModuleParameters(ulong[] bases, out DEBUG_MODULE_PARAMETERS[] parameters)
        {
            InitDelegate(ref _getModuleParameters, VTable->GetModuleParameters);

            parameters = new DEBUG_MODULE_PARAMETERS[bases.Length];

            fixed (ulong* pBases = bases)
            fixed (DEBUG_MODULE_PARAMETERS* pParams = parameters)
            {
                using IDisposable holder = _sys.Enter();
                int hr = _getModuleParameters(Self, bases.Length, pBases, 0, pParams);
                return hr == S_OK;
            }
        }

        public VersionInfo GetModuleVersionInformation(int index, ulong imgBase)
        {
            InitDelegate(ref _getModuleVersionInformation, VTable->GetModuleVersionInformation);

            byte* item = stackalloc byte[3] { (byte)'\\', (byte)'\\', 0 };

            using IDisposable holder = _sys.Enter();
            int hr = _getModuleVersionInformation(Self, index, imgBase, item, null, 0, out int needed);
            if (hr < 0)
                return default;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(needed);
            try
            {
                fixed (byte* pBuffer = buffer)
                    hr = _getModuleVersionInformation(Self, index, imgBase, item, pBuffer, buffer.Length, out needed);

                if (hr < 0)
                    return default;

                int minor = (ushort)Marshal.ReadInt16(buffer, 8);
                int major = (ushort)Marshal.ReadInt16(buffer, 10);
                int patch = (ushort)Marshal.ReadInt16(buffer, 12);
                int revision = (ushort)Marshal.ReadInt16(buffer, 14);


                return new VersionInfo(major, minor, revision, patch);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public bool GetModuleByOffset(ulong address, int index, out int outIndex, out ulong imgBase)
        {
            InitDelegate(ref _getModuleByOffset, VTable->GetModuleByOffset);

            using IDisposable holder = _sys.Enter();
            int hr = _getModuleByOffset(Self, address, index, out outIndex, out imgBase);
            return hr == S_OK;
        }


        private GetModuleByOffsetDelegate _getModuleByOffset;
        private GetModuleVersionInformationDelegate _getModuleVersionInformation;
        private GetModuleParametersDelegate _getModuleParameters;
        private GetModuleByIndexDelegate _getModuleByIndex;
        private GetNumberModulesDelegate _getNumberModules;
        private GetModuleNameStringWideDelegate _getModuleNameStringWide;
        private readonly DebugSystemObjects _sys;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetModuleNameStringWideDelegate(IntPtr self, DebugModuleName Which, int Index, ulong Base,
                                    [Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer, int BufferSize, out int NameSize);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetNumberModulesDelegate(IntPtr self, out int count, out int unloaded);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetModuleByIndexDelegate(IntPtr self, int index, out ulong imageBase);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetModuleParametersDelegate(IntPtr self, int count, ulong* bases, int start, DEBUG_MODULE_PARAMETERS* parameters);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetModuleVersionInformationDelegate(IntPtr self, int index, ulong baseAddress, byte* item, byte* buffer, int bufferSize, out int needed);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetModuleByOffsetDelegate(IntPtr self, ulong offset, int index, out int outIndex, out ulong imgBase);
    }

#pragma warning disable CS0169
#pragma warning disable CS0649
#pragma warning disable IDE0051
#pragma warning disable CA1823

    internal struct IDebugSymbols3VTable
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

