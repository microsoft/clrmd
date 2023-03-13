// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.Diagnostics.Runtime.Utilities.DbgEng
{
    internal sealed class DbgEngOutputStream : TextWriter
    {
        private readonly IDebugClient _client;
        private readonly IDebugControl _control;
        private readonly nint? _previousCallbacks;
        private readonly TextWriter _previousConsoleOut;

        public DbgEngOutputStream(IDebugClient client, IDebugControl control)
        {
            _client = client;
            _control = control;
            _previousCallbacks = client.GetOutputCallbacks();

            _previousConsoleOut = Console.Out;
            Console.SetOut(this);
        }

        public override void Write(string? value)
        {
            if (value is not null)
                _control.Write(DEBUG_OUTPUT.NORMAL, value);
        }

        public override Encoding Encoding => Encoding.Unicode;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Console.SetOut(_previousConsoleOut);
                if (_previousCallbacks is nint prev)
                    _client.SetOutputCallbacks(prev);
            }
        }

        public override void WriteLine()
        {
            _control.Write(DEBUG_OUTPUT.NORMAL, "\n");
        }
    }
}