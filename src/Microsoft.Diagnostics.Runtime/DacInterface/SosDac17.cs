// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
    /// <summary>
    /// This is an undocumented, untested, and unsupported interface.  Do not use directly.
    /// </summary>
    internal sealed unsafe class SosDac17 : CallableCOMWrapper, ISOSDac17
    {
        internal static readonly Guid IID_ISOSDacInterface17 = new("2f4bb585-ed50-479e-bbe0-10a95a5da3bb");

        public SosDac17(IntPtr ptr)
            : base(IID_ISOSDacInterface17, ptr)
        {
        }

        private ref readonly ISOSDacInterface17VTable VTable => ref Unsafe.AsRef<ISOSDacInterface17VTable>(_vtable);

        public bool TryGetStressLogData(out SOSStressLogData data)
        {
            data = default;
            fixed (SOSStressLogData* ptr = &data)
            {
                // S_FALSE means there is no stress log; treat as failure.
                HResult hr = VTable.GetStressLogData(Self, ptr);
                return hr.IsOK;
            }
        }

        public SosStressLogThreadEnum? GetThreadEnumerator()
        {
            HResult hr = VTable.GetStressLogThreadEnumerator(Self, out IntPtr ptr);
            if (!hr.IsOK || ptr == IntPtr.Zero)
                return null;

            try
            {
                return new SosStressLogThreadEnum(ptr);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public SosStressLogMsgEnum? GetMessageEnumerator(ClrDataAddress threadStressLogAddress)
        {
            HResult hr = VTable.GetStressLogMessageEnumerator(Self, threadStressLogAddress.ToInteropAddress(), out IntPtr ptr);
            if (!hr.IsOK || ptr == IntPtr.Zero)
                return null;

            try
            {
                return new SosStressLogMsgEnum(ptr);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public SosMemoryEnum? GetMemoryRangeEnumerator()
        {
            HResult hr = VTable.GetStressLogMemoryRanges(Self, out IntPtr ptr);
            if (!hr.IsOK || ptr == IntPtr.Zero)
                return null;

            try
            {
                return new SosMemoryEnum(ptr);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly unsafe struct ISOSDacInterface17VTable
        {
            public readonly delegate* unmanaged[Stdcall]<IntPtr, SOSStressLogData*, int> GetStressLogData;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, out IntPtr, int> GetStressLogThreadEnumerator;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, ulong /*ClrDataAddress*/, out IntPtr, int> GetStressLogMessageEnumerator;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, out IntPtr, int> GetStressLogMemoryRanges;
        }
    }

    /// <summary>
    /// This is an undocumented, untested, and unsupported interface.  Do not use directly.
    /// </summary>
    internal sealed unsafe class SosStressLogThreadEnum : CallableCOMWrapper
    {
        internal static readonly Guid IID_ISOSStressLogThreadEnum = new("94a2bd3d-ab3d-43bf-81d8-3ae96b8e33cd");

        public SosStressLogThreadEnum(IntPtr pUnk)
            : base(IID_ISOSStressLogThreadEnum, pUnk)
        {
        }

        private ref readonly ISOSStressLogThreadEnumVTable VTable => ref Unsafe.AsRef<ISOSStressLogThreadEnumVTable>(_vtable);

        public void Reset() => VTable.Reset(Self);

        public void Skip(uint count) => VTable.Skip(Self, count);

        public uint GetCount()
        {
            HResult hr = VTable.GetCount(Self, out uint count);
            return hr ? count : 0;
        }

        /// <summary>Fetch up to <paramref name="buffer"/>.Length entries. Returns the count written.</summary>
        public int Next(Span<SOSThreadStressLogData> buffer)
        {
            fixed (SOSThreadStressLogData* ptr = buffer)
            {
                HResult hr = VTable.Next(Self, (uint)buffer.Length, ptr, out uint fetched);
                return hr ? (int)fetched : 0;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly unsafe struct ISOSStressLogThreadEnumVTable
        {
            public readonly delegate* unmanaged[Stdcall]<IntPtr, uint, int> Skip;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int> Reset;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, out uint, int> GetCount;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, uint, SOSThreadStressLogData*, out uint, int> Next;
        }
    }

    /// <summary>
    /// This is an undocumented, untested, and unsupported interface.  Do not use directly.
    /// </summary>
    internal sealed unsafe class SosStressLogMsgEnum : CallableCOMWrapper
    {
        internal static readonly Guid IID_ISOSStressLogMsgEnum = new("437cb033-afe7-4c0f-a4a7-82c891bc049e");

        public SosStressLogMsgEnum(IntPtr pUnk)
            : base(IID_ISOSStressLogMsgEnum, pUnk)
        {
        }

        private ref readonly ISOSStressLogMsgEnumVTable VTable => ref Unsafe.AsRef<ISOSStressLogMsgEnumVTable>(_vtable);

        public void Reset() => VTable.Reset(Self);

        public void Skip(uint count) => VTable.Skip(Self, count);

        public uint GetCount()
        {
            HResult hr = VTable.GetCount(Self, out uint count);
            return hr ? count : 0;
        }

        /// <summary>Fetch up to <paramref name="buffer"/>.Length messages. Returns the count written.</summary>
        public int Next(Span<SOSStressMsgData> buffer)
        {
            fixed (SOSStressMsgData* ptr = buffer)
            {
                HResult hr = VTable.Next(Self, (uint)buffer.Length, ptr, out uint fetched);
                return hr ? (int)fetched : 0;
            }
        }

        /// <summary>
        /// Fetch the arguments of the message at <paramref name="messageIndex"/>
        /// within the most recent <see cref="Next"/> batch. The arguments must
        /// be retrieved before the next call to <see cref="Next"/>, because the
        /// DAC interprets <paramref name="messageIndex"/> relative to that batch.
        /// Returns the count written into <paramref name="args"/>.
        /// </summary>
        public int GetArguments(uint messageIndex, Span<ClrDataAddress> args)
        {
            fixed (ClrDataAddress* ptr = args)
            {
                HResult hr = VTable.GetArguments(Self, messageIndex, (uint)args.Length, ptr, out uint fetched);
                return hr ? (int)fetched : 0;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly unsafe struct ISOSStressLogMsgEnumVTable
        {
            public readonly delegate* unmanaged[Stdcall]<IntPtr, uint, int> Skip;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, int> Reset;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, out uint, int> GetCount;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, uint, SOSStressMsgData*, out uint, int> Next;
            public readonly delegate* unmanaged[Stdcall]<IntPtr, uint, uint, ClrDataAddress*, out uint, int> GetArguments;
        }
    }
}
