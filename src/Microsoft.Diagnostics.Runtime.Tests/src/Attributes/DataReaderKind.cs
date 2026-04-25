// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.Runtime.Tests
{
    /// <summary>
    /// Selects which <see cref="IDataReader"/> implementation a dump-based test
    /// should use. Exists so that heap / root / gcroot tests can run as
    /// <c>[Theory]</c>s against both the default cached/uncached reader and the
    /// opt-in lock-free memory-mapped reader without being duplicated.
    /// </summary>
    public enum DataReaderKind
    {
        /// <summary>
        /// ClrMD's default reader (cached or uncached minidump reader, ELF coredump
        /// reader, or Mach-O coredump reader). Thread-safe.
        /// </summary>
        Standard,

        /// <summary>
        /// The opt-in <see cref="DataTargetOptions.UseSingleThreadedDataReader"/>
        /// reader: single-threaded, lock-free, memory-mapped over the dump file.
        /// </summary>
        LockFreeMmf,
    }

    internal static class DataReaderKindExtensions
    {
        /// <summary>
        /// Returns a <see cref="DataTargetOptions"/> configured for this reader kind.
        /// </summary>
        public static DataTargetOptions ToOptions(this DataReaderKind kind)
            => new() { UseSingleThreadedDataReader = kind == DataReaderKind.LockFreeMmf };
    }
}
