namespace Microsoft.Diagnostics.Runtime.Interfaces
{
    public interface IClrDelegateTarget
    {
        IClrMethod Method { get; }
        IClrDelegate Parent { get; }
        IClrValue TargetObject { get; }
    }
}