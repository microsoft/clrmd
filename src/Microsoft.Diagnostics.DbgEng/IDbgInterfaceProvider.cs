namespace Microsoft.Diagnostics.DbgEng
{
    internal interface IDbgInterfaceProvider
    {
        nint DebugClient { get; }
        nint DebugControl { get; }
        nint DebugDataSpaces { get; }
    }
}
