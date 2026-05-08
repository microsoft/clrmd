// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.Runtime.StressLogs.Internal
{
    /// <summary>
    /// Per-enumeration state for <see cref="StressLog.EnumerateMessages"/>.
    /// Each call to <c>EnumerateMessages</c> allocates a fresh context, so
    /// two concurrent enumerations of the same <see cref="StressLog"/> do
    /// not share argument scratch buffers, the active iterator, or the
    /// generation counter that invalidates stale <see cref="StressLogMessage"/>
    /// instances.
    /// </summary>
    internal sealed class StressLogEnumerationContext
    {
        private readonly StressLog _log;
        private readonly ulong[] _argScratch = new ulong[StressLogConstants.MaxArgumentCount];
        private readonly ArgumentResolver _argResolver;

        private int _generationCounter;
        private ThreadIterator? _currentIterator;

        public StressLogEnumerationContext(StressLog log, ArgumentResolver argResolver)
        {
            _log = log;
            _argResolver = argResolver;
        }

        public StressLog Log => _log;

        public ThreadIterator? CurrentIterator
        {
            get => _currentIterator;
            set => _currentIterator = value;
        }

        /// <summary>
        /// Bump the generation counter and copy the active iterator's
        /// arguments into our scratch buffer. Returns the new generation,
        /// which the corresponding <see cref="StressLogMessage"/> stamps so
        /// later access through the message can detect that iteration has
        /// since advanced and refuse to read stale buffers.
        /// </summary>
        public int CaptureFromIterator(ThreadIterator iter)
        {
            _currentIterator = iter;
            int gen = ++_generationCounter;
            int n = iter.ArgumentCount;
            for (int i = 0; i < n; i++)
                _argScratch[i] = iter.GetArgument(i);
            return gen;
        }

        public void Clear()
        {
            _currentIterator = null;
            // Do not reset _generationCounter; outstanding StressLogMessage
            // instances rely on the counter never returning to a previously
            // valid value.
        }

        public ulong GetArgument(int generation, int index, int argCount)
        {
            if (generation != _generationCounter || _currentIterator is null) return 0;
            if ((uint)index >= (uint)argCount) return 0;
            return _argScratch[index];
        }

        public void FormatMessage<T>(int generation, ulong formatAddress, int argCount, ref T receiver)
            where T : struct, IStressLogFormatReceiver
        {
            if (generation != _generationCounter || _currentIterator is null) return;
            _log.FormatMessage(_argScratch, _argResolver, formatAddress, argCount, ref receiver);
        }
    }
}
