// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    /// <summary>
    /// Provides version specific debugging libraries such as mscordbi.dll and mscorwks.dll during
    /// startup in the native pipeline debugging architecture
    /// </summary>
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("3151C08D-4D09-4f9b-8838-2880BF18FE51")]
    public interface ICLRDebuggingLibraryProvider
    {
        /// <summary>
        /// Provides a version specific debugging library
        /// </summary>
        /// <param name="fileName">The name of the library being requested</param>
        /// <param name="timestamp">
        /// The timestamp of the library being requested as specified
        /// in the PE header
        /// </param>
        /// <param name="sizeOfImage">
        /// The SizeOfImage of the library being requested as specified
        /// in the PE header
        /// </param>
        /// <param name="hModule">An OS handle to the requested library</param>
        /// <returns>
        /// HResults.S_OK if the library was located, otherwise any appropriate
        /// error hresult
        /// </returns>
        [PreserveSig]
        int ProvideLibrary(
            [In][MarshalAs(UnmanagedType.LPWStr)] string fileName,
            int timestamp,
            int sizeOfImage,
            out IntPtr hModule);
    }
}