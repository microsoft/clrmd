// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Diagnostics.Runtime
{
    internal static class ExceptionExtensions
    {
        public static Exception AddData(this Exception exception, string name, object value)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            exception.Data[name] = value;
            return exception;
        }
    }
}