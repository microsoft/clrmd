// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.Diagnostics.Runtime.Utilities.Pdb
{
    internal class PdbDebugException : IOException
    {
        internal PdbDebugException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }
    }
}