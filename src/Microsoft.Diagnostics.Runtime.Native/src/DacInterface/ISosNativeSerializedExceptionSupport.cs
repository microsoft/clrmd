// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("9141efb3-370c-4a7a-bdf5-7f2f7d6dc2f4")]
    internal interface ISOSNativeSerializedExceptionSupport
    {
        ISerializedExceptionEnumerator GetSerializedExceptions();
    }
}