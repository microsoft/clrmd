// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    public class DbgEngOutputHolder : IDisposable, IDebugOutputCallbacks
    {
        private readonly IDebugClient _client;
        private readonly nint _previous;
        private bool _disposed;

        public DEBUG_OUTPUT InterestMask { get; set; }

        public Action<string, DEBUG_OUTPUT>? OutputReceived;

        public DbgEngOutputHolder(IDebugClient client, DEBUG_OUTPUT interestMask = DEBUG_OUTPUT.NORMAL)
        {
            _client = client;
            InterestMask = interestMask;

            _previous = _client.GetOutputCallbacks();
            _client.SetOutputCallbacks(this);
        }

        public void Dispose()
        {
            if (!_disposed)
                _client.SetOutputCallbacks(_previous);

            _disposed = true;
        }

        public void OnText(DEBUG_OUTPUT flags, string? text, ulong args)
        {
            if ((InterestMask & flags) != 0 && text is not null)
                OutputReceived?.Invoke(text, flags);
        }
    }
}