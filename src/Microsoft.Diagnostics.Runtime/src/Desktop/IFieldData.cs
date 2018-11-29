namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal interface IFieldData
    {
        uint CorElementType { get; }
        uint SigType { get; }
        ulong TypeMethodTable { get; }

        ulong Module { get; }
        uint TypeToken { get; }

        uint FieldToken { get; }
        ulong EnclosingMethodTable { get; }
        uint Offset { get; }
        bool IsThreadLocal { get; }
        bool IsContextLocal { get; }
        bool IsStatic { get; }
        ulong NextField { get; }
    }
}