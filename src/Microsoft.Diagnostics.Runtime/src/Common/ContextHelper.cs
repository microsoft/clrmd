// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    internal class ContextHelper
    {
        [ThreadStatic]
        private static volatile byte[] _context;
        private static int _ipOffset;
        private static int _spOffset;
        private static uint _contextFlags;

        public static uint ContextFlags => _contextFlags;
        public static byte[] Context
        {
            get
            {
                Init();
                return _context;
            }
        }
        public static int InstructionPointerOffset
        {
            get
            {
                Init();
                return _ipOffset;
            }
        }
        public static int StackPointerOffset
        {
            get
            {
                Init();
                return _spOffset;
            }
        }
        public static uint Length
        {
            get
            {
                Init();
                return (uint)_context.Length;
            }
        }

        private static void Init()
        {
            if (_context != null)
                return;

            if (IntPtr.Size == 4)
            {
                _ipOffset = 184;
                _spOffset = 196;
                _context = new byte[716];
                _contextFlags = 0x1003f;
            }
            else
            {
                _ipOffset = 248;
                _spOffset = 152;
                _context = new byte[1232];
                _contextFlags = 0x10003f;
            }
        }
    }
}