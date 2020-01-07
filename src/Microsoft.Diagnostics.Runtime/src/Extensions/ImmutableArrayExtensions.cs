// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class ImmutableArrayExtensions
    {
        internal static ImmutableArray<T> AsImmutableArray<T>(this T[] array)
        {
            Debug.Assert(Unsafe.SizeOf<T[]>() == Unsafe.SizeOf<ImmutableArray<T>>());
            return Unsafe.As<T[], ImmutableArray<T>>(ref array);
        }

        internal static ImmutableArray<T> MoveOrCopyToImmutable<T>(this ImmutableArray<T>.Builder builder)
        {
            return builder.Capacity == builder.Count ? builder.MoveToImmutable() : builder.ToImmutable();
        }
    }
}
