// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DbgEngExtension
{
    public static unsafe class Exports
    {
        [UnmanagedCallersOnly(EntryPoint = "mheap", CallConvs = new[] { typeof(CallConvStdcall) })]
        public static int ManagedHeap(nint pUnknown, nint args)
        {
            try
            {
                // MHeap subclasses DbgEngCommand, and all DbgEngCommand are IDisposable.  The convention
                // here is that if you have a "using" statement then we will completely clean up everything
                // related to pUnknown (including ClrMD) when Dispose() is called.  That pattern is
                // implemented here if that's what you intend, but it's better to *not* call Dispose() if
                // you don't need to.  In that case, ClrMD will maintain the caches it builds between
                // commands for FAR better performance.  We will, of course, dispose of pUnknown and
                // ClrMD if (for any reason) you end up passing us a new pUnknown...at which point we
                // really have to tear everything down and build it back up.  This doesn't happen
                // in practice with DbgEng extensions...they just keep giving us the same IDebugClient
                // over and over.

                MHeap cmd = new(pUnknown);
                string? arguments = Marshal.PtrToStringAnsi(args);
                cmd.Run(arguments ?? "");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Failed to run {nameof(ManagedHeap)} command.");
                Console.Error.WriteLine(e);
            }

            return 0;
        }

        [UnmanagedCallersOnly(EntryPoint = MAddress.Command, CallConvs = new[] { typeof(CallConvStdcall) })]
        public static int ManagedAddress(nint pUnknown, nint args)
        {
            try
            {
                MAddress cmd = new(pUnknown);
                string? arguments = Marshal.PtrToStringAnsi(args);
                cmd.Run(arguments ?? "");
            }
            catch (Exception e)
            {
                PrintError(MAddress.Command, e);
            }

            return 0;
        }

        [UnmanagedCallersOnly(EntryPoint = GCToNative.Command, CallConvs = new[] { typeof(CallConvStdcall) })]
        public static int FindGCToNativePointers(nint pUnknown, nint args)
        {
            try
            {
                GCToNative cmd = new(pUnknown);
                string? arguments = Marshal.PtrToStringAnsi(args);
                cmd.Run(arguments ?? "");
            }
            catch (Exception e)
            {
                PrintError(GCToNative.Command, e);
            }

            return 0;
        }


        [UnmanagedCallersOnly(EntryPoint = DbgEngExtension.FindPointersIn.Command, CallConvs = new[] { typeof(CallConvStdcall) })]
        public static int FindPointersIn(nint pUnknown, nint args)
        {
            try
            {
                FindPointersIn cmd = new(pUnknown);
                string? arguments = Marshal.PtrToStringAnsi(args);
                cmd.Run(arguments ?? "");
            }
            catch (Exception e)
            {
                PrintError(DbgEngExtension.FindPointersIn.Command, e);
            }

            return 0;
        }

        [UnmanagedCallersOnly(EntryPoint = DbgEngExtension.Help.Command, CallConvs = new[] { typeof(CallConvStdcall) })]
        public static int Help(nint pUnknown, nint args)
        {
            try
            {
                Help cmd = new(pUnknown);
                string? arguments = Marshal.PtrToStringAnsi(args);
                cmd.Show(arguments ?? "");
            }
            catch (Exception e)
            {
                PrintError(DbgEngExtension.FindPointersIn.Command, e);
            }

            return 0;
        }

        private static void PrintError(string command, Exception ex)
        {
            Console.Error.WriteLine($"An error occurred in !{command}:");
            Console.Error.WriteLine(ex);
        }

        [UnmanagedCallersOnly(EntryPoint = "DebugExtensionInitialize")]
        public static int DebugExtensionInitialize(uint* pVersion, uint* pFlags)
        {
            *pVersion = DEBUG_EXTENSION_VERSION(1, 0);
            *pFlags = 0;
            return 0;
        }

        private static uint DEBUG_EXTENSION_VERSION(uint Major, uint Minor)
        {
            return (((Major) & 0xffff) << 16) | ((Minor) & 0xffff);
        }
    }
}