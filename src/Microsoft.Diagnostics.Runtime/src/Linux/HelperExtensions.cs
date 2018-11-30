using System.IO;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    internal static class HelperExtensions
    {
        public static string GetFilename(this Stream stream)
        {
            return stream is FileStream fs ? fs.Name : null;
        }
    }
}