using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Diagnostics.Runtime {
    public static class ProcMap
    {
        
        public static IEnumerable<ProcMapEntry> GetEntries(int pid)
        {

            using (var sr = new StreamReader($"/proc/{pid}/maps", Encoding.UTF8, false, 81908))
            {
                do
                {
                    yield return new ProcMapEntry(sr.ReadLine());
                } while (!sr.EndOfStream);
            }
        }
    }
}