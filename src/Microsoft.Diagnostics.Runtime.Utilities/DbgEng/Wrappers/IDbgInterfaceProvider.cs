namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    internal interface IDbgInterfaceProvider
    {
        nint DebugClient { get; }
        nint DebugControl { get; }
        nint DebugDataSpaces { get; }
        nint DebugSymbols { get; }
        nint DebugSystemObjects { get; }
        nint DebugAdvanced { get; }
    }
}
