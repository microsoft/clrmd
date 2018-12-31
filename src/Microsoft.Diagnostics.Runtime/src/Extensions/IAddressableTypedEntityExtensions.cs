// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    public static class IAddressableTypedEntityExtensions
    {
        /// <summary>
        /// Gets the field value from <see cref="IAddressableTypedEntity"/> with respect to field nature (either reference, or value type).
        /// </summary>
        /// <param name="entity">The entity to read field value from.</param>
        /// <param name="fieldName">The name of the field to get value.</param>
        /// <exception cref="ArgumentNullException">if entity has no type.</exception>
        /// <exception cref="ArgumentException">Thrown when field with matching name was not found.</exception>
        /// <returns></returns>
        public static IAddressableTypedEntity GetFieldFrom(this IAddressableTypedEntity entity, string fieldName)
        {
            ClrType entityType = entity?.Type ?? throw new ArgumentNullException(nameof(entity), "No associated type");

            ClrInstanceField field = entityType.GetFieldByName(fieldName) ?? throw new ArgumentException($"Type '{entityType}' does not contain a field named '{fieldName}'");

            return field.IsObjectReference ? (IAddressableTypedEntity)entity.GetObjectField(fieldName) : entity.GetValueClassField(fieldName);
        }    
    }
}
