// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Runtime.Implementation;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// A wrapper for exception objects which help with common tasks for exception objects.
    /// Create this using <see cref="ClrObject.AsException"/>. You may call that when <see cref="ClrObject.IsException"/>
    /// is <see langword="true"/>.
    /// </summary>
    public class ClrException
    {
        private readonly IClrTypeHelpers _helpers;
        private readonly ClrObject _object;

        /// <summary>
        /// Gets the original thread this exception was thrown from.  This may be <see langword="null"/> if we do not know.
        /// </summary>
        public ClrThread? Thread { get; internal set; }

        internal ClrException(IClrTypeHelpers helpers, ClrThread? thread, ClrObject obj)
        {
            if (obj.IsNull)
                throw new InvalidOperationException($"Cannot construct a ClrException from a null object.");

            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
            _object = obj;
            Thread = thread;

            DebugOnly.Assert(obj.IsException);
        }

        /// <summary>
        /// Returns this exception's ClrObject representation.
        /// </summary>
        public ClrObject AsObject() => _object;

        /// <summary>
        /// Gets the address of the exception object.
        /// </summary>
        public ulong Address => _object;

        /// <summary>
        /// Gets the <see cref="ClrType"/> for this exception object.
        /// </summary>
        public ClrType? Type => _object.Type;

        /// <summary>
        /// Gets the exception message.
        /// </summary>
        public string? Message
        {
            get
            {
                uint offset = GetMessageOffset(Type);
                DebugOnly.Assert(offset != uint.MaxValue);
                if (offset == 0)
                    return null;

                ulong address = _helpers.DataReader.ReadPointer(Address + offset);
                if (address == 0)
                    return null;

                ClrObject obj = Type.Heap.GetObject(address);
                if (obj.IsValid)
                    return obj.AsString();

                return null;
            }
        }

        /// <summary>
        /// Gets the inner exception, if one exists, <see langword="null"/> otherwise.
        /// </summary>
        public ClrException? Inner
        {
            get
            {
                uint offset = GetInnerExceptionOffset(_object.Type);
                DebugOnly.Assert(offset != uint.MaxValue);

                if (offset == 0)
                    return null;

                ulong address = _helpers.DataReader.ReadPointer(Address + offset);
                ClrObject obj = Type.Heap.GetObject(address);
                if (obj.IsNull)
                    return null;

                return obj.AsException();
            }
        }

        /// <summary>
        /// Gets the HRESULT associated with this exception (or S_OK if there isn't one).
        /// </summary>
        public int HResult
        {
            get
            {
                uint offset = GetHResultOffset(Type);
                DebugOnly.Assert(offset != uint.MaxValue);

                if (offset == 0)
                    return 0;

                DebugOnly.Assert(offset != uint.MaxValue);
                return _helpers.DataReader.Read<int>(Address + offset);
            }
        }

        /// <summary>
        /// Gets the StackTrace for this exception.  Note that this may be empty or partial depending
        /// on the state of the exception in the process.  (It may have never been thrown or we may be in
        /// the middle of constructing the stackwalk.)  This returns an empty list if no stack trace is
        /// associated with this exception object.
        /// </summary>
        public ImmutableArray<ClrStackFrame> StackTrace => GetExceptionStackTrace(Thread, _object);


        public override string ToString()
        {
            return $"Type: {Type?.Name}\nMessage: {Message}\nStack Trace:\n    " + string.Join("    \n", StackTrace.Select(f => f.ToString()));
        }

        private uint GetStackTraceOffset(ClrType? type)
        {
            ClrField? field = type?.Fields.FirstOrDefault(f => f.Name == "_stackTrace");

            if (field != null && field.Offset >= 0)
                return (uint)(field.Offset + IntPtr.Size);

            uint result = _helpers.Heap.Runtime.ClrInfo.Flavor switch
            {
                ClrFlavor.Core => _helpers.DataReader.PointerSize switch
                {
                    4 => 0x14,
                    8 => 0x28,
                    _ => uint.MaxValue
                },

                ClrFlavor.Desktop => _helpers.DataReader.PointerSize switch
                {
                    4 => 0x1c,
                    8 => 0x38,
                    _ => uint.MaxValue
                },

                _ => uint.MaxValue
            };

            return result == uint.MaxValue ? 0 : result + (uint)IntPtr.Size;
        }

        uint GetInnerExceptionOffset(ClrType? type)
        {
            ClrField? field = type?.Fields.FirstOrDefault(f => f.Name == "_innerException");

            if (field != null && field.Offset >= 0)
                return (uint)(field.Offset + IntPtr.Size);

            uint result = _helpers.Heap.Runtime.ClrInfo.Flavor switch
            {
                ClrFlavor.Core => _helpers.DataReader.PointerSize switch
                {
                    4 => 0xc,
                    8 => 0x18,
                    _ => uint.MaxValue
                },

                ClrFlavor.Desktop => _helpers.DataReader.PointerSize switch
                {
                    4 => 0x14,
                    8 => 0x28,
                    _ => uint.MaxValue
                },

                _ => uint.MaxValue
            };

            return result == uint.MaxValue ? 0 : result + (uint)IntPtr.Size;
        }

        uint GetHResultOffset(ClrType? type)
        {
            ClrField? field = type?.Fields.FirstOrDefault(f => f.Name == "_HResult");

            if (field != null && field.Offset >= 0)
                return (uint)(field.Offset + IntPtr.Size);

            uint result = _helpers.Heap.Runtime.ClrInfo.Flavor switch
            {
                ClrFlavor.Core => _helpers.DataReader.PointerSize switch
                {
                    4 => 0x38,
                    8 => 0x6c,
                    _ => uint.MaxValue
                },

                ClrFlavor.Desktop => _helpers.DataReader.PointerSize switch
                {
                    4 => 0x3c,
                    8 => 0x84,
                    _ => uint.MaxValue
                },

                _ => uint.MaxValue
            };

            return result == uint.MaxValue ? 0 : result + (uint)IntPtr.Size;
        }

        uint GetMessageOffset(ClrType? type)
        {
            ClrField? field = type?.Fields.FirstOrDefault(f => f.Name == "_message");

            if (field != null && field.Offset >= 0)
                return (uint)(field.Offset + IntPtr.Size);

            uint result = _helpers.Heap.Runtime.ClrInfo.Flavor switch
            {
                ClrFlavor.Core => _helpers.DataReader.PointerSize switch
                {
                    4 => 4,
                    8 => 8,
                    _ => uint.MaxValue
                },

                ClrFlavor.Desktop => _helpers.DataReader.PointerSize switch
                {
                    4 => 0xc,
                    8 => 0x18,
                    _ => uint.MaxValue
                },

                _ => uint.MaxValue
            };

            return result == uint.MaxValue ? 0 : result + (uint)IntPtr.Size;
        }

        ImmutableArray<ClrStackFrame> GetExceptionStackTrace(ClrThread? thread, ClrObject obj)
        {
            uint offset = GetStackTraceOffset(obj.Type);
            DebugOnly.Assert(offset != uint.MaxValue);
            if (offset == 0)
                return ImmutableArray<ClrStackFrame>.Empty;

            ulong address = _helpers.DataReader.ReadPointer(obj.Address + offset);
            ClrObject _stackTrace = _helpers.Heap.GetObject(address);

            if (_stackTrace.IsNull)
                return ImmutableArray<ClrStackFrame>.Empty;

            int len = _stackTrace.AsArray().Length;
            if (len == 0)
                return ImmutableArray<ClrStackFrame>.Empty;

            int elementSize = IntPtr.Size * 4;
            ulong dataPtr = _stackTrace + (ulong)(IntPtr.Size * 2);
            if (!_helpers.DataReader.ReadPointer(dataPtr, out ulong count))
                return ImmutableArray<ClrStackFrame>.Empty;

            ImmutableArray<ClrStackFrame>.Builder result = ImmutableArray.CreateBuilder<ClrStackFrame>((int)count);
            result.Count = result.Capacity;

            // Skip size and header
            dataPtr += (ulong)(IntPtr.Size * 2);

            for (int i = 0; i < (int)count; ++i)
            {
                ulong ip = _helpers.DataReader.ReadPointer(dataPtr);
                ulong sp = _helpers.DataReader.ReadPointer(dataPtr + (ulong)IntPtr.Size);
                ulong md = _helpers.DataReader.ReadPointer(dataPtr + (ulong)IntPtr.Size + (ulong)IntPtr.Size);

                ClrMethod? method = _helpers.Heap.Runtime.GetMethodByHandle(md);
                result[i] = new ClrStackFrame(thread, null, ip, sp, ClrStackFrameKind.ManagedMethod, method, frameName: null);
                dataPtr += (ulong)elementSize;
            }

            return result.MoveToImmutable();
        }
    }
}