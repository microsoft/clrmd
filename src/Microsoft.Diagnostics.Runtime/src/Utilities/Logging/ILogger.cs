namespace Microsoft.Diagnostics.Runtime.Utilities
{
    public interface ILogger
    {
        void Log(string category, string format, params object[] parameters);
    }
}