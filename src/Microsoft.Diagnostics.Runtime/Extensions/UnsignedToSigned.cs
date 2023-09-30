// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Extensions
{
    internal static class UnsignedToSigned
    {
        public static int ToSigned(this uint unsigned) => (unsigned <= int.MaxValue) ? (int)unsigned : int.MaxValue;
    }
}