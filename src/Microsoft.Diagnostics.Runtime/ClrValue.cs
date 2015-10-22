using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A ClrValue represents the value of a field or variable.  This value may be of any type (or null).
    /// </summary>
    public abstract class ClrValue
    {
        private readonly RuntimeBase _runtime;

        internal ClrValue(RuntimeBase runtime, int index)
        {
            _runtime = runtime;
            Index = index;
        }

        /// <summary>
        /// The index of this value.  The meaning of this property is determined by the context in which
        /// you obtained it.  For example, for local variables and method arguments, this is the IL index
        /// for that variable.
        /// </summary>
        public int Index { get; protected set; }

        /// <summary>
        /// Returns the size of the value.
        /// </summary>
        public abstract int Size { get; }

        /// <summary>
        /// Returns the address of the value.
        /// </summary>
        public abstract ulong Address { get; }

        /// <summary>
        /// Returns the element type of this value.
        /// </summary>
        public abstract ClrElementType ElementType { get; }
        
        /// <summary>
        /// The runtime associated with this value.
        /// </summary>
        public virtual ClrRuntime Runtime { get { return _runtime; } }

        #region Converters
        public virtual ClrObject AsObject()
        {
            if (!ClrRuntime.IsObjectReference(ElementType))
                throw new InvalidOperationException("Value is not an object.");

            ClrHeap heap = Runtime.GetHeap();

            ulong obj;
            if (!heap.ReadPointer(Address, out obj))
                throw new MemoryReadException(Address);

            return new ClrObject(obj, obj != 0 ? heap.GetObjectType(obj) : heap.ErrorType);
        }

        public virtual bool AsBoolean()
        {
            if (ElementType != ClrElementType.Boolean && ElementType != ClrElementType.Int8 && ElementType != ClrElementType.UInt8)
                throw new InvalidOperationException("Value is not a boolean.");

            ClrHeap heap = Runtime.GetHeap();

            bool result;
            if (!_runtime.ReadBoolean(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }

        public virtual byte AsByte()
        {
            if (ElementType != ClrElementType.Boolean && ElementType != ClrElementType.Int8 && ElementType != ClrElementType.UInt8)
                throw new InvalidOperationException("Value is not a byte.");

            ClrHeap heap = Runtime.GetHeap();

            byte result;
            if (!_runtime.ReadByte(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }

        public virtual sbyte AsSByte()
        {
            if (ElementType != ClrElementType.Boolean && ElementType != ClrElementType.Int8 && ElementType != ClrElementType.UInt8)
                throw new InvalidOperationException("Value is not a byte.");

            ClrHeap heap = Runtime.GetHeap();

            sbyte result;
            if (!_runtime.ReadByte(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }

        public virtual char AsChar()
        {
            if (ElementType != ClrElementType.Char && ElementType != ClrElementType.Int16 && ElementType != ClrElementType.UInt16)
                throw new InvalidOperationException("Value is not a char.");

            ClrHeap heap = Runtime.GetHeap();

            char result;
            if (!_runtime.ReadChar(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }


        public virtual short AsInt16()
        {
            if (ElementType != ClrElementType.Char && ElementType != ClrElementType.Int16 && ElementType != ClrElementType.UInt16)
                throw new InvalidOperationException("Value is not a short.");

            ClrHeap heap = Runtime.GetHeap();

            short result;
            if (!_runtime.ReadShort(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }


        public virtual ushort AsUInt16()
        {
            if (ElementType != ClrElementType.Char && ElementType != ClrElementType.Int16 && ElementType != ClrElementType.UInt16)
                throw new InvalidOperationException("Value is not a short.");

            ClrHeap heap = Runtime.GetHeap();

            ushort result;
            if (!_runtime.ReadShort(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }

        public virtual int AsInt32()
        {
            if (ElementType != ClrElementType.Int32 && ElementType != ClrElementType.UInt32)
                throw new InvalidOperationException("Value is not an integer.");

            ClrHeap heap = Runtime.GetHeap();

            int result;
            if (!_runtime.ReadDword(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }

        public virtual uint AsUInt32()
        {
            if (ElementType != ClrElementType.Int32 && ElementType != ClrElementType.UInt32)
                throw new InvalidOperationException("Value is not a long.");

            ClrHeap heap = Runtime.GetHeap();

            uint result;
            if (!_runtime.ReadDword(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }

        public virtual ulong AsUInt64()
        {
            if (ElementType != ClrElementType.UInt64 && ElementType != ClrElementType.Int64)
                throw new InvalidOperationException("Value is not a long.");

            ClrHeap heap = Runtime.GetHeap();

            ulong result;
            if (!_runtime.ReadQword(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }
        public virtual long AsInt64()
        {
            if (ElementType != ClrElementType.UInt64 && ElementType != ClrElementType.Int64)
                throw new InvalidOperationException("Value is not a long.");

            ClrHeap heap = Runtime.GetHeap();

            long result;
            if (!_runtime.ReadQword(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }

        public virtual float AsFloat()
        {
            if (ElementType != ClrElementType.Float)
                throw new InvalidOperationException("Value is not a float.");

            ClrHeap heap = Runtime.GetHeap();

            float result;
            if (!_runtime.ReadFloat(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }

        public virtual double AsDouble()
        {
            if (ElementType != ClrElementType.Double)
                throw new InvalidOperationException("Value is not a double.");

            ClrHeap heap = Runtime.GetHeap();

            double result;
            if (!_runtime.ReadFloat(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }

        public virtual string AsString()
        {
            if (ElementType != ClrElementType.String && (!ClrRuntime.IsObjectReference(ElementType) || !_runtime.GetHeap().GetObjectType(Address).IsString))
                throw new InvalidOperationException("Value is not a string.");

            ulong str;
            if (!_runtime.ReadPointer(Address, out str))
                throw new MemoryReadException(Address);

            if (str == 0)
                return null;

            string result;
            if (!_runtime.ReadString(str, out result))
                throw new MemoryReadException(str);

            return result;
        }

        public virtual IntPtr AsIntPtr()
        {
            if (ElementType != ClrElementType.Pointer && ElementType != ClrElementType.FunctionPointer && ElementType != ClrElementType.NativeInt && ElementType != ClrElementType.NativeUInt)
                throw new InvalidOperationException("Value is not a pointer.");

            IntPtr result;
            if (!_runtime.ReadPointer(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }

        public virtual UIntPtr AsUIntPtr()
        {
            if (ElementType != ClrElementType.Pointer && ElementType != ClrElementType.FunctionPointer && ElementType != ClrElementType.NativeInt && ElementType != ClrElementType.NativeUInt)
                throw new InvalidOperationException("Value is not a pointer.");

            UIntPtr result;
            if (!_runtime.ReadPointer(Address, out result))
                throw new MemoryReadException(Address);

            return result;
        }
        #endregion

        public override string ToString()
        {
            if (!ClrRuntime.IsObjectReference(ElementType))
                throw new NotImplementedException();

            return AsObject().Address.ToString("x");
        }

        // TODO: This implementation not finished.
    }

    internal class CorDebugValue : ClrValue
    {
        ICorDebug.ICorDebugValue _value;
        int? _size;
        ulong? _address;
        ClrElementType? _elementType;

        public override int Size
        {
            get
            {
                if (!_size.HasValue)
                {
                    uint size;
                    _value.GetSize(out size);
                    _size = (int)size;
                }

                return _size.Value;
            }
        }

        public override ulong Address
        {
            get
            {
                if (!_address.HasValue)
                {
                    ulong addr;
                    _value.GetAddress(out addr);
                    _address = addr;
                }

                return _address.Value;
            }
        }

        public override ClrElementType ElementType
        {
            get
            {
                if (!_elementType.HasValue)
                {
                    ICorDebug.CorElementType element;
                    _value.GetType(out element);
                    _elementType = (ClrElementType)element;
                }

                return _elementType.Value;
            }
        }

        public CorDebugValue(RuntimeBase runtime, ICorDebug.ICorDebugValue value, int index)
            : base(runtime, index)
        {
            _value = value;
        }
    }
}
