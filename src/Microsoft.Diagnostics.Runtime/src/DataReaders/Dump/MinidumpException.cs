// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// The struct that holds an EXCEPTION_RECORD
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal class MINIDUMP_EXCEPTION
    {
        public uint ExceptionCode;
        public uint ExceptionFlags;
        public ulong ExceptionRecord;

        private ulong _exceptionaddress;
        public ulong ExceptionAddress
        {
            get => DumpNative.ZeroExtendAddress(_exceptionaddress);
            set => _exceptionaddress = value;
        }

        public uint NumberParameters;
        public uint __unusedAlignment;
        public ulong[] ExceptionInformation;

        public MINIDUMP_EXCEPTION()
        {
            ExceptionInformation = new ulong[DumpNative.EXCEPTION_MAXIMUM_PARAMETERS];
        }
    }
}