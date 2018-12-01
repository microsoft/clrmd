// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    /// <summary>
    /// This interface exposes the native pipeline architecture startup APIs
    /// </summary>
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("D28F3C5A-9634-4206-A509-477552EEFB10")]
    public interface ICLRDebugging
    {
        /// <summary>
        /// Detects if a native module represents a CLR and if so provides the debugging interface
        /// and versioning information
        /// </summary>
        /// <param name="moduleBaseAddress">The native base address of a module which might be a CLR</param>
        /// <param name="dataTarget">The process abstraction which can be used for inspection</param>
        /// <param name="libraryProvider">
        /// A callback interface for locating version specific debug libraries
        /// such as mscordbi.dll and mscordacwks.dll
        /// </param>
        /// <param name="maxDebuggerSupportedVersion">
        /// The highest version of the CLR/debugging libraries which
        /// the caller can support
        /// </param>
        /// <param name="process">The CLR's debugging interface or null if no debugger was detected</param>
        /// <param name="version">The version of the CLR detected or null if no CLR was detected</param>
        /// <param name="flags">
        /// Flags which have additional information about the CLR.
        /// <param name="riidProcess">The Guid for the interface requested.</param>
        /// See ClrDebuggingProcessFlags for more details
        /// </param>
        /// <returns>
        /// HResults.S_OK if an appropriate version CLR was detected, otherwise an appropriate
        /// error hresult
        /// </returns>
        [PreserveSig]
        int OpenVirtualProcess(
            [In] ulong moduleBaseAddress,
            [In][MarshalAs(UnmanagedType.IUnknown)]
            object dataTarget,
            [In][MarshalAs(UnmanagedType.Interface)]
            ICLRDebuggingLibraryProvider libraryProvider,
            [In] ref ClrDebuggingVersion maxDebuggerSupportedVersion,
            [In] ref Guid riidProcess,
            [Out][MarshalAs(UnmanagedType.IUnknown)]
            out object process,
            [In][Out] ref ClrDebuggingVersion version,
            [Out] out ClrDebuggingProcessFlags flags);

        /// <summary>
        /// Determines if the module is no longer in use
        /// </summary>
        /// <param name="moduleHandle">A module handle that was provided via the ILibraryProvider</param>
        /// <returns>
        /// HResults.S_OK if the module can be unloaded, HResults.S_FALSE if it is in use
        /// or an appropriate error hresult otherwise
        /// </returns>
        [PreserveSig]
        int CanUnloadNow(IntPtr moduleHandle);
    }
}