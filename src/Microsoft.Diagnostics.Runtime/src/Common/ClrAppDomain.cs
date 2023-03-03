// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Implementation;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents an AppDomain in the target runtime.
    /// </summary>
    public sealed class ClrAppDomain
    {
        private readonly IClrAppDomainHelpers _helpers;

        internal ClrAppDomain(ClrRuntime runtime, IClrAppDomainHelpers helpers, ulong address, string? name, int id)
        {
            Runtime = runtime;
            _helpers = helpers;
            Address = address;
            Id = id;
            Name = name;
        }

        /// <summary>
        /// Gets the runtime associated with this ClrAppDomain.
        /// </summary>
        public ClrRuntime Runtime { get; }

        /// <summary>
        /// Gets address of the AppDomain.
        /// </summary>
        public ulong Address { get; }

        /// <summary>
        /// Gets the AppDomain's ID.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the name of the AppDomain, as specified when the domain was created.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Gets a list of modules loaded into this AppDomain.
        /// </summary>
        public ImmutableArray<ClrModule> Modules { get; internal set; }

        /// <summary>
        /// Gets the config file used for the AppDomain.  This may be <see langword="null"/> if there was no config file
        /// loaded, or if the targeted runtime does not support enumerating that data.
        /// </summary>
        public string? ConfigurationFile => _helpers.GetConfigFile(this);

        /// <summary>
        /// Gets the base directory for this AppDomain.  This may return <see langword="null"/> if the targeted runtime does
        /// not support enumerating this information.
        /// </summary>
        public string? ApplicationBase => _helpers.GetApplicationBase(this);

        /// <summary>
        /// Returns the LoaderAllocator for this AppDomain.  This is used to debug some CLR internal state
        /// and isn't generally useful for most developers.  This field is only available when debugging
        /// .Net 8+ runtimes.
        /// </summary>
        public ulong LoaderAllocator => _helpers.GetLoaderAllocator(this);

        /// <summary>
        /// Enumerates the native heaps associated with this AppDomain.  Note that this may also enumerate
        /// the same heaps as other domains if they share the same LoaderAllocator (especially SystemDomain).
        /// </summary>
        /// <returns>An enumerable of native heaps associated with this AppDomain.</returns>
        public IEnumerable<ClrNativeHeapInfo> EnumerateLoaderAllocatorHeaps() => _helpers.GetNativeHeapHelpers().EnumerateLoaderAllocatorNativeHeaps(LoaderAllocator);

        /// <summary>
        /// To string override.
        /// </summary>
        /// <returns>The name of this AppDomain.</returns>
        public override string? ToString() => Name;
    }
}