using System;

namespace Microsoft.Diagnostics.Runtime.DacInterface
{
#pragma warning disable CS0169
#pragma warning disable CS0649

    internal struct IUnknownVTable
    {
        public IntPtr QueryInterface;
        public IntPtr AddRef;
        public IntPtr Release;
    }
}