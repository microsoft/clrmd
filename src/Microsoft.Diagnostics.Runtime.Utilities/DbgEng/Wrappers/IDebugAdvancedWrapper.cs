// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities.DbgEng.Structs;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{

    [DynamicInterfaceCastableImplementation]
    internal unsafe interface IDebugAdvancedWrapper : IDebugAdvanced
    {
        private const int commentBufferSize = 1048576;
        int IDebugAdvanced.GetThreadContext(Span<byte> buffer)
        {
            GetVTable(this, out nint self, out IDebugAdvancedVtable* vtable);

            fixed (byte* ptr = buffer)
                return vtable->GetThreadContext(self, ptr, buffer.Length);
        }

        int IDebugAdvanced.GetCommentWide(out string? comment)
        {
            GetVTable(this, out nint self, out IDebugAdvancedVtable* vtable);
            comment = null;
            char[] buffer = ArrayPool<char>.Shared.Rent(commentBufferSize / 2);
            try
            {
                fixed (char* ptr = buffer)
                {
                    DEBUG_READ_USER_MINIDUMP_STREAM minidumpStream = default;
                    minidumpStream.StreamType = MINIDUMP_STREAM_TYPE.CommentStreamW;
                    minidumpStream.Flags = 0;
                    minidumpStream.Offset = 0;
                    minidumpStream.Buffer = new IntPtr(ptr);
                    minidumpStream.BufferSize = (uint)(buffer.Length * sizeof(char));
                    minidumpStream.BufferUsed = 0;
                    HResult hr = vtable->Request(self, (uint)DEBUG_REQUEST.READ_USER_MINIDUMP_STREAM, &minidumpStream, sizeof(DEBUG_READ_USER_MINIDUMP_STREAM), null, 0, null);

                    if (hr < 0)
                    {
                        return hr;
                    }
                    else if (minidumpStream.BufferUsed > 1)
                    {
                        comment = new string(ptr, 0, ((int)minidumpStream.BufferUsed - 2) / 2);
                        return hr;
                    }
                    else
                    {
                        comment = string.Empty;
                    }
                    return hr;
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        private static void GetVTable(object ths, out nint self, out IDebugAdvancedVtable* vtable)
        {
            self = ((IDbgInterfaceProvider)ths).DebugAdvanced;
            vtable = *(IDebugAdvancedVtable**)self;
        }
    }
}