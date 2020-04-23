// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface ITypeHelpers
    {
        IDataReader DataReader { get; }
        ITypeFactory Factory { get; }
        IClrObjectHelpers ClrObjectHelpers { get; }

        /// <summary>
        /// Gets the name for a type.
        /// </summary>
        /// <param name="mt">The MethodTable to request the name of.</param>
        /// <param name="name">The name for that type, note that this has already had FixGenerics called on it.</param>
        /// <returns>True if the value should be cached, false if the value should not be cached.  (This is controlled
        /// by the user's string cache settings.</returns>
        bool GetTypeName(ulong mt, out string? name);
        ulong GetLoaderAllocatorHandle(ulong mt);

        // TODO: Should not expose this:
        IObjectData GetObjectData(ulong objRef);
    }
}