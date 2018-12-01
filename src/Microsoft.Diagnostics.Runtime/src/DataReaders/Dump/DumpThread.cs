// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// Represents a thread from a minidump file. This is a flyweight object.
    /// </summary>
    internal class DumpThread
    {
        /// <summary>
        /// Constructor for DumpThread
        /// </summary>
        /// <param name="owner">owning DumpReader object</param>
        /// <param name="raw">unmanaged structure in dump describing the thread</param>
        internal DumpThread(DumpReader owner, MINIDUMP_THREAD raw)
        {
            _raw = raw;
            _owner = owner;
        }

        private readonly DumpReader _owner;
        private readonly MINIDUMP_THREAD _raw;

        // Since new DumpThread objects are created on each request, override hash code and equals
        // to provide equality so that we can use them in hashes and collections.
        public override bool Equals(object obj)
        {
            DumpThread other = obj as DumpThread;
            if (other == null) return false;

            return other._owner == _owner && other._raw == _raw;
        }

        public ulong Teb => _raw.Teb;

        // Returns a hash code.
        public override int GetHashCode()
        {
            // Thread Ids are unique random integers within the dump so make a great hash code.
            return ThreadId;
        }

        // Override of ToString
        public override string ToString()
        {
            int id = ThreadId;
            return string.Format(CultureInfo.CurrentUICulture, "Thread {0} (0x{0:x})", id);
        }

        /// <summary>
        /// The native OS Thread Id of this thread.
        /// </summary>
        public int ThreadId => (int)_raw.ThreadId;

        /// <summary>
        /// Get a thread's context using a raw buffer and size
        /// </summary>
        public void GetThreadContext(IntPtr buffer, int sizeBufferBytes)
        {
            _owner.GetThreadContext(_raw.ThreadContext, buffer, sizeBufferBytes);
        }
    }
}