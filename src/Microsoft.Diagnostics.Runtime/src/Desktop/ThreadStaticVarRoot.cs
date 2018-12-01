// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class ThreadStaticVarRoot : ClrRoot
    {
        public ThreadStaticVarRoot(ulong addr, ulong obj, ClrType type, string typeName, string variableName, ClrAppDomain appDomain)
        {
            Address = addr;
            Object = obj;
            Name = string.Format("thread static var {0}.{1}", typeName, variableName);
            AppDomain = appDomain;
            Type = type;
        }

        public override ClrAppDomain AppDomain { get; }
        public override GCRootKind Kind => GCRootKind.ThreadStaticVar;
        public override string Name { get; }
        public override ClrType Type { get; }
    }
}