// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal class ThreadStaticVarRoot : ClrRoot
    {
        private string _name;
        private ClrAppDomain _domain;
        private ClrType _type;

        public ThreadStaticVarRoot(ulong addr, ulong obj, ClrType type, string typeName, string variableName, ClrAppDomain appDomain)
        {
            Address = addr;
            Object = obj;
            _name = string.Format("thread static var {0}.{1}", typeName, variableName);
            _domain = appDomain;
            _type = type;
        }

        public override ClrAppDomain AppDomain
        {
            get
            {
                return _domain;
            }
        }

        public override GCRootKind Kind
        {
            get { return GCRootKind.ThreadStaticVar; }
        }

        public override string Name
        {
            get
            {
                return _name;
            }
        }

        public override ClrType Type
        {
            get { return _type; }
        }
    }
}
