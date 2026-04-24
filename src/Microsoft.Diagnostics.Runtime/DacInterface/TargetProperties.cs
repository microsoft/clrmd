// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    /// <summary>
    /// Immutable target-process properties used throughout the DAC implementation.
    /// A single <see cref="TargetProperties"/> instance is owned by <see cref="DacLibrary"/>
    /// and passed to DAC wrappers. Future target-level facts (endianness, machine type, etc.)
    /// can be added here without re-plumbing every DAC wrapper.
    /// </summary>
    internal sealed class TargetProperties
    {
        /// <summary>
        /// Creates target properties for the specified pointer size.
        /// </summary>
        /// <param name="pointerSize">The target process pointer size (4 or 8).</param>
        public TargetProperties(int pointerSize)
        {
            if (pointerSize != 4 && pointerSize != 8)
                throw new ArgumentOutOfRangeException(nameof(pointerSize), pointerSize, "Pointer size must be 4 or 8.");

            PointerSize = pointerSize;
        }

        /// <summary>Pointer size in bytes (4 or 8).</summary>
        public int PointerSize { get; }
    }
}
