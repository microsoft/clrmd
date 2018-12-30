// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Represents an addressable entity (class or struct) with assosiated type.
    /// </summary>
    public interface IAddressableTypedEntity: IEquatable<IAddressableTypedEntity>
    {
        ulong Address { get; }

        string HexAddress { get; }

        ClrType Type { get; }

        T GetField<T>(string fieldName) where T : struct;

        string GetStringField(string fieldName);

        ClrValueClass GetValueClassField(string fieldName);

        ClrObject GetObjectField(string fieldName);
    }
}
