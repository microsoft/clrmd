using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A ClrValue represents the value of a field or variable.  This value may be of any type (or null).
    /// </summary>
    public abstract class ClrValue
    {
        internal ClrValue(ClrRuntime runtime, int index)
        {
            Runtime = runtime;
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
        /// A data reader to used by this object.
        /// </summary>
        protected ClrRuntime Runtime { get; set; }
        
        public virtual ClrObject AsObject()
        {
            if (!ClrRuntime.IsObjectReference(ElementType))
                throw new InvalidOperationException("Value is not an object reference.");

            ClrHeap heap = Runtime.GetHeap();

            ulong obj;
            if (!heap.ReadPointer(Address, out obj))
                return new ClrObject(obj, heap.ErrorType);

            return new ClrObject(obj, obj != 0 ? heap.GetObjectType(obj) : heap.ErrorType);
        }


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

        public CorDebugValue(ClrRuntime runtime, ICorDebug.ICorDebugValue value, int index)
            : base(runtime, index)
        {
            _value = value;
        }
    }
}
