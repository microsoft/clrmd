// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class ImmutableArrayExtension
    {
        /// <summary>
        /// <b>WARNING:</b> Typing this method may cause Visual Studio IntelliCode 2.2 to crash.
        /// </summary>
        internal static unsafe Span<T> DangerousGetSpan<T>(this ImmutableArray<T>.Builder builder) where T : unmanaged
        {
            // MemoryMarshal.CreateSpan isn't available on .NET Standard 2.0
            fixed (T* pointer = &builder.ItemRef(0))
            {
                return new Span<T>(pointer, builder.Count);
            }
        }

        internal static ImmutableArray<T> MoveOrCopyToImmutable<T>(this ImmutableArray<T>.Builder builder)
        {
            return builder.Capacity == builder.Count ? builder.MoveToImmutable() : builder.ToImmutable();
        }
    }
}
