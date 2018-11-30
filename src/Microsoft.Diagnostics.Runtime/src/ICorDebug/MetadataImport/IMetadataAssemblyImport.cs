// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [Guid("EE62470B-E94B-424e-9B7C-2F00C9249F93")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    // IID_IMetadataAssemblyImport from cor.h

    // GUID Copied from Cor.h
    internal interface IMetadataAssemblyImport
    {
        //     STDMETHOD(GetAssemblyProps)(            // S_OK or error.
        //         mdAssembly  mda,                    // [IN] The Assembly for which to get the properties.
        //         const void  **ppbPublicKey,         // [OUT] Pointer to the public key.
        //         ULONG       *pcbPublicKey,          // [OUT] Count of bytes in the public key.
        //         ULONG       *pulHashAlgId,          // [OUT] Hash Algorithm.
        //         LPWSTR      szName,                 // [OUT] Buffer to fill with assembly's simply name.
        //         ULONG       cchName,                // [IN] Size of buffer in wide chars.
        //         ULONG       *pchName,               // [OUT] Actual # of wide chars in name.
        //         ASSEMBLYMETADATA *pMetaData,        // [OUT] Assembly MetaData.
        //         DWORD       *pdwAssemblyFlags) PURE;    // [OUT] Flags.

        //     STDMETHOD(GetAssemblyRefProps)(         // S_OK or error.
        //         mdAssemblyRef mdar,                 // [IN] The AssemblyRef for which to get the properties.
        //         const void  **ppbPublicKeyOrToken,  // [OUT] Pointer to the public key or token.
        //         ULONG       *pcbPublicKeyOrToken,   // [OUT] Count of bytes in the public key or token.
        //         LPWSTR      szName,                 // [OUT] Buffer to fill with name.
        //         ULONG       cchName,                // [IN] Size of buffer in wide chars.
        //         ULONG       *pchName,               // [OUT] Actual # of wide chars in name.
        //         ASSEMBLYMETADATA *pMetaData,        // [OUT] Assembly MetaData.
        //         const void  **ppbHashValue,         // [OUT] Hash blob.
        //         ULONG       *pcbHashValue,          // [OUT] Count of bytes in the hash blob.
        //         DWORD       *pdwAssemblyRefFlags) PURE; // [OUT] Flags.

        //     STDMETHOD(GetFileProps)(                // S_OK or error.
        //         mdFile      mdf,                    // [IN] The File for which to get the properties.
        //         LPWSTR      szName,                 // [OUT] Buffer to fill with name.
        //         ULONG       cchName,                // [IN] Size of buffer in wide chars.
        //         ULONG       *pchName,               // [OUT] Actual # of wide chars in name.
        //         const void  **ppbHashValue,         // [OUT] Pointer to the Hash Value Blob.
        //         ULONG       *pcbHashValue,          // [OUT] Count of bytes in the Hash Value Blob.
        //         DWORD       *pdwFileFlags) PURE;    // [OUT] Flags.

        //     STDMETHOD(GetExportedTypeProps)(        // S_OK or error.
        //         mdExportedType   mdct,              // [IN] The ExportedType for which to get the properties.
        //         LPWSTR      szName,                 // [OUT] Buffer to fill with name.
        //         ULONG       cchName,                // [IN] Size of buffer in wide chars.
        //         ULONG       *pchName,               // [OUT] Actual # of wide chars in name.
        //         mdToken     *ptkImplementation,     // [OUT] mdFile or mdAssemblyRef or mdExportedType.
        //         mdTypeDef   *ptkTypeDef,            // [OUT] TypeDef token within the file.
        //         DWORD       *pdwExportedTypeFlags) PURE; // [OUT] Flags.

        //     STDMETHOD(GetManifestResourceProps)(    // S_OK or error.
        //         mdManifestResource  mdmr,           // [IN] The ManifestResource for which to get the properties.
        //         LPWSTR      szName,                 // [OUT] Buffer to fill with name.
        //         ULONG       cchName,                // [IN] Size of buffer in wide chars.
        //         ULONG       *pchName,               // [OUT] Actual # of wide chars in name.
        //         mdToken     *ptkImplementation,     // [OUT] mdFile or mdAssemblyRef that provides the ManifestResource.
        //         DWORD       *pdwOffset,             // [OUT] Offset to the beginning of the resource within the file.
        //         DWORD       *pdwResourceFlags) PURE;// [OUT] Flags.

        //     STDMETHOD(EnumAssemblyRefs)(            // S_OK or error
        //         HCORENUM    *phEnum,                // [IN|OUT] Pointer to the enum.
        //         mdAssemblyRef rAssemblyRefs[],      // [OUT] Put AssemblyRefs here.
        //         ULONG       cMax,                   // [IN] Max AssemblyRefs to put.
        //         ULONG       *pcTokens) PURE;        // [OUT] Put # put here.

        //     STDMETHOD(EnumFiles)(                   // S_OK or error
        //         HCORENUM    *phEnum,                // [IN|OUT] Pointer to the enum.
        //         mdFile      rFiles[],               // [OUT] Put Files here.
        //         ULONG       cMax,                   // [IN] Max Files to put.
        //         ULONG       *pcTokens) PURE;        // [OUT] Put # put here.

        //     STDMETHOD(EnumExportedTypes)(           // S_OK or error
        //         HCORENUM    *phEnum,                // [IN|OUT] Pointer to the enum.
        //         mdExportedType   rExportedTypes[],  // [OUT] Put ExportedTypes here.
        //         ULONG       cMax,                   // [IN] Max ExportedTypes to put.
        //         ULONG       *pcTokens) PURE;        // [OUT] Put # put here.

        //     STDMETHOD(EnumManifestResources)(       // S_OK or error
        //         HCORENUM    *phEnum,                // [IN|OUT] Pointer to the enum.
        //         mdManifestResource  rManifestResources[],   // [OUT] Put ManifestResources here.
        //         ULONG       cMax,                   // [IN] Max Resources to put.
        //         ULONG       *pcTokens) PURE;        // [OUT] Put # put here.

        //     STDMETHOD(GetAssemblyFromScope)(        // S_OK or error
        //         mdAssembly  *ptkAssembly) PURE;     // [OUT] Put token here.

        //     STDMETHOD(FindExportedTypeByName)(      // S_OK or error
        //         LPCWSTR     szName,                 // [IN] Name of the ExportedType.
        //         mdToken     mdtExportedType,        // [IN] ExportedType for the enclosing class.
        //         mdExportedType   *ptkExportedType) PURE; // [OUT] Put the ExportedType token here.

        //     STDMETHOD(FindManifestResourceByName)(  // S_OK or error
        //         LPCWSTR     szName,                 // [IN] Name of the ManifestResource.
        //         mdManifestResource *ptkManifestResource) PURE;  // [OUT] Put the ManifestResource token here.

        //     STDMETHOD_(void, CloseEnum)(
        //         HCORENUM hEnum) PURE;               // Enum to be closed.

        //     STDMETHOD(FindAssembliesByName)(        // S_OK or error
        //         LPCWSTR  szAppBase,                 // [IN] optional - can be NULL
        //         LPCWSTR  szPrivateBin,              // [IN] optional - can be NULL
        //         LPCWSTR  szAssemblyName,            // [IN] required - this is the assembly you are requesting
        //         IUnknown *ppIUnk[],                 // [OUT] put IMetaDataAssemblyImport pointers here
        //         ULONG    cMax,                      // [IN] The max number to put
        //         ULONG    *pcAssemblies) PURE;       // [OUT] The number of assemblies returned.
        // };  // IMetaDataAssemblyImport
    }
}