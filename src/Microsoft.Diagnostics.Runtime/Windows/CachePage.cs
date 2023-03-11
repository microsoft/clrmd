// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// TODO:  This code wasn't written to consider nullable.
#nullable disable

namespace Microsoft.Diagnostics.Runtime.Windows
{
    internal sealed class CachePage<T>
    {
        internal CachePage(T data, ulong dataExtent)
        {
            Data = data;
            DataExtent = dataExtent;
        }

        public T Data { get; }

        public ulong DataExtent { get; }
    }
}