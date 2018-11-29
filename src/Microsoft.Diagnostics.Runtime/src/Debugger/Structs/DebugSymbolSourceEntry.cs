using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DEBUG_SYMBOL_SOURCE_ENTRY
    {
        private UInt64 _moduleBase;
        private UInt64 _offset;
        private UInt64 _fileNameId;
        private UInt64 _engineInternal;
        private UInt32 _size;
        private UInt32 _flags;
        private UInt32 _fileNameSize;
        // Line numbers are one-based.
        // May be DEBUG_ANY_ID if unknown.
        private UInt32 _startLine;
        private UInt32 _endLine;
        // Column numbers are one-based byte indices.
        // May be DEBUG_ANY_ID if unknown.
        private UInt32 _startColumn;
        private UInt32 _endColumn;
        private UInt32 _reserved;
    }
}