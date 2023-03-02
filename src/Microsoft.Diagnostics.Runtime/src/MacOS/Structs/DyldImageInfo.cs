using System;

namespace Microsoft.Diagnostics.Runtime.MacOS.Structs
{
    internal readonly struct DyldImageInfo
    {
        public UIntPtr ImageLoadAddress { get; }
        public UIntPtr ImageFilePath { get; }
        public UIntPtr ImageFileModDate { get; }
    }
}
