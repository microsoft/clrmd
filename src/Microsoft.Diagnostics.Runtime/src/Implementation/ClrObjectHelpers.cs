using Microsoft.Diagnostics.Runtime.Builders;
using Microsoft.Diagnostics.Runtime.DacInterface;
using Microsoft.Diagnostics.Runtime.Implementation;
using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime
{
    internal class ClrObjectHelpers : IClrObjectHelpers
    {
        private readonly uint _firstChar = (uint)IntPtr.Size + 4;
        private readonly uint _stringLength = (uint)IntPtr.Size;
        private readonly SOSDac _sos;
        private readonly IClrTypeFactory _typeFactory;

        public ClrHeap Heap { get; }
        public IDataReader DataReader { get; }

        public ClrObjectHelpers(SOSDac sos, IClrTypeFactory typeFactory, ClrHeap heap)
        {
            _sos = sos;
            _typeFactory = typeFactory;
            Heap = heap;
            DataReader = heap.Runtime.DataTarget.DataReader;
        }

        public string? ReadString(ulong address, int maxLength)
        {
            if (address == 0)
                return null;


            int length = DataReader.Read<int>(address + _stringLength);
            length = Math.Min(length, maxLength);
            if (length == 0)
                return string.Empty;

            ulong data = address + _firstChar;
            char[] buffer = ArrayPool<char>.Shared.Rent(length);
            try
            {
                Span<char> charSpan = new Span<char>(buffer).Slice(0, length);
                Span<byte> bytes = MemoryMarshal.AsBytes(charSpan);
                int read = DataReader.Read(data, bytes);
                if (read == 0)
                    return null;

                return new string(buffer, 0, read / sizeof(char));
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        public ComCallableWrapper? CreateCCWForObject(ulong obj)
        {
            CcwBuilder builder = new(_sos, _typeFactory);
            if (!builder.Init(obj))
                return null;

            return new ComCallableWrapper(builder);
        }

        public RuntimeCallableWrapper? CreateRCWForObject(ulong obj)
        {
            RcwHelpers builder = new(_sos, _typeFactory);
            if (!builder.Init(obj))
                return null;

            return new RuntimeCallableWrapper(builder);
        }

        public ImmutableArray<ComInterfaceData> GetRCWInterfaces(ulong address, int interfaceCount)
        {
            COMInterfacePointerData[]? ifs = _sos.GetRCWInterfaces(address, interfaceCount);
            if (ifs is null)
                return ImmutableArray<ComInterfaceData>.Empty;

            return GetComInterfaces(ifs);
        }

        private ImmutableArray<ComInterfaceData> GetComInterfaces(COMInterfacePointerData[]? ifs)
        {
            ImmutableArray<ComInterfaceData>.Builder result = ImmutableArray.CreateBuilder<ComInterfaceData>(ifs.Length);
            result.Count = result.Capacity;

            for (int i = 0; i < ifs.Length; i++)
                result[i] = new ComInterfaceData(_typeFactory.GetOrCreateType(ifs[i].MethodTable, 0), ifs[i].InterfacePointer);

            return result.MoveToImmutable();
        }

        public ClrType? CreateRuntimeType(ClrObject obj)
        {
            if (!obj.IsRuntimeType)
                throw new InvalidOperationException($"Object {obj.Address:x} is of type '{obj.Type?.Name ?? "null"}', expected '{ClrObject.RuntimeTypeName}'.");

            ClrInstanceField? field = obj.Type?.Fields.Where(f => f.Name == "m_handle").FirstOrDefault();
            if (field is null)
                return null;

            ulong mt;
            if (field.ElementType == ClrElementType.NativeInt)
                mt = (ulong)obj.ReadField<IntPtr>("m_handle");
            else
                mt = (ulong)obj.ReadValueTypeField("m_handle").ReadField<IntPtr>("m_ptr");

            return _typeFactory.GetOrCreateType(mt, 0);
        }
    }
}
