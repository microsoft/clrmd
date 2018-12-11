// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Asserts for <see cref="ClrObject"/>.
    /// </summary>
    public static class ClrAssert
    {
        /// <summary>
        /// Assert that <paramref name="clrObject"/> points on something - opposite to <see cref="ClrObject.IsNull"/>.
        /// </summary>
        /// <param name="clrObject"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void NotNull(ClrObject clrObject)
        {
            if (clrObject.IsNull)
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Assert that <paramref name="clrObject"/> has <see cref="ClrObject.Type"/>.
        /// </summary>
        /// <param name="clrObject"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void HasType(ClrObject clrObject)
        {
            if (clrObject.Type is null)
            {
                throw new InvalidOperationException($"{clrObject.HexAddress} has no type");
            }
        }
    }
}
