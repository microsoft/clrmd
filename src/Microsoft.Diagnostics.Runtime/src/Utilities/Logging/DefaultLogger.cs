namespace Microsoft.Diagnostics.Runtime.Utilities
{
    public class DefaultLogger : ILogger
    {
        public static readonly ILogger Instance = new DefaultLogger();
    
        private DefaultLogger()
        {
        }
    
        public void Log(string category, string format, params object[] parameters)
        {
            if (parameters != null && parameters.Length > 0)
                format = string.Format(format, parameters);

            System.Diagnostics.Trace.WriteLine(format, category);
        }
    }
}