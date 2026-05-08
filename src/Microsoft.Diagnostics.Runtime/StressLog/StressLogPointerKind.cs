// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.StressLogs
{
    /// <summary>
    /// Kind of pointer represented by a stress log argument.
    /// Distinguishes plain pointers from runtime-typed pointers
    /// that the consumer may want to resolve to method or type names.
    /// </summary>
    public enum StressLogPointerKind
    {
        /// <summary>Plain pointer (printf <c>%p</c>).</summary>
        Plain,

        /// <summary>MethodDesc pointer (printf <c>%pM</c>).</summary>
        MethodDesc,

        /// <summary>MethodTable pointer (printf <c>%pT</c>).</summary>
        MethodTable,

        /// <summary>C++ vtable pointer (printf <c>%pV</c>).</summary>
        VTable,

        /// <summary>Code address used in stack traces (printf <c>%pK</c>).</summary>
        CodePointer,
    }
}
