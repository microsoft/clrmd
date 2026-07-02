// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Per-enumeration backing for the value-type <see cref="StressLogMessage"/>.
    /// A <see cref="StressLogMessage"/> is yielded eagerly but resolves its
    /// arguments and format text lazily, so it needs a stable place to read the
    /// "current" message's arguments from plus a way to render. Each call to
    /// <c>EnumerateMessages</c> allocates a fresh context, so concurrent
    /// enumerations of the same <see cref="StressLog"/> do not share the argument
    /// scratch buffer or the generation counter that invalidates stale messages.
    /// </summary>
    internal sealed class StressLogEnumerationContext
    {
        private readonly StressLog _log;
        private readonly ulong[] _argScratch = new ulong[StressLogConstants.MaxArgumentCount];
        private readonly ArgumentResolver _argResolver;

        private int _argCount;
        private int _generationCounter;
        private bool _hasCurrent;

        public StressLogEnumerationContext(StressLog log, ArgumentResolver argResolver)
        {
            _log = log;
            _argResolver = argResolver;
        }

        public StressLog Log => _log;

        /// <summary>
        /// Bump the generation counter and copy the active iterator's arguments into
        /// the scratch buffer. Returns the new generation, which the corresponding
        /// <see cref="StressLogMessage"/> stamps so later access through the message
        /// can detect that iteration has since advanced and refuse to read stale
        /// buffers.
        /// </summary>
        public int CaptureFromIterator(ThreadIterator iter)
        {
            int n = Math.Min(iter.ArgumentCount, _argScratch.Length);
            for (int i = 0; i < n; i++)
                _argScratch[i] = iter.GetArgument(i);
            return SetCurrent(n);
        }

        /// <summary>
        /// Bump the generation counter and copy a pre-resolved argument span (the DAC
        /// contract returns the arguments directly) into the scratch buffer. Returns
        /// the new generation, with the same staleness semantics as
        /// <see cref="CaptureFromIterator"/>.
        /// </summary>
        public int CaptureArgs(ReadOnlySpan<ulong> args)
        {
            int n = Math.Min(args.Length, _argScratch.Length);
            for (int i = 0; i < n; i++)
                _argScratch[i] = args[i];
            return SetCurrent(n);
        }

        private int SetCurrent(int count)
        {
            _argCount = count;
            _hasCurrent = true;
            return ++_generationCounter;
        }

        public void Clear()
        {
            _hasCurrent = false;
            _argCount = 0;
            // Do not reset _generationCounter; outstanding StressLogMessage
            // instances rely on the counter never returning to a previously
            // valid value.
        }

        public ulong GetArgument(int generation, int index, int argCount)
        {
            if (generation != _generationCounter || !_hasCurrent) return 0;
            if ((uint)index >= (uint)Math.Min(argCount, _argCount)) return 0;
            return _argScratch[index];
        }

        public void FormatMessage<T>(int generation, ulong formatAddress, int argCount, ref T receiver)
            where T : struct, IStressLogFormatReceiver
        {
            if (generation != _generationCounter || !_hasCurrent) return;
            int count = Math.Min(argCount, _argCount);
            _log.FormatMessage(_argScratch.AsSpan(0, count), _argResolver, formatAddress, ref receiver);
        }
    }
}
