// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// Describes system information about the system the dump was taken on.
    /// This is returned by the MINIDUMP_STREAM_TYPE.SystemInfoStream stream.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal class MINIDUMP_SYSTEM_INFO
    {
        // There are existing managed types that represent some of these fields.
        // Provide both the raw imports, and the managed wrappers to make these easier to
        // consume from managed code.

        // These 3 fields are the same as in the SYSTEM_INFO structure from GetSystemInfo().
        // As of .NET 2.0, there is no existing managed object that represents these.
        public ProcessorArchitecture ProcessorArchitecture;
        public ushort ProcessorLevel; // only used for display purposes
        public ushort ProcessorRevision;

        public byte NumberOfProcessors;
        public byte ProductType;

        // These next 4 fields plus CSDVersionRva are the same as the OSVERSIONINFO structure from GetVersionEx().
        // This can be represented as a System.Version.
        public uint MajorVersion;
        public uint MinorVersion;
        public uint BuildNumber;

        // This enum is the same value as System.PlatformId.
        public int PlatformId;

        // RVA to a CSDVersion string in the string table.
        // This would be a string like "Service Pack 1".
        public RVA CSDVersionRva;

        // Remaining fields are not imported.

        //
        // Helper methods
        //

        public Version Version
        {
            // System.Version is a managed abstraction on top of version numbers.
            get
            {
                Version v = new Version((int)MajorVersion, (int)MinorVersion, (int)BuildNumber);
                return v;
            }
        }
    }
}