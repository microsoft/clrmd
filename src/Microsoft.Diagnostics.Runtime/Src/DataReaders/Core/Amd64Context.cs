using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct M128A
    {
        public ulong Low;
        public ulong High;

        public void Clear()
        {
            Low = 0;
            High = 0;
        }

        public static bool operator ==(M128A lhs, M128A rhs)
        {
            return lhs.Low == rhs.Low && lhs.High == rhs.High;
        }

        public static bool operator !=(M128A lhs, M128A rhs)
        {
            return lhs.Low != rhs.Low || lhs.High != rhs.High;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");

            if (obj.GetType() != typeof(M128A))
                return false;

            return this == (M128A)obj;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct XmmSaveArea
    {
        public const int HeaderSize = 2;
        public const int LegacySize = 8;

        [FieldOffset(0x0)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = HeaderSize)]
        public M128A[] Header;

        [FieldOffset(0x20)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = LegacySize)]
        public M128A[] Legacy;

        [FieldOffset(0xa0)]
        public M128A Xmm0;
        [FieldOffset(0xb0)]
        public M128A Xmm1;
        [FieldOffset(0xc0)]
        public M128A Xmm2;
        [FieldOffset(0xd0)]
        public M128A Xmm3;
        [FieldOffset(0xe0)]
        public M128A Xmm4;
        [FieldOffset(0xf0)]
        public M128A Xmm5;
        [FieldOffset(0x100)]
        public M128A Xmm6;
        [FieldOffset(0x110)]
        public M128A Xmm7;
        [FieldOffset(0x120)]
        public M128A Xmm8;
        [FieldOffset(0x130)]
        public M128A Xmm9;
        [FieldOffset(0x140)]
        public M128A Xmm10;
        [FieldOffset(0x150)]
        public M128A Xmm11;
        [FieldOffset(0x160)]
        public M128A Xmm12;
        [FieldOffset(0x170)]
        public M128A Xmm13;
        [FieldOffset(0x180)]
        public M128A Xmm14;
        [FieldOffset(0x190)]
        public M128A Xmm15;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct VectorRegisterArea
    {
        public const int VectorRegisterSize = 26;

        [FieldOffset(0x0)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = VectorRegisterSize)]
        public M128A[] VectorRegister;

        [FieldOffset(0x1a0)]
        public ulong VectorControl;


        public VectorRegisterArea(VectorRegisterArea other) : this()
        {
            for (int i = 0; i < VectorRegisterSize; ++i)
                VectorRegister[i] = other.VectorRegister[i];

            VectorControl = other.VectorControl;
        }

        public void Clear()
        {
            for (int i = 0; i < VectorRegisterSize; ++i)
                VectorRegister[i].Clear();

            VectorControl = 0;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct AMD64Context
    {
        [FieldOffset(0x0)]
        public ulong P1Home;
        [FieldOffset(0x8)]
        public ulong P2Home;
        [FieldOffset(0x10)]
        public ulong P3Home;
        [FieldOffset(0x18)]
        public ulong P4Home;
        [FieldOffset(0x20)]
        public ulong P5Home;
        [FieldOffset(0x28)]
        public ulong P6Home;

        [FieldOffset(0x30)]
        public int ContextFlags;

        [FieldOffset(0x34)]
        public int MxCsr;

        [FieldOffset(0x38)]
        public short SegCs;
        [FieldOffset(0x3a)]
        public short SegDs;
        [FieldOffset(0x3c)]
        public short SegEs;
        [FieldOffset(0x3e)]
        public short SegFs;
        [FieldOffset(0x40)]
        public short SegGs;
        [FieldOffset(0x42)]
        public short SegSs;
        [FieldOffset(0x44)]
        public int EFlags;

        [FieldOffset(0x48)]
        public ulong Dr0;
        [FieldOffset(0x50)]
        public ulong Dr1;
        [FieldOffset(0x58)]
        public ulong Dr2;
        [FieldOffset(0x60)]
        public ulong Dr3;
        [FieldOffset(0x68)]
        public ulong Dr6;
        [FieldOffset(0x70)]
        public ulong Dr7;

        [FieldOffset(0x78)]
        public ulong Rax;
        [FieldOffset(0x80)]
        public ulong Rcx;
        [FieldOffset(0x88)]
        public ulong Rdx;
        [FieldOffset(0x90)]
        public ulong Rbx;
        [FieldOffset(0x98)]
        public ulong Rsp;
        [FieldOffset(0xa0)]
        public ulong Rbp;
        [FieldOffset(0xa8)]
        public ulong Rsi;
        [FieldOffset(0xb0)]
        public ulong Rdi;
        [FieldOffset(0xb8)]
        public ulong R8;
        [FieldOffset(0xc0)]
        public ulong R9;
        [FieldOffset(0xc8)]
        public ulong R10;
        [FieldOffset(0xd0)]
        public ulong R11;
        [FieldOffset(0xd8)]
        public ulong R12;
        [FieldOffset(0xe0)]
        public ulong R13;
        [FieldOffset(0xe8)]
        public ulong R14;
        [FieldOffset(0xf0)]
        public ulong R15;

        [FieldOffset(0xf8)]
        public ulong Rip;

        //[FieldOffset(0x100)]
        //public XmmSaveArea FltSave;

        //[FieldOffset(0x300)]
        //public VectorRegisterArea VectorRegisters;

        [FieldOffset(0x4a8)]
        public ulong DebugControl;
        [FieldOffset(0x4b0)]
        public ulong LastBranchToRip;
        [FieldOffset(0x4b8)]
        public ulong LastBranchFromRip;
        [FieldOffset(0x4c0)]
        public ulong LastExceptionToRip;
        [FieldOffset(0x4c8)]
        public ulong LastExceptionFromRip;

        public static int Size => Marshal.SizeOf(typeof(AMD64Context));
    }
}
