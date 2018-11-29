using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    internal static class AsyncHelpers
    {
        public static async Task<T> GetFirstNonNullResult<T>(this List<Task<T>> tasks)
            where T : class
        {
            while (tasks.Count > 0)
            {
                Task<T> task = await Task.WhenAny(tasks);

                T result = task.Result;
                if (result != null)
                    return result;

                if (tasks.Count == 1)
                    break;

                tasks.Remove(task);
            }

            return null;
        }
    }
}