// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ElfHeaderCommon
    {
        private const int EI_NIDENT = 16;

        private const byte Magic0 = 0x7f;
        private const byte Magic1 = (byte)'E';
        private const byte Magic2 = (byte)'L';
        private const byte Magic3 = (byte)'F';

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = EI_NIDENT)]
        private readonly byte[] _ident;
        private readonly ElfHeaderType _type;
        private readonly ushort _machine;
        private readonly uint _version;

        public bool IsValid
        {
            get
            {
                if (_ident[0] != Magic0 ||
                    _ident[1] != Magic1 ||
                    _ident[2] != Magic2 ||
                    _ident[3] != Magic3)
                    return false;

                return Data == ElfData.LittleEndian;
            }
        }

        public ElfHeaderType Type => _type;

        public ElfMachine Architecture => (ElfMachine)_machine;

        public ElfClass Class => (ElfClass)_ident[4];

        public ElfData Data => (ElfData)_ident[5];

        public IElfHeader GetHeader(Reader reader, long position)
        {
            if (IsValid)
            {
                switch (Architecture)
                {
                    case ElfMachine.EM_X86_64:
                    case ElfMachine.EM_AARCH64:
                        return reader.Read<ElfHeader64>(position);

                    case ElfMachine.EM_386:
                    case ElfMachine.EM_ARM:
                        return reader.Read<ElfHeader32>(position);
                }
            }
            return null;
        }
    }
}