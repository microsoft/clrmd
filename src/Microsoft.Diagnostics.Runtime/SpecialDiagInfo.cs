// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct SpecialDiagInfo
    {
        private static readonly byte[] SPECIAL_DIAGINFO_SIGNATURE = Encoding.ASCII.GetBytes("DIAGINFOHEADER");
        private const int SPECIAL_DIAGINFO_RUNTIME_BASEADDRESS = 2;
        private const int SPECIAL_DIAGINFO_LATEST = 2;

        private const ulong SpecialDiagInfoAddressMacOS64 = 0x7fffffff10000000;
        private const ulong SpecialDiagInfoAddress64 = 0x00007ffffff10000;
        private const ulong SpecialDiagInfoAddress32 = 0x7fff1000;

        private const int SignatureSize = 16;

        private fixed byte _signature[SignatureSize];
        private int _version;
        private ulong _exceptionRecordAddress;
        private ulong _runtimeBaseAddress;

        public static unsafe bool TryReadSpecialDiagInfo(IDataReader reader, out SpecialDiagInfo info)
        {
            ulong address = reader.TargetPlatform == OSPlatform.OSX ? SpecialDiagInfoAddressMacOS64 : (reader.PointerSize == 4 ? SpecialDiagInfoAddress32 : SpecialDiagInfoAddress64);
            int size = sizeof(SpecialDiagInfo);

            fixed (SpecialDiagInfo* ptr = &info)
            {
                return reader.Read(address, new Span<byte>(ptr, size)) != size || !info.IsValid;
            }
        }

        public bool IsValid
        {
            get
            {
                fixed (void* ptr = _signature)
                {
                    ReadOnlySpan<byte> signature = new(ptr, SPECIAL_DIAGINFO_SIGNATURE.Length);
                    return _version > 0 && signature.SequenceEqual(SPECIAL_DIAGINFO_SIGNATURE);
                }
            }
        }

        public ulong ExceptionRecordAddress => _exceptionRecordAddress;

        public ulong RuntimeBaseAddress => _version >= SPECIAL_DIAGINFO_RUNTIME_BASEADDRESS ? _runtimeBaseAddress : 0; 
    }
}