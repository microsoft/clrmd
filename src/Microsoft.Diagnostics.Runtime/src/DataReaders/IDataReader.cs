// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// An interface for reading data out of the target process.
    /// </summary>
    public interface IDataReader : IDisposable
    {
        /// <summary>
        /// Gets the architecture of the target.
        /// </summary>
        /// <returns>The architecture of the target.</returns>
        Architecture Architecture { get; }

        /// <summary>
        /// Gets the size of a pointer in the target process.
        /// </summary>
        /// <returns>The pointer size of the target process.</returns>
        int PointerSize { get; }

        /// <summary>
        /// The ProcessId of the DataTarget.
        /// </summary>
        uint ProcessId { get; }

        /// <summary>
        /// Returns true if the data target is a minidump which might not contain full heap data.
        /// </summary>
        bool IsMinidump { get; }

        /// <summary>
        /// Enumerates the OS thread ID of all threads in the process.
        /// </summary>
        /// <returns>An enumeration of all threads in the target process.</returns>
        IEnumerable<uint> EnumerateAllThreads();

        /// <summary>
        /// Enumerates modules in the target process.
        /// </summary>
        /// <returns>A list of the modules in the target process.</returns>
        IList<ModuleInfo> EnumerateModules();

        /// <summary>
        /// Gets the version information for a given module (given by the base address of the module).
        /// </summary>
        /// <param name="baseAddress">The base address of the module to look up.</param>
        /// <param name="version">The version info for the given module.</param>
        void GetVersionInfo(ulong baseAddress, out VersionInfo version);

        /// <summary>
        /// Read memory out of the target process.
        /// </summary>
        /// <param name="address">The address of memory to read.</param>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="bytesRead">The number of bytes actually read out of the target process.</param>
        /// <returns>True if any bytes were read at all, false if the read failed (and no bytes were read).</returns>
        bool ReadMemory(ulong address, Span<byte> buffer, out int bytesRead);

        /// <summary>
        /// Gets information about the given memory range.
        /// </summary>
        /// <param name="addr">An arbitrary address in the target process.</param>
        /// <param name="vq">The base address and size of the allocation.</param>
        /// <returns>True if the address was found and vq was filled, false if the address is not valid memory.</returns>
        bool VirtualQuery(ulong addr, out VirtualQueryData vq);

        /// <summary>
        /// Gets the thread context for the given thread.
        /// </summary>
        /// <param name="threadID">The OS thread ID to read the context from.</param>
        /// <param name="contextFlags">The requested context flags, or 0 for default flags.</param>
        /// <param name="context">A span to write the context to.</param>
        bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context);


        /// <summary>
        /// Read a pointer out of the target process.
        /// </summary>
        /// <returns>
        /// The pointer at the give address, or 0 if that pointer doesn't exist in
        /// the data target.
        /// </returns>
        ulong ReadPointerUnsafe(ulong addr);

        /// <summary>
        /// Read an int out of the target process.
        /// </summary>
        /// <returns>
        /// The int at the give address, or 0 if that pointer doesn't exist in
        /// the data target.
        /// </returns>
        uint ReadDwordUnsafe(ulong addr);

        /// <summary>
        /// Informs the data reader that the user has requested all data be flushed.
        /// </summary>
        void ClearCachedData();
    }
}
