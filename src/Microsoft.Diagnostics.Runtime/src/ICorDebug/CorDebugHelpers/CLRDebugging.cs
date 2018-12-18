// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    /// <summary>
    /// Wrapper for the ICLRDebugging shim interface. This interface exposes the native pipeline
    /// architecture startup APIs
    /// </summary>
    internal sealed class CLRDebugging
    {
        private static readonly Guid clsidCLRDebugging = new Guid("BACC578D-FBDD-48a4-969F-02D932B74634");
        private readonly ICLRDebugging _clrDebugging;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks>Creates the underlying interface from mscoree!CLRCreateInstance</remarks>
        public CLRDebugging()
        {
            object o;
            Guid ifaceId = typeof(ICLRDebugging).GetGuid();
            Guid clsid = clsidCLRDebugging;
            NativeMethods.CLRCreateInstance(ref clsid, ref ifaceId, out o);
            _clrDebugging = (ICLRDebugging)o;
        }

        public static ICorDebug GetDebuggerForProcess(int processID, string minimumVersion, DebuggerCallBacks callBacks = null)
        {
            CLRMetaHost mh = new CLRMetaHost();
            CLRRuntimeInfo highestLoadedRuntime = null;
            foreach (CLRRuntimeInfo runtime in mh.EnumerateLoadedRuntimes(processID))
            {
                if (highestLoadedRuntime == null ||
                    string.Compare(highestLoadedRuntime.GetVersionString(), runtime.GetVersionString(), StringComparison.OrdinalIgnoreCase) < 0)
                    highestLoadedRuntime = runtime;
            }

            if (highestLoadedRuntime == null)
                throw new Exception("Could not enumerate .NET runtimes on the system.");

            string runtimeVersion = highestLoadedRuntime.GetVersionString();
            if (string.Compare(runtimeVersion, minimumVersion, StringComparison.OrdinalIgnoreCase) < 0)
                throw new Exception("Runtime in process " + runtimeVersion + " below the minimum of " + minimumVersion);

            ICorDebug rawDebuggingAPI = highestLoadedRuntime.GetLegacyICorDebugInterface();
            if (rawDebuggingAPI == null)
                throw new ArgumentNullException(nameof(rawDebuggingAPI));

            rawDebuggingAPI.Initialize();
            if (callBacks == null)
                callBacks = new DebuggerCallBacks();
            rawDebuggingAPI.SetManagedHandler(callBacks);
            return rawDebuggingAPI;
        }

        public static ICorDebugProcess CreateICorDebugProcess(ulong baseAddress, ICorDebugDataTarget dataTarget, ICLRDebuggingLibraryProvider libraryProvider)
        {
            Debug.Assert(baseAddress != 0);

            Version version;
            ClrDebuggingProcessFlags flags;
            ICorDebugProcess process;
            int errorCode = new CLRDebugging().TryOpenVirtualProcess(
                baseAddress,
                dataTarget,
                libraryProvider,
                new Version(4, 6, 0x7fff, 0x7fff),
                out version,
                out flags,
                out process);
            if (errorCode < 0)
            {
                if (errorCode != -2146231228 && errorCode != -2146231226 && errorCode != -2146231225)
                {
                    Marshal.ThrowExceptionForHR(errorCode);
                }

                process = null;
            }

            return process;
        }

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
        /// <param name="version">The version of the CLR detected or null if no CLR was detected</param>
        /// <param name="flags">
        /// Flags which have additional information about the CLR.
        /// See ClrDebuggingProcessFlags for more details
        /// </param>
        /// <returns>The CLR's debugging interface</returns>
        public ICorDebugProcess OpenVirtualProcess(
            ulong moduleBaseAddress,
            ICorDebugDataTarget dataTarget,
            ICLRDebuggingLibraryProvider libraryProvider,
            Version maxDebuggerSupportedVersion,
            out Version version,
            out ClrDebuggingProcessFlags flags)
        {
            ICorDebugProcess process;
            int hr = TryOpenVirtualProcess(moduleBaseAddress, dataTarget, libraryProvider, maxDebuggerSupportedVersion, out version, out flags, out process);
            if (hr < 0)
                throw new Exception("Failed to OpenVirtualProcess for module at " + moduleBaseAddress + ".  hr = " + hr.ToString("x"));

            return process;
        }

        /// <summary>
        /// Version of the above that doesn't throw exceptions on failure
        /// </summary>
        public int TryOpenVirtualProcess(
            ulong moduleBaseAddress,
            ICorDebugDataTarget dataTarget,
            ICLRDebuggingLibraryProvider libraryProvider,
            Version maxDebuggerSupportedVersion,
            out Version version,
            out ClrDebuggingProcessFlags flags,
            out ICorDebugProcess process)
        {
            ClrDebuggingVersion maxSupport = new ClrDebuggingVersion();
            ClrDebuggingVersion clrVersion = new ClrDebuggingVersion();
            maxSupport.StructVersion = 0;
            maxSupport.Major = (short)maxDebuggerSupportedVersion.Major;
            maxSupport.Minor = (short)maxDebuggerSupportedVersion.Minor;
            maxSupport.Build = (short)maxDebuggerSupportedVersion.Build;
            maxSupport.Revision = (short)maxDebuggerSupportedVersion.Revision;
            object processIface = null;
            clrVersion.StructVersion = 0;
            Guid iid = typeof(ICorDebugProcess).GetGuid();

            int result = _clrDebugging.OpenVirtualProcess(
                moduleBaseAddress,
                dataTarget,
                libraryProvider,
                ref maxSupport,
                ref iid,
                out processIface,
                ref clrVersion,
                out flags);

            // This may be set regardless of success/failure
            version = new Version(clrVersion.Major, clrVersion.Minor, clrVersion.Build, clrVersion.Revision);

            if (result < 0)
            {
                // OpenVirtualProcess failed
                process = null;
                return result;
            }

            // Success
            process = (ICorDebugProcess)processIface;
            return 0;
        }

        /// <summary>
        /// Determines if the module is no longer in use
        /// </summary>
        /// <param name="moduleHandle">A module handle that was provided via the ILibraryProvider</param>
        /// <returns>True if the module can be unloaded, False otherwise</returns>
        public bool CanUnloadNow(IntPtr moduleHandle)
        {
            int ret = _clrDebugging.CanUnloadNow(moduleHandle);
            if (ret == 0) // S_OK
                return true;
            if (ret == 1) // S_FALSE
                return false;

            Marshal.ThrowExceptionForHR(ret);

            //unreachable
            throw new Exception();
        }
    }
}