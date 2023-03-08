using System;

namespace Microsoft.Diagnostics.Runtime.Interfaces
{
    public interface IClrArray
    {
        ulong Address { get; }
        int Length { get; }
        int Rank { get; }
        IClrType Type { get; }

        bool Equals(object? obj);
        int GetLength(int dimension);
        int GetLowerBound(int dimension);
        IClrValue GetObjectValue(int index);
        IClrValue GetObjectValue(params int[] indices);
        IClrValue GetStructValue(int index);
        IClrValue GetStructValue(params int[] indices);
        int GetUpperBound(int dimension);
        T GetValue<T>(int index) where T : unmanaged;
        T GetValue<T>(params int[] indices) where T : unmanaged;
        T[]? ReadValues<T>(int start, int count) where T : unmanaged;
    }
}