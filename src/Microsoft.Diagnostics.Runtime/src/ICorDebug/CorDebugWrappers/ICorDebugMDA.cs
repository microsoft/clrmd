// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
    [ComImport]
    [Guid("CC726F2F-1DB7-459B-B0EC-05F01D841B42")]
    [InterfaceType(1)]
    public interface ICorDebugMDA
    {
        void GetName([In] uint cchName, [Out] out uint pcchName, [MarshalAs(UnmanagedType.LPArray)] char[] szName);

        void GetDescription([In] uint cchName, [Out] out uint pcchName, [MarshalAs(UnmanagedType.LPArray)] char[] szName);

        void GetXML([In] uint cchName, [Out] out uint pcchName, [MarshalAs(UnmanagedType.LPArray)] char[] szName);

        void GetFlags([Out] out CorDebugMDAFlags pFlags);

        void GetOSThreadId([Out] out uint pOsTid);
    }
}