// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Extensions
{
    public static class IAddressableTypedEntityExtensions
    {
        public static IAddressableTypedEntity GetFieldFrom(this IAddressableTypedEntity entity, string fieldName)
        {
            var entityType = entity?.Type;
            if (entityType == null)
            {
                throw new ArgumentNullException("No assosiated type", nameof(entity));
            }

            var field = entityType.GetFieldByName(fieldName);

            if (field == null)
            {
                throw new ArgumentException($"Type '{entityType}' does not contain a field named '{fieldName}'");
            }

            return field.IsObjectReference ? (IAddressableTypedEntity)entity.GetObjectField(fieldName) : entity.GetValueClassField(fieldName);
        }

    }
}
