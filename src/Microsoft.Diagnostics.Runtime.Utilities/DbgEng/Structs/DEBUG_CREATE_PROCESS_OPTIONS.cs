// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_CREATE_PROCESS_OPTIONS
    {
        public DEBUG_CREATE_PROCESS CreateFlags;
        public DEBUG_ECREATE_PROCESS EngCreateFlags;
        public uint VerifierFlags;
        private readonly uint Reserved;
    }
}