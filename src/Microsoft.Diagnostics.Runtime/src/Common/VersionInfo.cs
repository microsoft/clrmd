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
    public struct VersionInfo : IEquatable<VersionInfo>, IComparable<VersionInfo>
    {
        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'A'.
        /// </summary>
        public readonly int Major;

        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'B'.
        /// </summary>
        public readonly int Minor;

        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'C'.
        /// </summary>
        public readonly int Revision;

        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'D'.
        /// </summary>
        public readonly int Patch;

        internal VersionInfo(int major, int minor, int revision, int patch)
        {
            Major = major;
            Minor = minor;
            Revision = revision;
            Patch = patch;
        }

        /// <summary>
        /// Equals
        /// </summary>
        public bool Equals(VersionInfo other)
        {
            return Major == other.Major && Minor == other.Minor && Revision == other.Revision && Patch == other.Patch;
        }

        /// <summary>
        /// Equals
        /// </summary>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;

            return obj is VersionInfo other && Equals(other);
        }

        /// <summary>
        /// GetHashCode
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Major;
                hashCode = (hashCode * 397) ^ Minor;
                hashCode = (hashCode * 397) ^ Revision;
                hashCode = (hashCode * 397) ^ Patch;
                return hashCode;
            }
        }

        /// <summary>
        /// CompareTo
        /// </summary>
        /// <returns>-1 if less, 0 if equal, 1 if greater.</returns>
        public int CompareTo(VersionInfo other)
        {
            if (Major != other.Major)
                return Major.CompareTo(other.Major);

            if (Minor != other.Minor)
                return Minor.CompareTo(other.Minor);

            if (Revision != other.Revision)
                return Revision.CompareTo(other.Revision);

            return Patch.CompareTo(other.Patch);
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