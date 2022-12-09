namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    internal interface IDbgInterfaceProvider
    {
        nint DebugClient { get; }
        nint DebugControl { get; }
        nint DebugDataSpaces { get; }
    }
}
