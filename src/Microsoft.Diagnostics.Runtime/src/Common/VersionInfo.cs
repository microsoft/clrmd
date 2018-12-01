// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents the version of a DLL.
    /// </summary>
    [Serializable]
    public struct VersionInfo
    {
        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'A'.
        /// </summary>
        public int Major;

        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'B'.
        /// </summary>
        public int Minor;

        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'C'.
        /// </summary>
        public int Revision;

        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'D'.
        /// </summary>
        public int Patch;

        internal VersionInfo(int major, int minor, int revision, int patch)
        {
            Major = major;
            Minor = minor;
            Revision = revision;
            Patch = patch;
        }

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>The A.B.C.D version prepended with 'v'.</returns>
        public override string ToString()
        {
            return $"v{Major}.{Minor}.{Revision}.{Patch:D2}";
        }
    }
}