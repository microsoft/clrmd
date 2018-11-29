using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Linux
{
    static class HelperExtensions
    {
        public static string GetFilename(this Stream stream) => stream is FileStream fs ? fs.Name : null;
    }
}
