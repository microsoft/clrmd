// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents information about a single Clr runtime in a process.
    /// </summary>
    [Serializable]
    public class ClrInfo : IComparable
    {
        private readonly DataTarget _dataTarget;

        internal ClrInfo(DataTarget dataTarget, ModuleInfo module, ClrFlavor flavor, Platform platform)
        {
            _dataTarget = dataTarget ?? throw new ArgumentNullException(nameof(dataTarget));
            ModuleInfo = module ?? throw new ArgumentNullException(nameof(module));
            Flavor = flavor;
            Platform = platform;
        }

        /// <summary>
        /// The version number of this runtime.
        /// </summary>
        public VersionInfo Version => ModuleInfo.Version;

        /// <summary>
        /// The type of CLR this module represents.
        /// </summary>
        public ClrFlavor Flavor { get; }

        /// <summary>
        /// Returns module information about the ClrInstance.
        /// </summary>
        public ModuleInfo ModuleInfo { get; }

        /// <summary>
        /// Target platform.
        /// </summary>
        internal Platform Platform { get; }

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>A version string for this Clr runtime.</returns>
        public override string ToString()
        {
            return Version.ToString();
        }

        /// <summary>
        /// IComparable. Sorts the object by version.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>-1 if less, 0 if equal, 1 if greater.</returns>
        public int CompareTo(object obj)
        {
            if (obj == null)
                return 1;

            if (!(obj is ClrInfo))
                throw new InvalidOperationException("Object not ClrInfo.");

            ClrFlavor flv = ((ClrInfo)obj).Flavor;
            if (flv != Flavor)
                return flv.CompareTo(Flavor); // Intentionally reversed.

            VersionInfo rhs = ((ClrInfo)obj).Version;
            if (Version.Major != rhs.Major)
                return Version.Major.CompareTo(rhs.Major);

            if (Version.Minor != rhs.Minor)
                return Version.Minor.CompareTo(rhs.Minor);

            if (Version.Revision != rhs.Revision)
                return Version.Revision.CompareTo(rhs.Revision);

            return Version.Patch.CompareTo(rhs.Patch);
        }

        /// <summary>
        /// Creates a runtime from the given Dac file on disk.
        /// </summary>
        public ClrRuntime CreateRuntime()
        {
            return _dataTarget.CreateRuntime(this);
        }

        /// <summary>
        /// Creates a runtime from a given IXClrDataProcess interface.  Used for debugger plugins.
        /// </summary>
        public ClrRuntime CreateRuntime(object clrDataProcess)
        {
            return _dataTarget.CreateRuntime(this, clrDataProcess);
        }

        /// <summary>
        /// Creates a runtime from the given Dac file on disk.
        /// </summary>
        /// <param name="dacFilename">A full path to the matching mscordacwks for this process.</param>
        /// <param name="ignoreMismatch">Whether or not to ignore mismatches between </param>
        /// <returns></returns>
        public ClrRuntime CreateRuntime(string dacFilename, bool ignoreMismatch = false)
        {
            return _dataTarget.CreateRuntime(this, dacFilename, ignoreMismatch);
        }
    }
}