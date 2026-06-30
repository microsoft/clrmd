// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.DacInterface;

namespace Microsoft.Diagnostics.Runtime.DacImplementation
{
    /// <summary>
    /// <see cref="IAbstractStressLog"/> backed by <c>ISOSDacInterface17</c>. Every
    /// DAC call is serialized on the DAC lock, addresses are translated to target
    /// pointers, and arguments are fetched eagerly per batch (the DAC interprets the
    /// message index passed to <c>GetArguments</c> relative to the most recent
    /// <c>Next</c> batch).
    /// </summary>
    internal sealed class DacStressLog : IAbstractStressLog
    {
        private const int ThreadBatchSize = 32;
        private const int MessageBatchSize = 32;
        private const int InitialArgumentCapacity = 16;

        private readonly SOSDac _sos;
        private readonly ISOSDac17 _sos17;
        private readonly TargetProperties _target;

        public DacStressLog(SOSDac sos, ISOSDac17 sos17, TargetProperties target)
        {
            _sos = sos;
            _sos17 = sos17;
            _target = target;
        }

        public bool GetStressLogData(out StressLogData data)
        {
            lock (_sos.SyncRoot)
            {
                if (_sos17.TryGetStressLogData(out SOSStressLogData raw))
                {
                    data = new StressLogData
                    {
                        LoggedFacilities = raw.LoggedFacilities,
                        Level = raw.Level,
                        MaxSizePerThread = raw.MaxSizePerThread,
                        MaxSizeTotal = raw.MaxSizeTotal,
                        TotalChunks = raw.TotalChunks,
                        TickFrequency = raw.TickFrequency,
                        StartTimestamp = raw.StartTimestamp,
                        StartTime = raw.StartTime,
                    };
                    return true;
                }
            }

            data = default;
            return false;
        }

        public IEnumerable<StressLogThreadInfo> EnumerateThreads()
        {
            List<StressLogThreadInfo> threads = [];
            lock (_sos.SyncRoot)
            {
                using SosStressLogThreadEnum? threadEnum = _sos17.GetThreadEnumerator();
                if (threadEnum is null)
                    return threads;

                SOSThreadStressLogData[] batch = new SOSThreadStressLogData[ThreadBatchSize];
                int got;
                while ((got = threadEnum.Next(batch)) > 0)
                {
                    for (int i = 0; i < got; i++)
                    {
                        threads.Add(new StressLogThreadInfo
                        {
                            ThreadLogAddress = batch[i].ThreadLogAddress.ToAddress(_target),
                            ThreadId = batch[i].ThreadId,
                        });
                    }

                    if (got < batch.Length)
                        break;
                }
            }

            return threads;
        }

        public IEnumerable<StressLogMessageInfo> EnumerateMessages(ulong threadLogAddress, CancellationToken cancellationToken)
        {
            ClrDataAddress threadAddress = ClrDataAddress.FromTargetAddress(threadLogAddress, _target);

            SosStressLogMsgEnum? msgEnum;
            lock (_sos.SyncRoot)
                msgEnum = _sos17.GetMessageEnumerator(threadAddress);

            if (msgEnum is null)
                yield break;

            try
            {
                SOSStressMsgData[] batch = new SOSStressMsgData[MessageBatchSize];
                ClrDataAddress[] argBuffer = new ClrDataAddress[InitialArgumentCapacity];

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Fetch a batch of message headers and their arguments together,
                    // under a single lock, before yielding any of them: the DAC's
                    // GetArguments(messageIndex) is relative to the most recent Next
                    // batch, so the args must be read before the next Next call.
                    StressLogMessageInfo[] decoded;
                    int got;
                    lock (_sos.SyncRoot)
                    {
                        got = msgEnum.Next(batch);
                        if (got <= 0)
                            break;

                        decoded = new StressLogMessageInfo[got];
                        for (int i = 0; i < got; i++)
                        {
                            SOSStressMsgData msg = batch[i];
                            int argCount = (int)msg.ArgumentCount;

                            ulong[] args;
                            if (argCount > 0)
                            {
                                // GetArguments has no start-offset: every call copies
                                // arguments starting at index 0, so the only way to read
                                // them all is a single call with a buffer large enough to
                                // hold them. Grow the reusable buffer to fit this message
                                // so no arguments are ever truncated, regardless of the
                                // runtime's maximum argument count.
                                if (argBuffer.Length < argCount)
                                    argBuffer = new ClrDataAddress[argCount];

                                int fetched = msgEnum.GetArguments((uint)i, argBuffer.AsSpan(0, argCount));
                                if (fetched < 0)
                                    fetched = 0;
                                args = new ulong[fetched];
                                for (int a = 0; a < fetched; a++)
                                    args[a] = argBuffer[a].ToAddress(_target);
                            }
                            else
                            {
                                args = Array.Empty<ulong>();
                            }

                            decoded[i] = new StressLogMessageInfo
                            {
                                Facility = msg.Facility,
                                Timestamp = msg.Timestamp,
                                FormatAddress = msg.FormatString.ToAddress(_target),
                                Arguments = args,
                            };
                        }
                    }

                    foreach (StressLogMessageInfo m in decoded)
                        yield return m;

                    if (got < batch.Length)
                        break;
                }
            }
            finally
            {
                msgEnum.Dispose();
            }
        }

        public IEnumerable<(ulong Start, ulong Size)> EnumerateMemoryRanges()
        {
            List<(ulong Start, ulong Size)> ranges = [];
            lock (_sos.SyncRoot)
            {
                using SosMemoryEnum? memoryEnum = _sos17.GetMemoryRangeEnumerator();
                if (memoryEnum is null)
                    return ranges;

                foreach (SosMemoryRegion region in memoryEnum)
                    ranges.Add((region.Start.ToAddress(_target), region.Length.ToAddress(_target)));
            }

            return ranges;
        }
    }
}
