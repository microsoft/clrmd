// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents the version of a DLL.
    /// </summary>
    public readonly struct VersionInfo : IEquatable<VersionInfo>, IComparable<VersionInfo>
    {
        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'A'.
        /// </summary>
        public int Major { get; }

        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'B'.
        /// </summary>
        public int Minor { get; }

        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'C'.
        /// </summary>
        public int Revision { get; }

        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'D'.
        /// </summary>
        public int Patch { get; }

        public VersionInfo(int major, int minor, int revision, int patch)
        {
            if (major < 0)
                throw new ArgumentOutOfRangeException(nameof(major));

            if (minor < 0)
                throw new ArgumentOutOfRangeException(nameof(minor));

            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));

            if (patch < 0)
                throw new ArgumentOutOfRangeException(nameof(patch));

            Major = major;
            Minor = minor;
            Revision = revision;
            Patch = patch;
        }

        internal VersionInfo(int major, int minor, int revision, int patch, bool skipChecks)
        {
            _ = skipChecks;

            Major = major;
            Minor = minor;
            Revision = revision;
            Patch = patch;
        }

        /// <inheritdoc/>
        public bool Equals(VersionInfo other)
        {
            return Major == other.Major && Minor == other.Minor && Revision == other.Revision && Patch == other.Patch;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is VersionInfo other && Equals(other);

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
            return $"{Major}.{Minor}.{Revision}.{Patch}";
        }

        public static bool operator ==(VersionInfo left, VersionInfo right) => left.Equals(right);

        public static bool operator !=(VersionInfo left, VersionInfo right) => !(left == right);

        public static bool operator <(VersionInfo left, VersionInfo right) => left.CompareTo(right) < 0;

        public static bool operator <=(VersionInfo left, VersionInfo right) => left.CompareTo(right) <= 0;

        public static bool operator >(VersionInfo left, VersionInfo right) => right < left;

        public static bool operator >=(VersionInfo left, VersionInfo right) => right <= left;
    }
}