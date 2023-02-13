using System;

namespace Microsoft.Diagnostics.Runtime.MacOS.Structs
{
    readonly struct DyldImageInfo
    {
        public UIntPtr ImageLoadAddress { get; }
        public UIntPtr ImageFilePath { get; }
        public UIntPtr ImageFileModDate { get; }
    }
}
