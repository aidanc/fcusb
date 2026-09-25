// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Diagnostics;
using Usb2Xchange.Protocol;
using Usb2Xchange.WinUsb;

namespace Usb2Xchange.AspiShim
{
    internal enum AspiLoaderSequenceState
    {
        Ready,
        Records,
        Complete,
        Failed
    }

    internal sealed class AspiLoaderSequenceGate
    {
        internal const long MaximumCompletionMilliseconds = 10000;
        internal const long MaximumSequenceMilliseconds =
            (PrecisionTwoLoaderRecordManifest.RecordCount + 1) *
            MaximumCompletionMilliseconds;

        private readonly PrecisionTwoLoaderRecordManifest manifest;
        private readonly Func<long> monotonicMilliseconds;
        private AspiLoaderSequenceState state;
        private int nextRecordIndex;
        private bool completionPending;
        private bool pendingTerminal;
        private int pendingLength;
        private bool sequenceStarted;
        private long sequenceStartedMilliseconds;
        private long pendingStartedMilliseconds;
        private bool hasObservedTime;
        private long lastObservedMilliseconds;

        internal AspiLoaderSequenceGate()
            : this(PrecisionTwoLoaderRecordManifest.CreateKnown(),
                GetMonotonicMilliseconds)
        {
        }

        internal AspiLoaderSequenceGate(
            PrecisionTwoLoaderRecordManifest manifest,
            Func<long> monotonicMilliseconds)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException("manifest");
            }
            if (monotonicMilliseconds == null)
            {
                throw new ArgumentNullException("monotonicMilliseconds");
            }
            this.manifest = manifest;
            this.monotonicMilliseconds = monotonicMilliseconds;
            state = AspiLoaderSequenceState.Ready;
        }

        internal AspiLoaderSequenceState State
        {
            get { return state; }
        }

        internal int AcceptedRecordCount
        {
            get { return nextRecordIndex; }
        }

        internal bool CompletionPending
        {
            get { return completionPending; }
        }

        internal int Begin(byte target, byte lun, byte[] cdb, byte[] data,
            bool loaderD8Prerequisite)
        {
            if (state == AspiLoaderSequenceState.Failed)
            {
                throw new InvalidOperationException(
                    "The ASPI loader sequence has already failed.");
            }
            if (state == AspiLoaderSequenceState.Complete)
            {
                return Fail("The ASPI loader sequence is already complete.");
            }
            if (completionPending)
            {
                return Fail("The previous loader request has no completion.");
            }

            try
            {
                long now = ObserveTime();
                if (!sequenceStarted)
                {
                    sequenceStarted = true;
                    sequenceStartedMilliseconds = now;
                }
                ValidateSequenceDeadline(now);
                if (!loaderD8Prerequisite || target != 5 || lun != 0)
                {
                    throw new InvalidOperationException(
                        "Loader data-out requires target 5/LUN 0, exact loader " +
                        "identity, and a successful loader D8 prerequisite.");
                }

                int recordIndex;
                if (nextRecordIndex <
                    PrecisionTwoLoaderRecordManifest.RecordCount)
                {
                    recordIndex = nextRecordIndex;
                    manifest.ValidateRecord(recordIndex, cdb, data);
                    pendingTerminal = false;
                    pendingLength =
                        PrecisionTwoLoaderRecordManifest.RecordLength;
                }
                else
                {
                    manifest.ValidateTerminal(cdb, data);
                    recordIndex = -1;
                    pendingTerminal = true;
                    pendingLength =
                        PrecisionTwoLoaderRecordManifest.TerminalLength;
                }

                // Consume the attempt before the transport is called. A USB
                // exception, timeout, or malformed CSW must never be retried.
                completionPending = true;
                pendingStartedMilliseconds = now;
                state = AspiLoaderSequenceState.Records;
                return recordIndex;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void Complete(byte adapterStatus, uint residue,
            int actualLength)
        {
            if (state == AspiLoaderSequenceState.Failed)
            {
                throw new InvalidOperationException(
                    "The ASPI loader sequence has already failed.");
            }
            if (!completionPending)
            {
                Fail("No ASPI loader request is awaiting completion.");
            }

            try
            {
                long now = ObserveTime();
                ValidateSequenceDeadline(now);
                if (now - pendingStartedMilliseconds >
                        MaximumCompletionMilliseconds ||
                    adapterStatus != (byte)AdapterStatus.Success ||
                    residue != 0 || actualLength != pendingLength)
                {
                    throw new InvalidOperationException(
                        "Loader completion was not exact, successful, full " +
                        "length, and within its deadline.");
                }

                completionPending = false;
                if (pendingTerminal)
                {
                    state = AspiLoaderSequenceState.Complete;
                }
                else
                {
                    ++nextRecordIndex;
                    state = AspiLoaderSequenceState.Records;
                }
                pendingTerminal = false;
                pendingLength = 0;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void FailClosed()
        {
            SetFailed();
        }

        private long ObserveTime()
        {
            long now = monotonicMilliseconds();
            if (now < 0 ||
                (hasObservedTime && now < lastObservedMilliseconds))
            {
                throw new InvalidOperationException(
                    "The ASPI loader sequence clock is not monotonic.");
            }
            hasObservedTime = true;
            lastObservedMilliseconds = now;
            return now;
        }

        private void ValidateSequenceDeadline(long now)
        {
            if (sequenceStarted && now - sequenceStartedMilliseconds >
                    MaximumSequenceMilliseconds)
            {
                throw new InvalidOperationException(
                    "The ASPI loader sequence deadline expired.");
            }
        }

        private int Fail(string message)
        {
            SetFailed();
            throw new InvalidOperationException(message);
        }

        private void SetFailed()
        {
            completionPending = false;
            pendingTerminal = false;
            pendingLength = 0;
            state = AspiLoaderSequenceState.Failed;
        }

        private static long GetMonotonicMilliseconds()
        {
            long timestamp = Stopwatch.GetTimestamp();
            long wholeSeconds = timestamp / Stopwatch.Frequency;
            long remainder = timestamp % Stopwatch.Frequency;
            return checked((wholeSeconds * 1000) +
                ((remainder * 1000) / Stopwatch.Frequency));
        }
    }
}
