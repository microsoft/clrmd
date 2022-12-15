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
                MHeap cmd = new MHeap(pUnknown);
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
