// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="ClrObject"/>.
    /// </summary>
    public static class ClrObjectExtensions
    {
        /// <summary>
        /// Navigates through <paramref name="fieldNames"/> starting from <paramref name="clrObject"/>; stops on meeting <see cref="ClrObject.IsNull"/> field value, or field without type.
        /// <para>Example: ClassA.AToB points on ClassB instance; ClassB.BToC points on ClassC instance; ClassC.CField value is to be located.</para>
        /// <para>Request objA.GetChainedFieldValue("AToB","BToC", "CField") to get ClassC.CField value from ClassA instance.</para>
        /// </summary>
        /// <param name="clrObject"></param>
        /// <param name="fieldNames"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static ClrObject GetChainedFieldValue(this ClrObject clrObject, params string[] fieldNames)
        {
            var current = clrObject;
            ClrObject fieldValue = default;
            var inspectedFieldName = string.Empty;
            try
            {
                foreach (var fieldName in fieldNames)
                {
                    inspectedFieldName = fieldName;
                    fieldValue = current.GetObjectField(inspectedFieldName);
                    if (fieldValue.IsNull || fieldValue.Type == null)
                    {
                        return default;
                    }
                    current = fieldValue;
                }
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"{inspectedFieldName} was not found in {current.HexAddress}", exception);
            }

            return fieldValue;
        }

        /// <summary>
        /// Navigates through <paramref name="fieldNames"/> starting from <paramref name="clrObject"/> and mutes all exceptions; stops on meeting <see cref="ClrObject.IsNull"/> field value, or field without type.
        /// <para>Example: ClassA.AToB points on ClassB instance; ClassB.BToC points on ClassC instance; ClassC.CField value is to be located.</para>
        /// <para>Request objA.GetChainedFieldValue("AToB","BToC", "CField") to get ClassC.CField value from ClassA instance.</para>
        /// </summary>
        /// <param name="clrObject"></param>
        /// <param name="fieldNames"></param>
        /// <returns></returns>
        public static ClrObject GetChainedFieldValueWrapped(this ClrObject clrObject, params string[] fieldNames)
        {
            try
            {
                return clrObject.GetChainedFieldValue(fieldNames);
            }
            catch
            {
                return default;
            }
        }

    }
}
