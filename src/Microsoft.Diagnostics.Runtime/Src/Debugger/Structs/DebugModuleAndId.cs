using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Interop
{
    /// <summary>
    ///    Describes a symbol within a module.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_MODULE_AND_ID
    {
        /// <summary>
        ///    The location in the target's virtual address space of the module's base address.
        /// </summary>
        public UInt64 ModuleBase;

        /// <summary>
        ///    The symbol ID of the symbol within the module.
        /// </summary>
        public UInt64 Id;
    }
}