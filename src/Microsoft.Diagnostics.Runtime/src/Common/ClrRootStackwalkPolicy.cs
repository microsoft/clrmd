// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// This sets the policy for how ClrHeap walks the stack when enumerating roots.  There is a choice here because the 'Exact' stack walking
    /// gives the correct answer (without overreporting), but unfortunately is poorly implemented in CLR's debugging layer.
    /// This means it could take 10-30 minutes (!) to enumerate roots on crash dumps with 4000+ threads.
    /// </summary>
    public enum ClrRootStackwalkPolicy
    {
        /// <summary>
        /// The GCRoot class will attempt to select a policy for you based on the number of threads in the process.
        /// </summary>
        Automatic,

        /// <summary>
        /// Use real stack roots.  This is much slower than 'Fast', but provides no false-positives for more accurate
        /// results.  Note that this can actually take 10s of minutes to enumerate the roots themselves in the worst
        /// case scenario.
        /// </summary>
        Exact,

        /// <summary>
        /// Walks each pointer aligned address on all stacks and if it points to an object it treats that location
        /// as a real root.  This can over-report roots when a value is left on the stack, but the GC does not
        /// consider it a real root.
        /// </summary>
        Fast,

        /// <summary>
        /// Do not walk stack roots.
        /// </summary>
        SkipStack
    }
}