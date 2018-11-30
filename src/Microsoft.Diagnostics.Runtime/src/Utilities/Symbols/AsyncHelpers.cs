// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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