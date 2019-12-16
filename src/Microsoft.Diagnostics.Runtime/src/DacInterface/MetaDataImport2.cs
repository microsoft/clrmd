// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    public sealed unsafe class MetaDataImport2 : CallableCOMWrapper
    {
        private static readonly Guid IID_IMetaDataImport2 = new Guid("FCE5EFA0-8BBA-4f8e-A036-8F2022B08466");

        private CloseEnumDelegate? _closeEnum;
        private EnumGenericParamsDelegate? _enumGenericParams;
        private GetGenericParamPropsDelegate? _getGenericParamProps;

        public MetaDataImport2(DacLibrary library, IntPtr pUnknown)
            : base(library?.OwningLibrary, IID_IMetaDataImport2, pUnknown)
        {
        }

        private ref readonly IMetaDataImport2VTable VTable => ref Unsafe.AsRef<IMetaDataImport2VTable>(_vtable);

        public IEnumerable<int> EnumerateGenericParams(int token)
        {
            InitDelegate(ref _enumGenericParams, VTable.EnumGenericParams);

            IntPtr handle = IntPtr.Zero;
            int[] tokens = ArrayPool<int>.Shared.Rent(32);
            try
            {
                int hr;
                while ((hr = _enumGenericParams(Self, ref handle, token, tokens, tokens.Length, out int count)) >= 0 && count > 0)
                    for (int i = 0; i < count; i++)
                        yield return tokens[i];
            }
            finally
            {
                if (handle != IntPtr.Zero)
                    CloseEnum(handle);

                ArrayPool<int>.Shared.Return(tokens);
            }
        }

        public unsafe bool GetGenericParamProps(int token, out int index, out GenericParameterAttributes attributes, [NotNullWhen(true)] out string? name)
        {
            name = null;
            InitDelegate(ref _getGenericParamProps, VTable.GetGenericParamProps);

            int hr = _getGenericParamProps(
                Self, token, out index, out attributes, out int owner, out _, null, 0, out int needed);

            if (hr < 0)
                return false;

            string nameResult = new string('\0', needed - 1);
            fixed (char* nameResultPtr = nameResult)
                hr = _getGenericParamProps(
                    Self, token, out index, out attributes, out owner, out _, nameResultPtr, nameResult.Length + 1, out needed);

            if (hr < 0)
                return false;

            name = nameResult;
            return true;
        }

        private void CloseEnum(IntPtr handle)
        {
            if (handle != IntPtr.Zero)
            {
                InitDelegate(ref _closeEnum, VTable.BaseVTable.CloseEnum);
                _closeEnum(Self, handle);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CloseEnumDelegate(IntPtr self, IntPtr hEnum);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int EnumGenericParamsDelegate(
            IntPtr self,
            ref IntPtr phEnum,
            int tk,
            [Out] int[] rGenericParams,
            int cMax,
            out int pcGenericParams);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetGenericParamPropsDelegate(
            IntPtr self,
            int gp,
            out int pulParamSeq,
            out GenericParameterAttributes pdwParamFlags,
            out int ptOwner,
            out int reserved,
            char* wzname,
            int cchName,
            out int pchName);

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct IMetaDataImport2VTable
        {
            public readonly IMetaDataImportVTable BaseVTable;
            public readonly IntPtr EnumGenericParams;
            public readonly IntPtr GetGenericParamProps;
            private readonly IntPtr GetMethodSpecProps;
            private readonly IntPtr EnumGenericParamConstraints;
            private readonly IntPtr GetGenericParamConstraintProps;
            private readonly IntPtr GetPEKind;
            private readonly IntPtr GetVersionString;
            private readonly IntPtr EnumMethodSpecs;
        }
    }
}
