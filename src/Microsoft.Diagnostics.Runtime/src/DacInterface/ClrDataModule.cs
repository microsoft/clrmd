// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    public sealed unsafe class ClrDataModule : CallableCOMWrapper
    {
        private const uint DACDATAMODULEPRIV_REQUEST_GET_MODULEDATA = 0xf0000001;

        private static readonly Guid IID_IXCLRDataModule = new Guid("88E32849-0A0A-4cb0-9022-7CD2E9E139E2");

        private RequestDelegate? _request;

        public ClrDataModule(DacLibrary library, IntPtr pUnknown)
            : base(library?.OwningLibrary, IID_IXCLRDataModule, pUnknown)
        {
        }

        private ref readonly IClrDataModuleVTable VTable => ref Unsafe.AsRef<IClrDataModuleVTable>(_vtable);

        public bool GetModuleData(out ExtendedModuleData data)
        {
            InitDelegate(ref _request, VTable.Request);

            fixed (void* dataPtr = &data)
                if (SUCCEEDED(_request(Self, DACDATAMODULEPRIV_REQUEST_GET_MODULEDATA, 0, null, sizeof(ExtendedModuleData), dataPtr)))
                    return true;

            data = default;
            return false;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int RequestDelegate(IntPtr self, uint reqCode, int inBufferSize, void* inBuffer, int outBufferSize, void* outBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct IClrDataModuleVTable
        {
            private readonly IntPtr StartEnumAssemblies;
            private readonly IntPtr EnumAssembly;
            private readonly IntPtr EndEnumAssemblies;
            private readonly IntPtr StartEnumTypeDefinitions;
            private readonly IntPtr EnumTypeDefinition;
            private readonly IntPtr EndEnumTypeDefinitions;
            private readonly IntPtr StartEnumTypeInstances;
            private readonly IntPtr EnumTypeInstance;
            private readonly IntPtr EndEnumTypeInstances;
            private readonly IntPtr StartEnumTypeDefinitionsByName;
            private readonly IntPtr EnumTypeDefinitionByName;
            private readonly IntPtr EndEnumTypeDefinitionsByName;
            private readonly IntPtr StartEnumTypeInstancesByName;
            private readonly IntPtr EnumTypeInstanceByName;
            private readonly IntPtr EndEnumTypeInstancesByName;
            private readonly IntPtr GetTypeDefinitionByToken;
            private readonly IntPtr StartEnumMethodDefinitionsByName;
            private readonly IntPtr EnumMethodDefinitionByName;
            private readonly IntPtr EndEnumMethodDefinitionsByName;
            private readonly IntPtr StartEnumMethodInstancesByName;
            private readonly IntPtr EnumMethodInstanceByName;
            private readonly IntPtr EndEnumMethodInstancesByName;
            private readonly IntPtr GetMethodDefinitionByToken;
            private readonly IntPtr StartEnumDataByName;
            private readonly IntPtr EnumDataByName;
            private readonly IntPtr EndEnumDataByName;
            private readonly IntPtr GetName;
            private readonly IntPtr GetFileName;
            private readonly IntPtr GetFlags;
            private readonly IntPtr IsSameObject;
            private readonly IntPtr StartEnumExtents;
            private readonly IntPtr EnumExtent;
            private readonly IntPtr EndEnumExtents;
            public readonly IntPtr Request;
            private readonly IntPtr StartEnumAppDomains;
            private readonly IntPtr EnumAppDomain;
            private readonly IntPtr EndEnumAppDomains;
            private readonly IntPtr GetVersionId;
        }
    }
}
