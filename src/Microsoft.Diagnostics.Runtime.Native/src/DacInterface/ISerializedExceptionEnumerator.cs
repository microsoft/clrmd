// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("d50b1d22-dc01-4d68-b71d-761f9d49f980")]
    internal interface ISerializedExceptionEnumerator
    {
        bool HasNext();
        ISerializedException Next();
    }
}