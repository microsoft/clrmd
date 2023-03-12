// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    internal sealed class DbgEngCom : ComWrappers
    {
        public const string DbgEngDll = "dbgeng.dll";

        protected override unsafe ComInterfaceEntry* ComputeVtables(object obj, CreateComInterfaceFlags flags, out int count)
        {
            // We don't implement any of the DbgEng interfaces on this type.
            count = 0;
            return null;
        }

        protected override object? CreateObject(IntPtr externalComObject, CreateObjectFlags flags)
        {
            if (flags != CreateObjectFlags.UniqueInstance)
                throw new NotSupportedException($"Only 'UniqueInstance' is supported.");

            return new DbgEngWrapper(externalComObject);
        }

        protected override void ReleaseObjects(IEnumerable objects)
        {
            throw new NotImplementedException();
        }
    }
}