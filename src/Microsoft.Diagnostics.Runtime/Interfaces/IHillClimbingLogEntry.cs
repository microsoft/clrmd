// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Interfaces
{
    public interface IHillClimbingLogEntry
    {
        /// <summary>
        /// The tick count of this entry.
        /// </summary>
        int TickCount { get; }

        /// <summary>
        /// The new state.
        /// </summary>
        HillClimbingTransition StateOrTransition { get; }

        /// <summary>
        /// The new control setting.
        /// </summary>
        int NewControlSetting { get; }

        /// <summary>
        /// The last history count.
        /// </summary>
        int LastHistoryCount { get; }

        /// <summary>
        /// The last history mean.
        /// </summary>
        float LastHistoryMean { get; }
    }
}