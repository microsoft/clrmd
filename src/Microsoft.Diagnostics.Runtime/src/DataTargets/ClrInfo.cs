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
        
        internal ClrInfo(DataTarget dt, ClrFlavor flavor, ModuleInfo module, DacInfo dacInfo, string dacLocation)
        {
            _dataTarget = dt ?? throw new ArgumentNullException(nameof(dt));
            Flavor = flavor;
            DacInfo = dacInfo ?? throw new ArgumentNullException(nameof(dacInfo));
            ModuleInfo = module ?? throw new ArgumentNullException(nameof(module));
            LocalMatchingDac = dacLocation;
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
        /// Returns module information about the Dac needed create a ClrRuntime instance for this runtime.
        /// </summary>
        public DacInfo DacInfo { get; }

        /// <summary>
        /// Returns module information about the ClrInstance.
        /// </summary>
        public ModuleInfo ModuleInfo { get; }

        /// <summary>
        /// Returns the location of the local dac on your machine which matches this version of Clr, or null
        /// if one could not be found.
        /// </summary>
        public string LocalMatchingDac { get; }

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>A version string for this Clr runtime.</returns>
        public override string ToString()
        {
            return Version.ToString();
        }

        /// <summary>
        /// IComparable.  Sorts the object by version.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>-1 if less, 0 if equal, 1 if greater.</returns>
        public int CompareTo(object obj)
        {
            if (ReferenceEquals(this, obj)) return 0;
            if (ReferenceEquals(null, obj)) return 1;
            
            if (!(obj is ClrInfo other))
                throw new InvalidOperationException("Object is not a ClrInfo.");
            
            if (Flavor != other.Flavor)
                return other.Flavor.CompareTo(Flavor); // Intentionally reversed
            
            return Version.CompareTo(other.Version);
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