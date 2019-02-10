// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A set of helper functions that are consistently implemented across platforms.
    /// </summary>
    public abstract class PlatformFunctions
    {
        internal abstract bool GetFileVersion(string dll, out int major, out int minor, out int revision, out int patch);
        public abstract bool TryGetWow64(IntPtr proc, out bool result);
        public abstract IntPtr LoadLibrary(string lpFileName);
        public abstract bool FreeLibrary(IntPtr module);

        public abstract IntPtr GetProcAddress(IntPtr module, string method);

        public virtual bool IsEqualFileVersion(string file, VersionInfo version)
        {
            if (!GetFileVersion(file, out int major, out int minor, out int revision, out int patch))
                return false;

            return major == version.Major && minor == version.Minor && revision == version.Revision && patch == version.Patch;
        }
    }
}