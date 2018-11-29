using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface ICodeHeap
    {
        CodeHeapType Type { get; }
        ulong Address { get; }
    }
}