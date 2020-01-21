// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Diagnostics.Runtime.Implementation
{
    public interface IFieldHelpers
    {
        ITypeFactory Factory { get; }
        IDataReader DataReader { get; }
        bool ReadProperties(ClrType parentType, int token, out string? name, out FieldAttributes attributes, out Utilities.SigParser sigParser);
        ulong GetStaticFieldAddress(ClrStaticField field, ClrAppDomain? appDomain);
    }
}