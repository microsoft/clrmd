// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    internal class ContextHelper
    {
        [ThreadStatic]
        private static volatile byte[] _context;
        private static int _ipOffset;
        private static int _spOffset;

        public static uint ContextFlags { get { return 0x1003f; } }
        public static byte[] Context { get { Init(); return _context; } }
        public static int InstructionPointerOffset { get { Init(); return _ipOffset; } }
        public static int StackPointerOffset { get { Init(); return _spOffset; } }
        public static uint Length { get { Init(); return (uint)_context.Length; } }

        static void Init()
        {
            if (_context != null)
                return;
            
            if (IntPtr.Size == 4)
            {
                _ipOffset = 184;
                _spOffset = 196;
                _context = new byte[716];
            }
            else
            {
                _ipOffset = 248;
                _spOffset = 152;
                _context = new byte[1232];
            }
        }
    }
}