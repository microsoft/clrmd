using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.MacOS.Structs
{
    internal enum LoadCommandType
    {
        Segment = 1,
        SymTab,
        SymSeg,
        Thread,
        UnixThread,
        LoadFVMLib,
        IDFVMLib,
        Ident,
        FVMFile,
        PrePage,
        DysymTab,
        LoadDylib,
        IdDylib,
        LoadDylinker,
        IdDylinker,
        PreboundDylib,
        Routines,
        SubFramework,
        SubUmbrella,
        SubClient,
        SubLibrary,
        TwoLevelHints,
        PrebindChksum,
        Segment64 = 0x19
    }
}
