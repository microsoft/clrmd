// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Native.DacInterface
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("d681b4fd-87e1-42ec-af44-4938e62bd266")]
    internal interface ISerializedException
    {
        ulong ExceptionId { get; }
        ulong InnerExceptionId { get; }
        ulong ThreadId { get; }
        ulong NestingLevel { get; }
        ulong ExceptionCCWPtr { get; }
        ulong ExceptionEEType { get; }
        ulong HResult { get; }
        ISerializedStackFrameEnumerator StackFrames { get; }
    }
}