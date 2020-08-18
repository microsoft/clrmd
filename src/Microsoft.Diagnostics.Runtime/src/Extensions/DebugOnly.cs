// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class DebugOnly
    {
        [Conditional("DEBUG")]
        public static void Assert(bool mustBeTrue) => Assert(mustBeTrue, "Assertion Failed");

        [Conditional("DEBUG")]
        public static void Assert(bool mustBeTrue, string msg)
        {
            if (!mustBeTrue)
                Fail(msg);
        }

        [Conditional("DEBUG")]
        public static void Fail(string message)
        {
            throw new AssertionException(message);
        }
    }

#pragma warning disable CA1032 // Implement standard exception constructors
#pragma warning disable CA1064 // Exceptions should be public
    internal sealed class AssertionException : Exception
    {
        public AssertionException(string msg)
            : base(msg)
        {
        }
    }
}
