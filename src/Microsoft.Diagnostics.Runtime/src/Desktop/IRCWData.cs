namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface IRCWData
    {
        ulong IdentityPointer { get; }
        ulong UnknownPointer { get; }
        ulong ManagedObject { get; }
        ulong JupiterObject { get; }
        ulong VTablePtr { get; }
        ulong CreatorThread { get; }

        int RefCount { get; }
        int InterfaceCount { get; }

        bool IsJupiterObject { get; }
        bool IsDisconnected { get; }
    }
}