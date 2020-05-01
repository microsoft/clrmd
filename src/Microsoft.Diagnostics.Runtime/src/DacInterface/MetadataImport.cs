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
using System.Text;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    public sealed unsafe class MetadataImport : CallableCOMWrapper
    {
        private static readonly Guid IID_IMetaDataImport = new Guid("7DAC8207-D3AE-4c75-9B67-92801A497D44");

        public MetadataImport(DacLibrary library, IntPtr pUnknown)
            : base(library?.OwningLibrary, IID_IMetaDataImport, pUnknown)
        {
        }

        private ref readonly IMetaDataImportVTable VTable => ref Unsafe.AsRef<IMetaDataImportVTable>(_vtable);

        public IEnumerable<int> EnumerateInterfaceImpls(int token)
        {
            InitDelegate(ref _enumInterfaceImpls, VTable.EnumInterfaceImpls);

            IntPtr handle = IntPtr.Zero;
            int[] tokens = ArrayPool<int>.Shared.Rent(32);
            try
            {
                while (_enumInterfaceImpls(Self, ref handle, token, tokens, tokens.Length, out int count) && count > 0)
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

        public MethodAttributes GetMethodAttributes(int token)
        {
            InitDelegate(ref _getMethodProps, VTable.GetMethodProps);

            HResult hr = _getMethodProps(Self, token, out int cls, null, 0, out int needed, out MethodAttributes result, out IntPtr sigBlob, out uint sigBlobLen, out uint codeRVA, out uint implFlags);
            return hr ? result : default;
        }

        public uint GetRva(int token)
        {
            InitDelegate(ref _getRVA, VTable.GetRVA);

            HResult hr = _getRVA(Self, token, out uint rva, out uint flags);
            return hr ? rva : 0;
        }

        public HResult GetTypeDefProperties(int token, out string? name, out TypeAttributes attributes, out int mdParent)
        {
            InitDelegate(ref _getTypeDefProps, VTable.GetTypeDefProps);

            HResult hr = _getTypeDefProps(Self, token, null, 0, out int needed, out attributes, out mdParent);
            if (!hr)
            {
                name = null;
                return hr;
            }

            string nameResult = new string('\0', needed - 1);
            fixed (char* nameResultPtr = nameResult)
                hr = _getTypeDefProps(Self, token, nameResultPtr, needed, out needed, out attributes, out mdParent);

            name = hr ? nameResult : null;
            return hr;
        }

        public HResult GetCustomAttributeByName(int token, string name, out IntPtr data, out uint cbData)
        {
            InitDelegate(ref _getCustomAttributeByName, VTable.GetCustomAttributeByName);
            return _getCustomAttributeByName(Self, token, name, out data, out cbData);
        }

        public HResult GetFieldProps(int token, out string? name, out FieldAttributes attrs, out IntPtr ppvSigBlob, out int pcbSigBlob, out int pdwCPlusTypeFlag, out IntPtr ppValue)
        {
            InitDelegate(ref _getFieldProps, VTable.GetFieldProps);

            HResult hr = _getFieldProps(Self, token, out int typeDef, null, 0, out int needed, out attrs, out ppvSigBlob, out pcbSigBlob, out pdwCPlusTypeFlag, out ppValue, out int pcchValue);
            if (!hr)
            {
                name = null;
                return hr;
            }

            string nameResult = new string('\0', needed - 1);
            fixed (char* nameResultPtr = nameResult)
                hr = _getFieldProps(Self, token, out typeDef, nameResultPtr, needed, out needed, out attrs, out ppvSigBlob, out pcbSigBlob, out pdwCPlusTypeFlag, out ppValue, out pcchValue);

            name = hr ? nameResult : null;
            return hr;
        }

        public IEnumerable<int> EnumerateFields(int token)
        {
            InitDelegate(ref _enumFields, VTable.EnumFields);

            IntPtr handle = IntPtr.Zero;
            int[] tokens = ArrayPool<int>.Shared.Rent(32);
            try
            {
                while (_enumFields(Self, ref handle, token, tokens, tokens.Length, out int count) && count > 0)
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

        internal HResult GetTypeDefAttributes(int token, out TypeAttributes attrs)
        {
            InitDelegate(ref _getTypeDefProps, VTable.GetTypeDefProps);
            return _getTypeDefProps(Self, token, null, 0, out int needed, out attrs, out int extends);
        }

        public string? GetTypeRefName(int token)
        {
            InitDelegate(ref _getTypeRefProps, VTable.GetTypeRefProps);

            if (!_getTypeRefProps(Self, token, out int scope, null, 0, out int needed))
                return null;

            string nameResult = new string('\0', needed - 1);
            fixed (char* nameResultPtr = nameResult)
                if (_getTypeRefProps(Self, token, out scope, nameResultPtr, needed, out needed))
                    return nameResult;

            return null;
        }

        public HResult GetNestedClassProperties(int token, out int enclosing)
        {
            InitDelegate(ref _getNestedClassProps, VTable.GetNestedClassProps);
            return _getNestedClassProps(Self, token, out enclosing);
        }

        public HResult GetInterfaceImplProps(int token, out int mdClass, out int mdInterface)
        {
            InitDelegate(ref _getInterfaceImplProps, VTable.GetInterfaceImplProps);
            return _getInterfaceImplProps(Self, token, out mdClass, out mdInterface);
        }

        private void CloseEnum(IntPtr handle)
        {
            if (handle != IntPtr.Zero)
            {
                InitDelegate(ref _closeEnum, VTable.CloseEnum);
                _closeEnum(Self, handle);
            }
        }

        public IEnumerable<int> EnumerateGenericParams(int token)
        {
            InitDelegate(ref _enumGenericParams, VTable.EnumGenericParams);

            IntPtr handle = IntPtr.Zero;
            int[] tokens = ArrayPool<int>.Shared.Rent(32);
            try
            {
                while (_enumGenericParams(Self, ref handle, token, tokens, tokens.Length, out int count) && count > 0)
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

        public bool GetGenericParamProps(int token, out int index, out GenericParameterAttributes attributes, [NotNullWhen(true)] out string? name)
        {
            // [NotNullWhen(true)] does not like returning HResult from this method
            name = null;
            InitDelegate(ref _getGenericParamProps, VTable.GetGenericParamProps);

            HResult hr = _getGenericParamProps(Self, token, out index, out attributes, out int owner, out _, null, 0, out int needed);

            if (hr < 0)
                return false;

            string nameResult = new string('\0', needed - 1);
            fixed (char* nameResultPtr = nameResult)
                hr = _getGenericParamProps(Self, token, out index, out attributes, out owner, out _, nameResultPtr, nameResult.Length + 1, out needed);

            if (hr < 0)
                return false;

            name = nameResult;
            return true;
        }


        private EnumInterfaceImplsDelegate? _enumInterfaceImpls;
        private CloseEnumDelegate? _closeEnum;
        private GetInterfaceImplPropsDelegate? _getInterfaceImplProps;
        private GetTypeRefPropsDelegate? _getTypeRefProps;
        private GetTypeDefPropsDelegate? _getTypeDefProps;
        private EnumFieldsDelegate? _enumFields;
        private GetRVADelegate? _getRVA;
        private GetMethodPropsDelegate? _getMethodProps;
        private GetNestedClassPropsDelegate? _getNestedClassProps;
        private GetFieldPropsDelegate? _getFieldProps;
        private GetCustomAttributeByNameDelegate? _getCustomAttributeByName;
        private EnumGenericParamsDelegate? _enumGenericParams;
        private GetGenericParamPropsDelegate? _getGenericParamProps;

        private delegate HResult CloseEnumDelegate(IntPtr self, IntPtr e);
        private delegate HResult EnumInterfaceImplsDelegate(IntPtr self, ref IntPtr phEnum, int td, [Out] int[] rImpls, int cMax, out int pCount);
        private delegate HResult GetInterfaceImplPropsDelegate(IntPtr self, int mdImpl, out int mdClass, out int mdIFace);
        private delegate HResult GetTypeRefPropsDelegate(IntPtr self, int token, out int resolutionScopeToken, char* szName,
                                                         int bufferSize, out int needed);
        private delegate HResult GetTypeDefPropsDelegate(IntPtr self, int token, char* szTypeDef,
                                                     int cchTypeDef, out int pchTypeDef, out TypeAttributes pdwTypeDefFlags, out int ptkExtends);
        private delegate HResult EnumFieldsDelegate(IntPtr self, ref IntPtr phEnum, int cl, int[] mdFieldDef, int cMax, out int pcTokens);
        private delegate HResult GetRVADelegate(IntPtr self, int token, out uint pRva, out uint flags);
        private delegate HResult GetMethodPropsDelegate(IntPtr self, int md, out int pClass, StringBuilder? szMethod, int cchMethod, out int pchMethod,
            out MethodAttributes pdwAttr, out IntPtr ppvSigBlob, out uint pcbSigBlob, out uint pulCodeRVA, out uint pdwImplFlags);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate HResult GetNestedClassPropsDelegate(IntPtr self, int tdNestedClass, out int tdEnclosingClass);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate HResult GetFieldPropsDelegate(IntPtr self, int mb, out int mdTypeDef, char* szField,
                                                       int cchField, out int pchField, out FieldAttributes pdwAttr, out IntPtr ppvSigBlob, out int pcbSigBlob,
                                                       out int pdwCPlusTypeFlab, out IntPtr ppValue, out int pcchValue);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate HResult GetCustomAttributeByNameDelegate(IntPtr self, int tkObj, [MarshalAs(UnmanagedType.LPWStr)] string szName, out IntPtr ppData, out uint pcbData);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate HResult EnumGenericParamsDelegate(
            IntPtr self,
            ref IntPtr phEnum,
            int tk,
            [Out] int[] rGenericParams,
            int cMax,
            out int pcGenericParams);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate HResult GetGenericParamPropsDelegate(
            IntPtr self,
            int gp,
            out int pulParamSeq,
            out GenericParameterAttributes pdwParamFlags,
            out int ptOwner,
            out int reserved,
            char* wzname,
            int cchName,
            out int pchName);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct IMetaDataImportVTable
    {
        public readonly IntPtr CloseEnum;
        private readonly IntPtr CountEnum;
        private readonly IntPtr ResetEnum;
        private readonly IntPtr EnumTypeDefs;
        public readonly IntPtr EnumInterfaceImpls;
        private readonly IntPtr EnumTypeRefs;
        private readonly IntPtr FindTypeDefByName;
        private readonly IntPtr GetScopeProps;
        private readonly IntPtr GetModuleFromScope;
        public readonly IntPtr GetTypeDefProps;
        public readonly IntPtr GetInterfaceImplProps;
        public readonly IntPtr GetTypeRefProps;
        private readonly IntPtr ResolveTypeRef;
        private readonly IntPtr EnumMembers;
        private readonly IntPtr EnumMembersWithName;
        private readonly IntPtr EnumMethods;
        private readonly IntPtr EnumMethodsWithName;
        public readonly IntPtr EnumFields;
        private readonly IntPtr EnumFieldsWithName;
        private readonly IntPtr EnumParams;
        private readonly IntPtr EnumMemberRefs;
        private readonly IntPtr EnumMethodImpls;
        private readonly IntPtr EnumPermissionSets;
        private readonly IntPtr FindMember;
        private readonly IntPtr FindMethod;
        private readonly IntPtr FindField;
        private readonly IntPtr FindMemberRef;
        public readonly IntPtr GetMethodProps;
        private readonly IntPtr GetMemberRefProps;
        private readonly IntPtr EnumProperties;
        private readonly IntPtr EnumEvents;
        private readonly IntPtr GetEventProps;
        private readonly IntPtr EnumMethodSemantics;
        private readonly IntPtr GetMethodSemantics;
        private readonly IntPtr GetClassLayout;
        private readonly IntPtr GetFieldMarshal;
        public readonly IntPtr GetRVA;
        private readonly IntPtr GetPermissionSetProps;
        private readonly IntPtr GetSigFromToken;
        private readonly IntPtr GetModuleRefProps;
        private readonly IntPtr EnumModuleRefs;
        private readonly IntPtr GetTypeSpecFromToken;
        private readonly IntPtr GetNameFromToken;
        private readonly IntPtr EnumUnresolvedMethods;
        private readonly IntPtr GetUserString;
        private readonly IntPtr GetPinvokeMap;
        private readonly IntPtr EnumSignatures;
        private readonly IntPtr EnumTypeSpecs;
        private readonly IntPtr EnumUserStrings;
        private readonly IntPtr GetParamForMethodIndex;
        private readonly IntPtr EnumCustomAttributes;
        private readonly IntPtr GetCustomAttributeProps;
        private readonly IntPtr FindTypeRef;
        private readonly IntPtr GetMemberProps;
        public readonly IntPtr GetFieldProps;
        private readonly IntPtr GetPropertyProps;
        private readonly IntPtr GetParamProps;
        public readonly IntPtr GetCustomAttributeByName;
        private readonly IntPtr IsValidToken;
        public readonly IntPtr GetNestedClassProps;
        private readonly IntPtr GetNativeCallConvFromSig;
        private readonly IntPtr IsGlobal;

        // IMetaDataImport2
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