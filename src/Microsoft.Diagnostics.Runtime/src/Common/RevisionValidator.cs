// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Runtime
{
    internal static class RevisionValidator
    {
        public static void Validate(int revision, int runtimeRevision)
        {
            if (revision != runtimeRevision)
                throw new ClrDiagnosticsException(
                    $"You must not reuse any object other than ClrRuntime after calling flush!\nClrModule revision ({revision}) != ClrRuntime revision ({runtimeRevision}).",
                    ClrDiagnosticsExceptionKind.RevisionMismatch);
        }
    }
}