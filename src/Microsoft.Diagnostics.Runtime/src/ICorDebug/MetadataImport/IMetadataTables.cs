// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [Guid("D8F579AB-402D-4b8e-82D9-5D63B1065C68")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    // Get the geometry of the tables. This is useful for GetTableInfo, which can tell how
    // many rows a table has, which can then be used for quick enumeration of tokens.
    internal interface IMetadataTables
    {
        //STDMETHOD (GetStringHeapSize) (    
        //    ULONG   *pcbStrings) PURE;          // [OUT] Size of the string heap.
        void GetStringHeapSize(out uint countBytesStrings);

        //STDMETHOD (GetBlobHeapSize) (    
        //    ULONG   *pcbBlobs) PURE;            // [OUT] Size of the Blob heap.
        void GetBlobHeapSize(out uint countBytesBlobs);

        //STDMETHOD (GetGuidHeapSize) (    
        //    ULONG   *pcbGuids) PURE;            // [OUT] Size of the Guid heap.
        void GetGuidHeapSize(out uint countBytesGuids);

        //STDMETHOD (GetUserStringHeapSize) (  
        //    ULONG   *pcbBlobs) PURE;            // [OUT] Size of the User String heap.
        void GetUserStringHeapSize(out uint countByteBlobs);

        //STDMETHOD (GetNumTables) (    
        //    ULONG   *pcTables) PURE;            // [OUT] Count of tables.
        void GetNumTables(out uint countTables);

        //STDMETHOD (GetTableIndex) (   
        //    ULONG   token,                      // [IN] Token for which to get table index.
        //    ULONG   *pixTbl) PURE;              // [OUT] Put table index here.
        void GetTableIndex(uint token, out uint tableIndex);

        //STDMETHOD (GetTableInfo) (    
        //    ULONG   ixTbl,                      // [IN] Which table.
        //    ULONG   *pcbRow,                    // [OUT] Size of a row, bytes.
        //    ULONG   *pcRows,                    // [OUT] Number of rows.
        //    ULONG   *pcCols,                    // [OUT] Number of columns in each row.
        //    ULONG   *piKey,                     // [OUT] Key column, or -1 if none.
        //    const char **ppName) PURE;          // [OUT] Name of the table.
        void GetTableInfo(
            uint tableIndex,
            out uint countByteRows,
            out uint countRows,
            out uint countColumns,
            out uint columnPrimaryKey,
            [Out][MarshalAs(UnmanagedType.LPStr)] out string name);

        // Other methods are not yet imported...
    }
}