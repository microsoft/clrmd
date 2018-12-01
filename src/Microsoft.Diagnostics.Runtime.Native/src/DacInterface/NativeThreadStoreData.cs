// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    public readonly struct NativeThreadStoreData
    {
        public readonly int ThreadCount;
        public readonly ulong FirstThread;
        public readonly ulong FinalizerThread;
        public readonly ulong GCThread;
    }
}