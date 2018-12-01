// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Native.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Native
{
    public class NativeThread
    {
        private NativeThreadData _data;

        public bool IsFinalizer { get; }
        public ulong Address { get; }
        public NativeRuntime Runtime { get; }
        public uint OSThreadId => _data.OSThreadId;
        public ulong Teb => _data.Teb;
        internal ref NativeThreadData ThreadData => ref _data;

        public NativeThread(NativeRuntime runtime, ref NativeThreadData data, ulong address, bool finalizer)
        {
            data = _data;
            IsFinalizer = finalizer;
            Address = address;
            Runtime = runtime;
        }
    }
}