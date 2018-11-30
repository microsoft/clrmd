// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    internal struct ModuleEntry
    {
        public ClrModule Module;
        public uint Token;

        public ModuleEntry(ClrModule module, uint token)
        {
            Module = module;
            Token = token;
        }
    }
}