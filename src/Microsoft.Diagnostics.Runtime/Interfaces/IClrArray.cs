namespace Microsoft.Diagnostics.Runtime.Interfaces
{
    public interface IClrArray
    {
        ulong Address { get; }
        int Length { get; }
        int Rank { get; }
        ClrType Type { get; }

        bool Equals(ClrArray other);
        bool Equals(ClrObject other);
        bool Equals(object? obj);
        int GetLength(int dimension);
        int GetLowerBound(int dimension);
        ClrObject GetObjectValue(int index);
        ClrObject GetObjectValue(params int[] indices);
        ClrValueType GetStructValue(int index);
        ClrValueType GetStructValue(params int[] indices);
        int GetUpperBound(int dimension);
        T GetValue<T>(int index) where T : unmanaged;
        T GetValue<T>(params int[] indices) where T : unmanaged;
        T[]? ReadValues<T>(int start, int count) where T : unmanaged;
    }
}