// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    /// <summary>
    /// Captures dbgeng output by installing an <see cref="IDebugOutputCallbacks"/>
    /// shim and widening the engine's output mask so all relevant severities
    /// reach us. Routes plain-text payloads through <see cref="OutputReceived"/>
    /// and DML-tagged payloads through <see cref="DmlReceived"/>. Most callers
    /// only care about text; DML is opt-in via the <c>channelInterest</c>
    /// constructor argument.
    /// </summary>
    /// <remarks>
    /// IMPORTANT — dbgeng emits BOTH a DML version AND a text version of
    /// every chunk when DML interest is enabled. Subscribing to both
    /// <see cref="OutputReceived"/> and <see cref="DmlReceived"/> will give
    /// you semantically-duplicate content. Pick one or filter.
    /// </remarks>
    public class DbgEngOutputHolder : IDisposable, IDebugOutputCallbacks
    {
        private readonly IDebugClient _client;
        private readonly nint _previous;
        private readonly uint _previousOutputMask;
        private bool _disposed;

        /// <summary>
        /// DEBUG_OUTPUT severity filter applied to text payloads before
        /// invoking <see cref="OutputReceived"/>.
        /// </summary>
        public DEBUG_OUTPUT InterestMask { get; set; }

        /// <summary>Subscribers receive text-channel chunks.</summary>
        public Action<string, DEBUG_OUTPUT>? OutputReceived;

        /// <summary>Subscribers receive DML-channel chunks (tagged markup).</summary>
        public Action<string, DEBUG_OUTCBF>? DmlReceived;

        /// <summary>
        /// Which dbgeng output channels we want delivered. Defaults to TEXT
        /// only. Pass <c>TEXT | DML</c> at construction time if you also want
        /// <see cref="DmlReceived"/> to fire. EXPLICIT_FLUSH intentionally
        /// excluded — dbgeng issues tens of thousands of flush notifications
        /// per command and almost no caller needs them.
        /// </summary>
        public DEBUG_OUTCBI ChannelInterestFlags { get; set; } = DEBUG_OUTCBI.TEXT;

        DEBUG_OUTCBI IDebugOutputCallbacks.OutputInterestFlags => ChannelInterestFlags;

        /// <summary>Public mirror of the channel interest flags.</summary>
        public DEBUG_OUTCBI OutputInterestFlags => ChannelInterestFlags;

        public DbgEngOutputHolder(IDebugClient client,
                                  DEBUG_OUTPUT interestMask = DEBUG_OUTPUT.NORMAL,
                                  DEBUG_OUTCBI channelInterest = DEBUG_OUTCBI.TEXT)
        {
            _client = client;
            InterestMask = interestMask;
            ChannelInterestFlags = channelInterest;

            _previous = _client.GetOutputCallbacks();
            _client.SetOutputCallbacks(this);

            // Widen the engine's output mask to include the EXTRA severity
            // bits we care about (XML for !analyze -xml etc.) on top of
            // dbgeng's default mask. Going fully 0xFFFFFFFF causes dbgeng
            // to send huge volumes of VERBOSE/SYMBOLS/DEBUGGEE chatter we
            // don't want. The default mask is approximately
            //   NORMAL|ERROR|WARNING|PROMPT|PROMPT_REGISTERS|EXTENSION_WARNING.
            // We add XML (0x800) so !analyze -xml's <ANALYSIS> output
            // reaches us when the underlying engine emits it.
            //
            // Discovered via the diagnostic probe at
            // C:\work\tmp\dbgcb-probe (native-debugger sibling repo) —
            // without this widening, DML payloads from many extensions are
            // filtered at the producer side and never reach Output2 at all.
            //
            // The InterestMask field above still filters at OUR end, so
            // callers control what severities OutputReceived sees.
            _previousOutputMask = _client.GetOutputMask();
            _client.SetOutputMask(_previousOutputMask | (uint)DEBUG_OUTPUT.XML);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _client.SetOutputMask(_previousOutputMask);
                _client.SetOutputCallbacks(_previous);
            }

            _disposed = true;
        }

        public void OnText(DEBUG_OUTPUT flags, string? text, ulong args)
        {
            if ((InterestMask & flags) != 0 && text is not null)
                OutputReceived?.Invoke(text, flags);
        }

        /// <summary>
        /// Called for DML payloads when DML is included in
        /// <see cref="ChannelInterestFlags"/>. Default invokes
        /// <see cref="DmlReceived"/>. Subclasses can override.
        /// </summary>
        public void OnDml(DEBUG_OUTCBF flags, string? dml, ulong args)
        {
            if (dml is not null)
                DmlReceived?.Invoke(dml, flags);
        }
    }
}