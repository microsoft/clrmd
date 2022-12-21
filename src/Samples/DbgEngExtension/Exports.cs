using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DbgEngExtension
{
    public unsafe static class Exports
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
                Console.Error.WriteLine($"Failed to run {nameof(ManagedAddress)} command.");
                Console.Error.WriteLine(e);
            }

            return 0;
        }

        [UnmanagedCallersOnly(EntryPoint = GCPointsTo.GCPointsToCommand, CallConvs = new[] { typeof(CallConvStdcall) })]
        public static int GCPointersToNative(nint pUnknown, nint args)
        {
            try
            {
                GCPointsTo cmd = new(pUnknown);
                string? arguments = Marshal.PtrToStringAnsi(args);
                cmd.Run(arguments ?? "");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Failed to run {nameof(ManagedAddress)} command.");
                Console.Error.WriteLine(e);
            }

            return 0;
        }


        [UnmanagedCallersOnly(EntryPoint = MemPointsTo.MemPointsToCommand, CallConvs = new[] { typeof(CallConvStdcall) })]
        public static int MemoryRegionPointsTo(nint pUnknown, nint args)
        {
            try
            {
                MemPointsTo cmd = new(pUnknown);
                string? arguments = Marshal.PtrToStringAnsi(args);
                cmd.Run(arguments ?? "");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Failed to run {nameof(ManagedAddress)} command.");
                Console.Error.WriteLine(e);
            }

            return 0;
        }


        [UnmanagedCallersOnly(EntryPoint = "DebugExtensionInitialize")]
        public static int DebugExtensionInitialize(uint *pVersion, uint *pFlags)
        {
            *pVersion = DEBUG_EXTENSION_VERSION(1, 0);
            *pFlags = 0;
            return 0;
        }

        static uint DEBUG_EXTENSION_VERSION(uint Major, uint Minor)
        {
            return (((Major) & 0xffff) << 16) | ((Minor) & 0xffff);
        }
    }
}
