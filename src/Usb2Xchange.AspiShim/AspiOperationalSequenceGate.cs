// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Diagnostics;
using Usb2Xchange.Protocol;

namespace Usb2Xchange.AspiShim
{
    internal enum AspiOperationalSequenceState
    {
        AwaitOperationalD8,
        AwaitInitialScannerReady,
        AwaitFaultPixelHeader,
        AwaitPostFaultScannerReady,
        AwaitFaultPixelData,
        AwaitCalibration,
        AwaitD8Offset55OrPostCalibrationScannerReady,
        AwaitPostCalibrationScannerReady,
        PreviewSetWindowReady,
        PreviewSetWindowObserved,
        AwaitFirstPostWindowScannerReady,
        AwaitSecondPostWindowScannerReady,
        AwaitPredictedPreviewImageRead,
        PredictedPreviewImageReadObserved,
        PreviewImageStreaming,
        PreviewCleanupFirstObserved,
        PreviewCleanupFirstCompleted,
        PreviewCleanupSecondObserved,
        Complete,
        Failed
    }

    internal enum AspiOperationalReadCommand
    {
        ScannerReady,
        FaultPixelHeader,
        FaultPixelData,
        Calibration,
        D8Offset55,
        DynamicConfigurationD8
    }

    internal enum AspiLivePreviewImageCompletion
    {
        FullRow,
        RetryShortRow
    }

    internal sealed class AspiOperationalSequenceGate
    {
        internal const int MaximumInitializationCycles = 6;
        internal const int MaximumOfflineImageRows = 8192;
        internal const long MaximumCompletionMilliseconds = 15000;
        internal const int RecoveredScannerReadyRetryDelayMilliseconds = 255;
        internal const int RecoveredScannerReadyEscalationCadence = 7;
        internal const int MaximumPostWindowScannerReadyAttemptsPerPhase =
            256;
        internal const long MaximumPostWindowScannerReadyPhaseMilliseconds =
            60000;
        internal const long MaximumLivePreviewStreamMilliseconds = 120000;

        private static readonly byte[] KnownFaultPixelHeader = new byte[]
        {
            0x46, 0x61, 0x75, 0x6C, 0x20, 0x50, 0x69, 0x78,
            0x73, 0x00, 0x20, 0x20, 0x30, 0x0A, 0x20, 0x20,
            0x31, 0x0A, 0x20, 0x20, 0x30, 0x0A
        };

        private readonly Func<long> monotonicMilliseconds;
        private readonly string expectedPreviewSetWindowSha256;
        private readonly bool allowOfflinePreviewEnvelope;
        private readonly bool requirePostWindowScannerReadyPair;
        private readonly bool allowLivePreviewInStreamScannerReady;
        private readonly bool allowLivePreviewShortRetry;
        private readonly int livePreviewImageRowLimit;
        private readonly int livePreviewMaximumReadSubmissions;
        private readonly int livePreviewMaximumShortRetries;
        private readonly int livePreviewMaximumConsecutiveShortRetries;
        private readonly int livePreviewMaximumScannerReadyPolls;
        private readonly long livePreviewMaximumMilliseconds;
        private AspiOperationalSequenceState state;
        private bool completionPending;
        private AspiOperationalReadCommand pendingCommand;
        private AspiOperationalSequenceState pendingOriginState;
        private uint pendingLength;
        private long pendingStartedMilliseconds;
        private bool hasObservedTime;
        private long lastObservedMilliseconds;
        private int completedInitializationCycles;
        private int completedOfflineImageStreams;
        private int offlineImageRows;
        private byte offlineImageSelector;
        private uint offlineImageLength;
        private int firstPostWindowScannerReadyAttempts;
        private int secondPostWindowScannerReadyAttempts;
        private int livePreviewImageRows;
        private int livePreviewImageReadSubmissions;
        private int livePreviewShortRetries;
        private int livePreviewConsecutiveShortRetries;
        private bool livePreviewImageReadPending;
        private int livePreviewScannerReadyPolls;
        private bool liveFullScanImageStreamArmed;
        private int liveFullScanImageRowLimit;
        private int liveFullScanMaximumReadSubmissions;
        private int liveFullScanMaximumShortRetries;
        private bool liveFullScanTerminalProbePending;
        private bool liveFullScanTerminalProbeObserved;
        private int pendingPostWindowScannerReadyAttempt;
        private int pendingLivePreviewScannerReadyPoll;
        private bool liveFullScanInitialScannerReadyRetryArmed;
        private int liveFullScanInitialScannerReadyAttempts;
        private int pendingLiveFullScanInitialScannerReadyAttempt;
        private long liveFullScanInitialScannerReadyStartedMilliseconds = -1;
        private long pendingLiveFullScanInitialScannerReadyElapsedMilliseconds;
        private long livePreviewStreamStartedMilliseconds = -1;
        private long firstPostWindowScannerReadyStartedMilliseconds = -1;
        private long secondPostWindowScannerReadyStartedMilliseconds = -1;
        private long pendingPostWindowScannerReadyPhaseElapsedMilliseconds;
        private long lastPostWindowScannerReadyCompletionElapsedMilliseconds;

        internal AspiOperationalSequenceGate()
            : this(GetMonotonicMilliseconds,
                PrecisionTwoPreviewCommandManifest.SetWindowSha256)
        {
        }

        internal AspiOperationalSequenceGate(Func<long> monotonicMilliseconds)
            : this(monotonicMilliseconds,
                PrecisionTwoPreviewCommandManifest.SetWindowSha256)
        {
        }

        internal AspiOperationalSequenceGate(Func<long> monotonicMilliseconds,
            string expectedPreviewSetWindowSha256)
            : this(monotonicMilliseconds, expectedPreviewSetWindowSha256,
                false)
        {
        }

        internal AspiOperationalSequenceGate(Func<long> monotonicMilliseconds,
            string expectedPreviewSetWindowSha256,
            bool allowOfflinePreviewEnvelope)
            : this(monotonicMilliseconds, expectedPreviewSetWindowSha256,
                allowOfflinePreviewEnvelope, false)
        {
        }

        internal AspiOperationalSequenceGate(bool requirePostWindowReadyPair)
            : this(GetMonotonicMilliseconds,
                PrecisionTwoPreviewCommandManifest.SetWindowSha256, false,
                requirePostWindowReadyPair)
        {
        }

        internal AspiOperationalSequenceGate(
            string expectedPreviewSetWindowSha256,
            bool requirePostWindowReadyPair)
            : this(GetMonotonicMilliseconds,
                expectedPreviewSetWindowSha256, false,
                requirePostWindowReadyPair)
        {
        }

        internal AspiOperationalSequenceGate(Func<long> monotonicMilliseconds,
            string expectedPreviewSetWindowSha256,
            bool allowOfflinePreviewEnvelope,
            bool requirePostWindowScannerReadyPair)
            : this(monotonicMilliseconds, expectedPreviewSetWindowSha256,
                allowOfflinePreviewEnvelope,
                requirePostWindowScannerReadyPair, false)
        {
        }

        internal AspiOperationalSequenceGate(
            string expectedPreviewSetWindowSha256,
            bool requirePostWindowScannerReadyPair,
            bool allowLivePreviewInStreamScannerReady)
            : this(GetMonotonicMilliseconds,
                expectedPreviewSetWindowSha256, false,
                requirePostWindowScannerReadyPair,
                allowLivePreviewInStreamScannerReady)
        {
        }

        internal AspiOperationalSequenceGate(
            string expectedPreviewSetWindowSha256,
            bool requirePostWindowScannerReadyPair,
            bool allowLivePreviewInStreamScannerReady,
            bool allowLivePreviewShortRetry)
            : this(GetMonotonicMilliseconds,
                expectedPreviewSetWindowSha256, false,
                requirePostWindowScannerReadyPair,
                allowLivePreviewInStreamScannerReady,
                allowLivePreviewShortRetry)
        {
        }

        internal AspiOperationalSequenceGate(Func<long> monotonicMilliseconds,
            string expectedPreviewSetWindowSha256,
            bool allowOfflinePreviewEnvelope,
            bool requirePostWindowScannerReadyPair,
            bool allowLivePreviewInStreamScannerReady)
            : this(monotonicMilliseconds, expectedPreviewSetWindowSha256,
                allowOfflinePreviewEnvelope,
                requirePostWindowScannerReadyPair,
                allowLivePreviewInStreamScannerReady, false)
        {
        }

        internal AspiOperationalSequenceGate(Func<long> monotonicMilliseconds,
            string expectedPreviewSetWindowSha256,
            bool allowOfflinePreviewEnvelope,
            bool requirePostWindowScannerReadyPair,
            bool allowLivePreviewInStreamScannerReady,
            bool allowLivePreviewShortRetry)
            : this(monotonicMilliseconds, expectedPreviewSetWindowSha256,
                allowOfflinePreviewEnvelope,
                requirePostWindowScannerReadyPair,
                allowLivePreviewInStreamScannerReady,
                allowLivePreviewShortRetry,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamRows,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamMaximumReadSubmissions,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamMaximumShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamMaximumConsecutiveShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamMaximumScannerReadyPolls,
                MaximumLivePreviewStreamMilliseconds)
        {
        }

        internal AspiOperationalSequenceGate(
            string expectedPreviewSetWindowSha256,
            bool requirePostWindowScannerReadyPair,
            bool allowLivePreviewInStreamScannerReady,
            bool allowLivePreviewShortRetry, int livePreviewImageRowLimit,
            int livePreviewMaximumReadSubmissions,
            int livePreviewMaximumShortRetries,
            int livePreviewMaximumConsecutiveShortRetries,
            int livePreviewMaximumScannerReadyPolls,
            long livePreviewMaximumMilliseconds)
            : this(GetMonotonicMilliseconds, expectedPreviewSetWindowSha256,
                false, requirePostWindowScannerReadyPair,
                allowLivePreviewInStreamScannerReady,
                allowLivePreviewShortRetry, livePreviewImageRowLimit,
                livePreviewMaximumReadSubmissions,
                livePreviewMaximumShortRetries,
                livePreviewMaximumConsecutiveShortRetries,
                livePreviewMaximumScannerReadyPolls,
                livePreviewMaximumMilliseconds)
        {
        }

        internal AspiOperationalSequenceGate(Func<long> monotonicMilliseconds,
            string expectedPreviewSetWindowSha256,
            bool allowOfflinePreviewEnvelope,
            bool requirePostWindowScannerReadyPair,
            bool allowLivePreviewInStreamScannerReady,
            bool allowLivePreviewShortRetry, int livePreviewImageRowLimit,
            int livePreviewMaximumReadSubmissions,
            int livePreviewMaximumShortRetries,
            int livePreviewMaximumConsecutiveShortRetries,
            int livePreviewMaximumScannerReadyPolls,
            long livePreviewMaximumMilliseconds)
        {
            if (monotonicMilliseconds == null)
            {
                throw new ArgumentNullException("monotonicMilliseconds");
            }
            if (expectedPreviewSetWindowSha256 == null ||
                expectedPreviewSetWindowSha256.Length != 64)
            {
                throw new ArgumentException(
                    "An exact Preview SET WINDOW SHA-256 is required.",
                    "expectedPreviewSetWindowSha256");
            }
            if (livePreviewImageRowLimit <= 0 ||
                livePreviewMaximumShortRetries < 0 ||
                livePreviewMaximumReadSubmissions !=
                    livePreviewImageRowLimit +
                        livePreviewMaximumShortRetries ||
                livePreviewMaximumConsecutiveShortRetries <= 0 ||
                livePreviewMaximumConsecutiveShortRetries >
                    livePreviewMaximumShortRetries ||
                livePreviewMaximumScannerReadyPolls <= 0 ||
                livePreviewMaximumMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    "livePreviewImageRowLimit",
                    "Live Preview limits must define one exact positive " +
                    "row/poll/time boundary and submissions equal to rows " +
                    "plus total short retries.");
            }
            this.monotonicMilliseconds = monotonicMilliseconds;
            this.expectedPreviewSetWindowSha256 =
                expectedPreviewSetWindowSha256;
            this.allowOfflinePreviewEnvelope = allowOfflinePreviewEnvelope;
            this.requirePostWindowScannerReadyPair =
                requirePostWindowScannerReadyPair;
            this.allowLivePreviewInStreamScannerReady =
                allowLivePreviewInStreamScannerReady;
            this.allowLivePreviewShortRetry = allowLivePreviewShortRetry;
            this.livePreviewImageRowLimit = livePreviewImageRowLimit;
            this.livePreviewMaximumReadSubmissions =
                livePreviewMaximumReadSubmissions;
            this.livePreviewMaximumShortRetries =
                livePreviewMaximumShortRetries;
            this.livePreviewMaximumConsecutiveShortRetries =
                livePreviewMaximumConsecutiveShortRetries;
            this.livePreviewMaximumScannerReadyPolls =
                livePreviewMaximumScannerReadyPolls;
            this.livePreviewMaximumMilliseconds =
                livePreviewMaximumMilliseconds;
            state = AspiOperationalSequenceState.AwaitOperationalD8;
        }

        internal AspiOperationalSequenceState State
        {
            get { return state; }
        }

        internal int CompletedInitializationCycles
        {
            get { return completedInitializationCycles; }
        }

        internal int CompletedOfflineImageStreams
        {
            get { return completedOfflineImageStreams; }
        }

        internal bool CompletionPending
        {
            get { return completionPending; }
        }

        internal int PendingPostWindowScannerReadyAttempt
        {
            get { return pendingPostWindowScannerReadyAttempt; }
        }

        internal long LastPostWindowScannerReadyCompletionElapsedMilliseconds
        {
            get
            {
                return lastPostWindowScannerReadyCompletionElapsedMilliseconds;
            }
        }

        internal int LivePreviewImageRows
        {
            get { return livePreviewImageRows; }
        }

        internal int LivePreviewImageReadSubmissions
        {
            get { return livePreviewImageReadSubmissions; }
        }

        internal int LivePreviewShortRetries
        {
            get { return livePreviewShortRetries; }
        }

        internal int LivePreviewConsecutiveShortRetries
        {
            get { return livePreviewConsecutiveShortRetries; }
        }

        internal int PendingLivePreviewScannerReadyPoll
        {
            get { return pendingLivePreviewScannerReadyPoll; }
        }

        internal bool LiveFullScanImageStreamArmed
        {
            get { return liveFullScanImageStreamArmed; }
        }

        internal int LiveFullScanImageRowLimit
        {
            get { return liveFullScanImageRowLimit; }
        }

        internal int LiveFullScanMaximumReadSubmissions
        {
            get { return liveFullScanMaximumReadSubmissions; }
        }

        internal int LiveFullScanMaximumShortRetries
        {
            get { return liveFullScanMaximumShortRetries; }
        }

        internal bool LiveFullScanTerminalProbeObserved
        {
            get { return liveFullScanTerminalProbeObserved; }
        }

        internal int PendingLiveFullScanInitialScannerReadyAttempt
        {
            get { return pendingLiveFullScanInitialScannerReadyAttempt; }
        }

        internal long PendingLiveFullScanInitialScannerReadyElapsedMilliseconds
        {
            get
            {
                return
                    pendingLiveFullScanInitialScannerReadyElapsedMilliseconds;
            }
        }

        internal void BeginPostLoaderInitialization()
        {
            EnsureUsable();
            try
            {
                if (state !=
                        AspiOperationalSequenceState.AwaitOperationalD8 ||
                    completionPending)
                {
                    throw new InvalidOperationException(
                        "Post-loader initialization can be armed only once " +
                        "from the initial operational gate state.");
                }
                state = AspiOperationalSequenceState.
                    AwaitInitialScannerReady;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void RecordOperationalD8Completion(byte adapterStatus,
            uint residue, int actualLength, byte[] data)
        {
            EnsureUsable();
            try
            {
                if (state !=
                        AspiOperationalSequenceState.AwaitOperationalD8 ||
                    completionPending ||
                    adapterStatus != (byte)AdapterStatus.Success ||
                    residue != 0 || actualLength !=
                        ScsiFraming.PrecisionTwoLoaderBufferD8Length ||
                    data == null || data.Length !=
                        ScsiFraming.PrecisionTwoLoaderBufferD8Length)
                {
                    throw new InvalidOperationException(
                        "Operational D8 must complete once, successfully, " +
                        "and at its exact 66-byte length.");
                }
                state = AspiOperationalSequenceState.
                    AwaitInitialScannerReady;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal AspiOperationalReadCommand BeginRead(byte target, byte lun,
            byte[] cdb, uint requestedLength)
        {
            EnsureUsable();
            if (completionPending)
            {
                SetFailed();
                throw new InvalidOperationException(
                    "The previous operational ASPI read has no completion.");
            }

            try
            {
                if (target != 5 || lun != 0)
                {
                    throw new InvalidOperationException(
                        "The operational Precision II sequence requires " +
                        "target 5/LUN 0.");
                }

                AspiOperationalReadCommand command = ClassifyRead(cdb,
                    requestedLength);
                ValidateSuccessor(command);

                long now = ObserveTime();

                pendingPostWindowScannerReadyAttempt =
                    BeginPostWindowScannerReadyAttempt(command, now);
                pendingLivePreviewScannerReadyPoll =
                    BeginLivePreviewScannerReadyPoll(command, now);
                pendingLiveFullScanInitialScannerReadyAttempt =
                    BeginLiveFullScanInitialScannerReadyAttempt(command,
                        now);

                pendingOriginState = state;
                pendingCommand = command;
                pendingLength = requestedLength;
                pendingStartedMilliseconds = now;
                completionPending = true;
                return command;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal bool CompleteRead(byte adapterStatus, uint residue,
            int actualLength, byte[] data)
        {
            EnsureUsable();
            if (!completionPending)
            {
                SetFailed();
                throw new InvalidOperationException(
                    "No operational ASPI read is awaiting completion.");
            }

            try
            {
                long now = ObserveTime();
                if (IsPostWindowScannerReadyState(pendingOriginState))
                {
                    lastPostWindowScannerReadyCompletionElapsedMilliseconds =
                        checked(pendingPostWindowScannerReadyPhaseElapsedMilliseconds +
                            (now - pendingStartedMilliseconds));
                }
                else
                {
                    lastPostWindowScannerReadyCompletionElapsedMilliseconds =
                        0;
                }
                if (now - pendingStartedMilliseconds >
                        MaximumCompletionMilliseconds ||
                    adapterStatus != (byte)AdapterStatus.Success ||
                    residue != 0 || actualLength != pendingLength ||
                    data == null || data.Length != pendingLength)
                {
                    throw new InvalidOperationException(
                        "Operational read completion was not exact, " +
                        "successful, full length, and within its deadline.");
                }
                if (pendingCommand ==
                        AspiOperationalReadCommand.FaultPixelHeader &&
                    !Matches(data, KnownFaultPixelHeader))
                {
                    throw new ProtocolException(
                        "The fault-pixel header does not match the verified " +
                        "Precision II response.");
                }
                if (pendingCommand ==
                        AspiOperationalReadCommand.ScannerReady &&
                    pendingOriginState == AspiOperationalSequenceState.
                        PreviewImageStreaming &&
                    allowLivePreviewInStreamScannerReady)
                {
                    if ((data[0] != 0 && data[0] != 8) || data[1] != 0)
                    {
                        throw new ProtocolException(
                            "The direct in-stream ScannerReady path accepts " +
                            "only the observed 08 00 active or 00 00 " +
                            "phase-complete responses.");
                    }
                }
                else if (pendingCommand ==
                        AspiOperationalReadCommand.ScannerReady &&
                    pendingOriginState == AspiOperationalSequenceState.
                        AwaitInitialScannerReady &&
                    liveFullScanInitialScannerReadyRetryArmed)
                {
                    if ((data[0] != 0 && data[0] != 8) || data[1] != 0)
                    {
                        throw new ProtocolException(
                            "The guarded full-scan initial ScannerReady " +
                            "path accepts only the observed 08 00 active or " +
                            "00 00 ready responses.");
                    }
                    if (data[0] != 0)
                    {
                        completionPending = false;
                        pendingLength = 0;
                        return false;
                    }
                }
                else if (pendingCommand ==
                        AspiOperationalReadCommand.ScannerReady &&
                    data[0] != 0)
                {
                    if (IsPostWindowScannerReadyState(pendingOriginState))
                    {
                        completionPending = false;
                        pendingLength = 0;
                        return false;
                    }
                    throw new ProtocolException(
                        "The common Precision II ScannerReady path requires " +
                        "a zero first response byte.");
                }

                completionPending = false;
                AdvanceAfterCompletion();
                pendingLength = 0;
                return true;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void ObservePreviewSetWindow(byte target, byte lun,
            byte[] cdb, byte[] data)
        {
            EnsureUsable();
            try
            {
                bool initialWindow = state == AspiOperationalSequenceState.
                    PreviewSetWindowReady;
                bool firstCleanup = allowOfflinePreviewEnvelope &&
                    offlineImageRows != 0 && state ==
                        AspiOperationalSequenceState.PreviewImageStreaming;
                bool secondCleanup = allowOfflinePreviewEnvelope && state ==
                    AspiOperationalSequenceState.PreviewCleanupFirstCompleted;
                if (completionPending || target != 5 || lun != 0 ||
                    (!initialWindow && !firstCleanup && !secondCleanup) ||
                    initialWindow && completedInitializationCycles < 1)
                {
                    throw new InvalidOperationException(
                        "SET WINDOW is not an allowed initial or cleanup " +
                        "successor in the Preview replay state.");
                }
                if (allowOfflinePreviewEnvelope)
                {
                    PrecisionTwoPreviewCommandManifest.
                        ValidateSetWindowEnvelope(cdb, data);
                }
                else
                {
                    PrecisionTwoPreviewCommandManifest.ValidateSetWindow(cdb,
                        data, expectedPreviewSetWindowSha256);
                }

                // Consume the state before transport selection. Offline replay
                // completes the request synthetically; live policy may observe
                // it but still cannot send this data-out request to USB.
                state = initialWindow
                    ? AspiOperationalSequenceState.PreviewSetWindowObserved
                    : firstCleanup
                        ? AspiOperationalSequenceState.
                            PreviewCleanupFirstObserved
                        : AspiOperationalSequenceState.
                            PreviewCleanupSecondObserved;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void RecordPreviewSetWindowCompletion(byte adapterStatus,
            uint residue, int actualLength)
        {
            EnsureUsable();
            try
            {
                bool initialWindow = state == AspiOperationalSequenceState.
                    PreviewSetWindowObserved;
                bool firstCleanup = state == AspiOperationalSequenceState.
                    PreviewCleanupFirstObserved;
                bool secondCleanup = state == AspiOperationalSequenceState.
                    PreviewCleanupSecondObserved;
                if ((!initialWindow && !firstCleanup && !secondCleanup) ||
                    adapterStatus != (byte)AdapterStatus.Success ||
                    residue != 0 || actualLength !=
                        ScsiFraming.PrecisionTwoPreviewSetWindowLength)
                {
                    throw new InvalidOperationException(
                        "Preview SET WINDOW did not complete successfully at " +
                        "its exact 84-byte data-out length.");
                }
                if (secondCleanup)
                {
                    ++completedOfflineImageStreams;
                }
                state = initialWindow
                    ? (allowOfflinePreviewEnvelope ||
                        requirePostWindowScannerReadyPair)
                        ? AspiOperationalSequenceState.
                            AwaitFirstPostWindowScannerReady
                        : AspiOperationalSequenceState.
                            AwaitPredictedPreviewImageRead
                    : firstCleanup
                        ? AspiOperationalSequenceState.
                            PreviewCleanupFirstCompleted
                        : AspiOperationalSequenceState.Complete;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal uint ObservePredictedPreviewImageRead(byte target, byte lun,
            byte[] cdb, uint requestedLength)
        {
            EnsureUsable();
            try
            {
                if (target != 5 || lun != 0 || completionPending ||
                    state != AspiOperationalSequenceState.
                        AwaitPredictedPreviewImageRead)
                {
                    throw new InvalidOperationException(
                        "The predicted image READ is allowed only immediately " +
                        "after an exact successful Preview SET WINDOW.");
                }
                uint scanWidth = requirePostWindowScannerReadyPair
                    ? PrecisionTwoPreviewCommandManifest.
                        ValidateAspiLiveFirstImageRead(cdb, requestedLength)
                    : PrecisionTwoPreviewCommandManifest.
                        ValidatePredictedImageRead(cdb, requestedLength);
                state = AspiOperationalSequenceState.
                    PredictedPreviewImageReadObserved;
                return scanWidth;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal uint BeginLivePreviewImageBurstRead(byte target, byte lun,
            byte[] cdb, uint requestedLength, int maximumRows)
        {
            EnsureUsable();
            try
            {
                bool exactBurst = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewBurstRows &&
                    !allowLivePreviewInStreamScannerReady;
                bool exactStream = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamRows &&
                    allowLivePreviewInStreamScannerReady &&
                    !allowLivePreviewShortRetry;
                if ((!exactBurst && !exactStream) || target != 5 || lun != 0 ||
                    completionPending || livePreviewImageRows >= maximumRows ||
                    (livePreviewImageRows == 0 && state !=
                        AspiOperationalSequenceState.
                            AwaitPredictedPreviewImageRead) ||
                    (livePreviewImageRows != 0 && state !=
                        AspiOperationalSequenceState.PreviewImageStreaming) ||
                    !requirePostWindowScannerReadyPair ||
                    allowOfflinePreviewEnvelope)
                {
                    throw new InvalidOperationException(
                        "The live Preview image stream is outside its exact " +
                        "project-owned post-window boundary.");
                }
                long now = ObserveTime();
                if (exactStream)
                {
                    if (livePreviewStreamStartedMilliseconds < 0)
                    {
                        livePreviewStreamStartedMilliseconds = now;
                    }
                    if (now - livePreviewStreamStartedMilliseconds >=
                            MaximumLivePreviewStreamMilliseconds)
                    {
                        throw new ProtocolException(
                            "The bounded live Preview stream exceeded its " +
                            "120-second transport allowance.");
                    }
                }
                uint scanWidth = PrecisionTwoPreviewCommandManifest.
                    ValidateAspiLiveFirstImageRead(cdb, requestedLength);
                ++livePreviewImageRows;
                state = livePreviewImageRows == maximumRows
                    ? AspiOperationalSequenceState.
                        PredictedPreviewImageReadObserved
                    : AspiOperationalSequenceState.PreviewImageStreaming;
                return scanWidth;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal uint BeginLivePreviewImageShortRetryRead(byte target,
            byte lun, byte[] cdb, uint requestedLength)
        {
            EnsureUsable();
            try
            {
                int activeRowLimit = ActiveImageRowLimit;
                int activeMaximumReadSubmissions =
                    ActiveMaximumReadSubmissions;
                int activeMaximumShortRetries = ActiveMaximumShortRetries;
                int activeMaximumConsecutiveShortRetries =
                    ActiveMaximumConsecutiveShortRetries;
                if (!allowLivePreviewShortRetry ||
                    !allowLivePreviewInStreamScannerReady ||
                    !requirePostWindowScannerReadyPair ||
                    allowOfflinePreviewEnvelope || target != 5 || lun != 0 ||
                    completionPending || livePreviewImageReadPending ||
                    livePreviewImageRows >= activeRowLimit ||
                    livePreviewImageReadSubmissions >=
                        activeMaximumReadSubmissions ||
                    livePreviewShortRetries >=
                        activeMaximumShortRetries ||
                    livePreviewConsecutiveShortRetries >=
                        activeMaximumConsecutiveShortRetries ||
                    (livePreviewImageRows == 0 && state !=
                        AspiOperationalSequenceState.
                            AwaitPredictedPreviewImageRead && state !=
                        AspiOperationalSequenceState.PreviewImageStreaming) ||
                    (livePreviewImageRows != 0 && state !=
                        AspiOperationalSequenceState.PreviewImageStreaming))
                {
                    throw new InvalidOperationException(
                        "The live image short-retry stream is outside its " +
                        "exact project-owned row, retry, or submission " +
                        "boundary.");
                }
                long now = ObserveTime();
                if (livePreviewStreamStartedMilliseconds < 0)
                {
                    livePreviewStreamStartedMilliseconds = now;
                }
                if (now - livePreviewStreamStartedMilliseconds >=
                        ActiveMaximumMilliseconds)
                {
                    throw new ProtocolException(
                        "The bounded live image short-retry stream " +
                        "exceeded its exact transport-time allowance.");
                }
                uint scanWidth = liveFullScanImageStreamArmed
                    ? PrecisionTwoPreviewCommandManifest.
                        ValidateAspiLiveFullScanImageRead(cdb,
                            requestedLength)
                    : PrecisionTwoPreviewCommandManifest.
                        ValidateAspiLiveFirstImageRead(cdb, requestedLength);

                // Consume a submission before USB. A timeout, disconnect, or
                // malformed short completion can never regain this allowance.
                ++livePreviewImageReadSubmissions;
                livePreviewImageReadPending = true;
                state = AspiOperationalSequenceState.PreviewImageStreaming;
                return scanWidth;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal AspiLivePreviewImageCompletion
            CompleteLivePreviewImageShortRetryRead(byte adapterStatus,
                uint requestedLength, uint residue, int actualLength,
                byte[] data)
        {
            EnsureUsable();
            try
            {
                int activeRowLimit = ActiveImageRowLimit;
                int activeMaximumShortRetries = ActiveMaximumShortRetries;
                int activeMaximumConsecutiveShortRetries =
                    ActiveMaximumConsecutiveShortRetries;
                uint activeRequestedLength = liveFullScanImageStreamArmed
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanLength
                    : PrecisionTwoPreviewCommandManifest.
                        AspiLiveFirstImageLength;
                if (!allowLivePreviewShortRetry ||
                    !livePreviewImageReadPending ||
                    requestedLength != activeRequestedLength ||
                    adapterStatus != (byte)AdapterStatus.Success ||
                    data == null || actualLength < 0 ||
                    data.Length != actualLength || residue > requestedLength ||
                    checked((uint)actualLength) != requestedLength - residue)
                {
                    throw new InvalidOperationException(
                        "The live image short-retry completion is not a " +
                        "consistent successful transport result.");
                }

                livePreviewImageReadPending = false;
                if (actualLength == requestedLength && residue == 0)
                {
                    ++livePreviewImageRows;
                    livePreviewConsecutiveShortRetries = 0;
                    state = livePreviewImageRows == activeRowLimit &&
                            !liveFullScanImageStreamArmed
                        ? AspiOperationalSequenceState.
                            PredictedPreviewImageReadObserved
                        : AspiOperationalSequenceState.PreviewImageStreaming;
                    return AspiLivePreviewImageCompletion.FullRow;
                }

                uint expectedResidue = requestedLength -
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewObservedShortLength;
                if (actualLength != PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewObservedShortLength ||
                    residue != expectedResidue ||
                    livePreviewShortRetries >=
                        activeMaximumShortRetries ||
                    livePreviewConsecutiveShortRetries >=
                        activeMaximumConsecutiveShortRetries)
                {
                    throw new InvalidOperationException(
                        "The live image completion is neither one " +
                        "full row nor the exact bounded 10-byte short " +
                        "observation.");
                }

                ++livePreviewShortRetries;
                ++livePreviewConsecutiveShortRetries;
                state = AspiOperationalSequenceState.PreviewImageStreaming;
                return AspiLivePreviewImageCompletion.RetryShortRow;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal uint BeginLiveFullScanTerminalProbeRead(byte target,
            byte lun, byte[] cdb, uint requestedLength)
        {
            EnsureUsable();
            try
            {
                if (!liveFullScanImageStreamArmed ||
                    livePreviewImageRows !=
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanRows ||
                    state != AspiOperationalSequenceState.
                        PreviewImageStreaming ||
                    target != 5 || lun != 0 || completionPending ||
                    livePreviewImageReadPending ||
                    liveFullScanTerminalProbePending ||
                    liveFullScanTerminalProbeObserved ||
                    livePreviewImageReadSubmissions >
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanMaximumReadSubmissions)
                {
                    throw new InvalidOperationException(
                        "The full-scan terminal probe is permitted exactly " +
                        "once, only after 762 accepted rows.");
                }
                long now = ObserveTime();
                if (livePreviewStreamStartedMilliseconds < 0 ||
                    now - livePreviewStreamStartedMilliseconds >=
                        ActiveMaximumMilliseconds)
                {
                    throw new ProtocolException(
                        "The full-scan terminal probe exceeded the exact " +
                        "stream-time allowance.");
                }
                uint scanWidth = PrecisionTwoPreviewCommandManifest.
                    ValidateAspiLiveFullScanImageRead(cdb, requestedLength);

                // This is a separate one-shot allowance beyond the bounded
                // row stream. Consume it before USB so no failed observation
                // can be repeated.
                ++livePreviewImageReadSubmissions;
                liveFullScanTerminalProbePending = true;
                return scanWidth;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void RecordLiveFullScanTerminalProbeCompletion(
            byte adapterStatus, uint requestedLength, uint residue,
            int actualLength, byte[] data)
        {
            EnsureUsable();
            try
            {
                if (!liveFullScanTerminalProbePending ||
                    requestedLength !=
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanLength ||
                    data == null || actualLength < 0 ||
                    data.Length != actualLength || residue > requestedLength ||
                    checked((uint)actualLength) != requestedLength - residue)
                {
                    throw new InvalidOperationException(
                        "The full-scan terminal probe completion is not a " +
                        "consistent one-shot transport result.");
                }

                // Any well-framed status/length combination is evidence. It
                // is quarantined rather than interpreted or returned to the
                // application until hardware establishes its semantics.
                liveFullScanTerminalProbePending = false;
                liveFullScanTerminalProbeObserved = true;
                state = AspiOperationalSequenceState.Failed;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal uint BeginOfflinePreviewImageRead(byte target, byte lun,
            byte[] cdb, uint requestedLength)
        {
            EnsureUsable();
            try
            {
                if (!allowOfflinePreviewEnvelope || target != 5 || lun != 0 ||
                    completionPending ||
                    (state != AspiOperationalSequenceState.
                        AwaitPredictedPreviewImageRead &&
                     state != AspiOperationalSequenceState.
                        PreviewImageStreaming) ||
                    offlineImageRows >= MaximumOfflineImageRows)
                {
                    throw new InvalidOperationException(
                        "Offline image replay is outside its bounded " +
                        "post-window state.");
                }
                uint scanWidth = PrecisionTwoPreviewCommandManifest.
                    ValidateOfflineImageRead(cdb, requestedLength);
                if (offlineImageRows == 0)
                {
                    offlineImageSelector = cdb[4];
                    offlineImageLength = requestedLength;
                }
                else if (offlineImageSelector != cdb[4] ||
                    offlineImageLength != requestedLength)
                {
                    throw new ProtocolException(
                        "Offline image replay changed selector or row length " +
                        "during one Preview stream.");
                }
                ++offlineImageRows;
                state = AspiOperationalSequenceState.PreviewImageStreaming;
                return scanWidth;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void BeginOfflineRepeatScanInitialization()
        {
            EnsureUsable();
            try
            {
                bool warmFirstStream = completedOfflineImageStreams == 0 &&
                    completedInitializationCycles ==
                        MaximumInitializationCycles - 3;
                if (!allowOfflinePreviewEnvelope || completionPending ||
                    state != AspiOperationalSequenceState.Complete ||
                    (completedOfflineImageStreams != 1 &&
                     !warmFirstStream) ||
                    completedInitializationCycles < 1 ||
                    completedInitializationCycles >= MaximumInitializationCycles)
                {
                    throw new InvalidOperationException(
                        "Offline repeat-scan initialization requires one " +
                        "completed image stream, or the exact seeded warm " +
                        "first-stream state, and remaining bounded " +
                        "initialization capacity.");
                }

                offlineImageRows = 0;
                offlineImageSelector = 0;
                offlineImageLength = 0;
                firstPostWindowScannerReadyAttempts = 0;
                secondPostWindowScannerReadyAttempts = 0;
                pendingPostWindowScannerReadyAttempt = 0;
                firstPostWindowScannerReadyStartedMilliseconds = -1;
                secondPostWindowScannerReadyStartedMilliseconds = -1;
                pendingPostWindowScannerReadyPhaseElapsedMilliseconds = 0;
                lastPostWindowScannerReadyCompletionElapsedMilliseconds = 0;
                state = AspiOperationalSequenceState.AwaitInitialScannerReady;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void BeginOfflineWarmOperatorCycle()
        {
            EnsureUsable();
            try
            {
                if (!allowOfflinePreviewEnvelope || completionPending ||
                    state != AspiOperationalSequenceState.
                        AwaitOperationalD8 ||
                    completedInitializationCycles != 0 ||
                    completedOfflineImageStreams != 0)
                {
                    throw new InvalidOperationException(
                        "An offline warm operator cycle can be seeded only " +
                        "from a new bounded replay gate.");
                }

                // A warm FlexColor process begins its next Preview with the
                // same INQUIRY/DF prefix used for a repeat scan, not with the
                // cold-start D8. Reserve exactly three remaining one-cycle
                // initializations: two stabilization cycles observed before
                // the warm Preview and one before Scan.
                completedInitializationCycles =
                    MaximumInitializationCycles - 3;
                state = AspiOperationalSequenceState.Complete;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void BeginLiveWarmOperatorCycle()
        {
            EnsureUsable();
            try
            {
                if (allowOfflinePreviewEnvelope ||
                    !requirePostWindowScannerReadyPair ||
                    !allowLivePreviewInStreamScannerReady ||
                    !allowLivePreviewShortRetry || completionPending ||
                    state != AspiOperationalSequenceState.
                        AwaitOperationalD8 ||
                    completedInitializationCycles != 0 ||
                    livePreviewImageRows != 0)
                {
                    throw new InvalidOperationException(
                        "A live warm operator cycle requires a new exact " +
                        "powered Preview/full-scan gate.");
                }
                completedInitializationCycles =
                    MaximumInitializationCycles - 3;
                state = AspiOperationalSequenceState.Complete;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void BeginLiveWarmOperatorPreviewInitialization()
        {
            EnsureUsable();
            try
            {
                if (allowOfflinePreviewEnvelope ||
                    !requirePostWindowScannerReadyPair ||
                    !allowLivePreviewInStreamScannerReady ||
                    !allowLivePreviewShortRetry || completionPending ||
                    state != AspiOperationalSequenceState.Complete ||
                    completedInitializationCycles !=
                        MaximumInitializationCycles - 3 ||
                    livePreviewImageRows != 0 ||
                    livePreviewImageReadSubmissions != 0)
                {
                    throw new InvalidOperationException(
                        "Live warm Preview initialization requires the exact " +
                        "new seeded operator state.");
                }
                firstPostWindowScannerReadyAttempts = 0;
                secondPostWindowScannerReadyAttempts = 0;
                pendingPostWindowScannerReadyAttempt = 0;
                firstPostWindowScannerReadyStartedMilliseconds = -1;
                secondPostWindowScannerReadyStartedMilliseconds = -1;
                pendingPostWindowScannerReadyPhaseElapsedMilliseconds = 0;
                lastPostWindowScannerReadyCompletionElapsedMilliseconds = 0;
                liveFullScanInitialScannerReadyRetryArmed = true;
                liveFullScanInitialScannerReadyAttempts = 0;
                pendingLiveFullScanInitialScannerReadyAttempt = 0;
                liveFullScanInitialScannerReadyStartedMilliseconds = -1;
                pendingLiveFullScanInitialScannerReadyElapsedMilliseconds = 0;
                state = AspiOperationalSequenceState.AwaitInitialScannerReady;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void BeginLiveFullScanSuccessorObservation()
        {
            EnsureUsable();
            try
            {
                if (allowOfflinePreviewEnvelope || completionPending ||
                    !requirePostWindowScannerReadyPair ||
                    !allowLivePreviewInStreamScannerReady ||
                    !allowLivePreviewShortRetry ||
                    state != AspiOperationalSequenceState.
                        PredictedPreviewImageReadObserved ||
                    livePreviewImageRows != livePreviewImageRowLimit ||
                    completedInitializationCycles < 1 ||
                    completedInitializationCycles >=
                        MaximumInitializationCycles)
                {
                    throw new InvalidOperationException(
                        "Live full-scan successor observation requires one " +
                        "complete bounded Preview stream and remaining " +
                        "initialization capacity.");
                }

                firstPostWindowScannerReadyAttempts = 0;
                secondPostWindowScannerReadyAttempts = 0;
                pendingPostWindowScannerReadyAttempt = 0;
                firstPostWindowScannerReadyStartedMilliseconds = -1;
                secondPostWindowScannerReadyStartedMilliseconds = -1;
                pendingPostWindowScannerReadyPhaseElapsedMilliseconds = 0;
                lastPostWindowScannerReadyCompletionElapsedMilliseconds = 0;
                liveFullScanInitialScannerReadyRetryArmed = true;
                liveFullScanInitialScannerReadyAttempts = 0;
                pendingLiveFullScanInitialScannerReadyAttempt = 0;
                liveFullScanInitialScannerReadyStartedMilliseconds = -1;
                pendingLiveFullScanInitialScannerReadyElapsedMilliseconds = 0;
                state = AspiOperationalSequenceState.AwaitInitialScannerReady;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void ObserveLiveFullScanSetWindowSuccessor(byte target,
            byte lun, byte[] cdb, byte[] data)
        {
            EnsureUsable();
            try
            {
                if (allowOfflinePreviewEnvelope || completionPending ||
                    state != AspiOperationalSequenceState.
                        PreviewSetWindowReady || target != 5 || lun != 0)
                {
                    throw new InvalidOperationException(
                        "The full-scan SET WINDOW fingerprint is allowed " +
                        "only at the bounded repeat initialization boundary.");
                }
                PrecisionTwoPreviewCommandManifest.ValidateSetWindowEnvelope(
                    cdb, data);
                state = AspiOperationalSequenceState.Failed;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void BeginLiveFullScanSetWindowExecution(byte target,
            byte lun, byte[] cdb, byte[] data, string expectedSha256)
        {
            EnsureUsable();
            try
            {
                if (allowOfflinePreviewEnvelope || completionPending ||
                    state != AspiOperationalSequenceState.
                        PreviewSetWindowReady || target != 5 || lun != 0)
                {
                    throw new InvalidOperationException(
                        "The full-scan SET WINDOW execution is allowed only " +
                        "at the bounded repeat initialization boundary.");
                }
                PrecisionTwoPreviewCommandManifest.ValidateSetWindow(cdb,
                    data, expectedSha256);
                state = AspiOperationalSequenceState.
                    PreviewSetWindowObserved;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void ArmLiveFullScanImageStream()
        {
            ArmLiveFullScanImageStream(false);
        }

        internal void ArmLiveFullScanImageStream(
            bool naturalCompletionEnabled)
        {
            ArmLiveFullScanImageStream(naturalCompletionEnabled, false,
                false);
        }

        internal void ArmLiveFullScanImageStream(
            bool naturalCompletionEnabled,
            bool row997CompletionEnabled)
        {
            ArmLiveFullScanImageStream(naturalCompletionEnabled,
                row997CompletionEnabled, false);
        }

        internal void ArmLiveFullScanImageStream(
            bool naturalCompletionEnabled,
            bool row997CompletionEnabled,
            bool progressCompletionEnabled)
        {
            EnsureUsable();
            try
            {
                if ((naturalCompletionEnabled ? 1 : 0) +
                        (row997CompletionEnabled ? 1 : 0) +
                        (progressCompletionEnabled ? 1 : 0) > 1 ||
                    allowOfflinePreviewEnvelope || completionPending ||
                    liveFullScanImageStreamArmed ||
                    state != AspiOperationalSequenceState.
                        AwaitFirstPostWindowScannerReady ||
                    completedInitializationCycles !=
                        MaximumInitializationCycles ||
                    livePreviewImageRows != livePreviewImageRowLimit ||
                    livePreviewImageReadPending)
                {
                    throw new InvalidOperationException(
                        "The full-scan image stream can be armed only after " +
                        "the exact completed Preview and successful repeat " +
                        "SET WINDOW boundary.");
                }
                livePreviewImageRows = 0;
                livePreviewImageReadSubmissions = 0;
                livePreviewShortRetries = 0;
                livePreviewConsecutiveShortRetries = 0;
                livePreviewScannerReadyPolls = 0;
                pendingLivePreviewScannerReadyPoll = 0;
                livePreviewStreamStartedMilliseconds = -1;
                liveFullScanImageRowLimit = progressCompletionEnabled
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanProgressMaximumRows
                    : row997CompletionEnabled
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanRow997CompletionRows
                    : naturalCompletionEnabled
                        ? PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanNaturalRows
                        : PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanRows;
                liveFullScanMaximumShortRetries = progressCompletionEnabled
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanProgressMaximumShortRetries
                    : row997CompletionEnabled
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanRow997CompletionMaximumShortRetries
                    : naturalCompletionEnabled
                        ? PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanNaturalMaximumShortRetries
                        : PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanMaximumShortRetries;
                liveFullScanMaximumReadSubmissions =
                    progressCompletionEnabled
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanProgressMaximumReadSubmissions
                    : row997CompletionEnabled
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanRow997CompletionMaximumReadSubmissions
                    : naturalCompletionEnabled
                        ? PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanNaturalMaximumReadSubmissions
                        : PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanMaximumReadSubmissions;
                liveFullScanImageStreamArmed = true;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal uint ObserveLiveFullScanFirstImageRead(byte target,
            byte lun, byte[] cdb, uint requestedLength)
        {
            EnsureUsable();
            try
            {
                if (target != 5 || lun != 0 || completionPending ||
                    state != AspiOperationalSequenceState.
                        AwaitPredictedPreviewImageRead)
                {
                    throw new InvalidOperationException(
                        "The full-scan first image READ is allowed only " +
                        "after its exact SET WINDOW and two readiness " +
                        "phases.");
                }
                uint width = PrecisionTwoPreviewCommandManifest.
                    ValidatePredictedImageRead(cdb, requestedLength);
                state = AspiOperationalSequenceState.Failed;
                return width;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void ObserveLiveFullScanStartupWrite(byte target, byte lun,
            byte[] cdb, byte[] data)
        {
            EnsureUsable();
            try
            {
                if (allowOfflinePreviewEnvelope || completionPending ||
                    state != AspiOperationalSequenceState.
                        PreviewSetWindowReady || target != 5 || lun != 0 ||
                    data == null || data.Length != ScsiFraming.
                        PrecisionTwoStartupWriteBuffer1082Length ||
                    !Matches(cdb, ScsiFraming.
                        BuildPrecisionTwoStartupWriteBuffer1082Cdb()))
                {
                    throw new InvalidOperationException(
                        "The startup WRITE BUFFER fingerprint is allowed " +
                        "only after the bounded repeat initialization.");
                }
                state = AspiOperationalSequenceState.Failed;
            }
            catch
            {
                SetFailed();
                throw;
            }
        }

        internal void BeginOfflinePostCancellationRecoveryCycle()
        {
            EnsureUsable();
            try
            {
                if (!allowOfflinePreviewEnvelope || completionPending ||
                    state != AspiOperationalSequenceState.
                        PreviewSetWindowReady ||
                    completedOfflineImageStreams != 1 ||
                    completedInitializationCycles !=
                        MaximumInitializationCycles)
                {
                    throw new InvalidOperationException(
                        "Offline post-cancellation recovery requires one " +
                        "cancelled stream, the completed sixth initialization " +
                        "cycle, and the exact fault-header successor.");
                }
                state = AspiOperationalSequenceState.AwaitFaultPixelHeader;
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

        private void ValidateSuccessor(AspiOperationalReadCommand command)
        {
            bool valid =
                state == AspiOperationalSequenceState.
                    AwaitInitialScannerReady &&
                    command == AspiOperationalReadCommand.ScannerReady ||
                state == AspiOperationalSequenceState.
                    AwaitFaultPixelHeader &&
                    command == AspiOperationalReadCommand.FaultPixelHeader ||
                state == AspiOperationalSequenceState.
                    AwaitPostFaultScannerReady &&
                    command == AspiOperationalReadCommand.ScannerReady ||
                state == AspiOperationalSequenceState.
                    AwaitFaultPixelData &&
                    command == AspiOperationalReadCommand.FaultPixelData ||
                state == AspiOperationalSequenceState.AwaitCalibration &&
                    command == AspiOperationalReadCommand.Calibration ||
                state == AspiOperationalSequenceState.
                    AwaitD8Offset55OrPostCalibrationScannerReady &&
                    (command == AspiOperationalReadCommand.D8Offset55 ||
                     command == AspiOperationalReadCommand.
                        DynamicConfigurationD8 ||
                     command == AspiOperationalReadCommand.ScannerReady) ||
                state == AspiOperationalSequenceState.
                    AwaitPostCalibrationScannerReady &&
                    command == AspiOperationalReadCommand.ScannerReady ||
                state == AspiOperationalSequenceState.
                    PreviewSetWindowReady &&
                    completedInitializationCycles <
                        MaximumInitializationCycles &&
                    command == AspiOperationalReadCommand.FaultPixelHeader ||
                (allowOfflinePreviewEnvelope ||
                    requirePostWindowScannerReadyPair) && state ==
                    AspiOperationalSequenceState.
                        AwaitFirstPostWindowScannerReady &&
                    command == AspiOperationalReadCommand.ScannerReady ||
                (allowOfflinePreviewEnvelope ||
                    requirePostWindowScannerReadyPair) && state ==
                    AspiOperationalSequenceState.
                        AwaitSecondPostWindowScannerReady &&
                    command == AspiOperationalReadCommand.ScannerReady ||
                (allowOfflinePreviewEnvelope ||
                    allowLivePreviewInStreamScannerReady) && state ==
                    AspiOperationalSequenceState.PreviewImageStreaming &&
                    command == AspiOperationalReadCommand.ScannerReady;
            if (!valid)
            {
                throw new InvalidOperationException(
                    "The operational ASPI read is not the exact successor " +
                    "for the current initialization state.");
            }
        }

        private int BeginPostWindowScannerReadyAttempt(
            AspiOperationalReadCommand command, long now)
        {
            if (command != AspiOperationalReadCommand.ScannerReady)
            {
                pendingPostWindowScannerReadyPhaseElapsedMilliseconds = 0;
                return 0;
            }

            if (state == AspiOperationalSequenceState.
                    AwaitFirstPostWindowScannerReady)
            {
                return BeginPostWindowScannerReadyPhaseAttempt(
                    ref firstPostWindowScannerReadyAttempts,
                    ref firstPostWindowScannerReadyStartedMilliseconds,
                    now, "first");
            }
            if (state == AspiOperationalSequenceState.
                    AwaitSecondPostWindowScannerReady)
            {
                return BeginPostWindowScannerReadyPhaseAttempt(
                    ref secondPostWindowScannerReadyAttempts,
                    ref secondPostWindowScannerReadyStartedMilliseconds,
                    now, "second");
            }
            pendingPostWindowScannerReadyPhaseElapsedMilliseconds = 0;
            return 0;
        }

        private int BeginLivePreviewScannerReadyPoll(
            AspiOperationalReadCommand command, long now)
        {
            if (!allowLivePreviewInStreamScannerReady ||
                command != AspiOperationalReadCommand.ScannerReady ||
                state != AspiOperationalSequenceState.PreviewImageStreaming)
            {
                return 0;
            }
            int activeRowLimit = ActiveImageRowLimit;
            if (livePreviewImageRows <= 0 ||
                livePreviewImageRows > activeRowLimit ||
                (!liveFullScanImageStreamArmed &&
                 livePreviewImageRows >= activeRowLimit))
            {
                throw new ProtocolException(
                    "An in-stream ScannerReady poll is outside the bounded " +
                    "image-row interval.");
            }
            if (livePreviewStreamStartedMilliseconds < 0 ||
                now - livePreviewStreamStartedMilliseconds >=
                    ActiveMaximumMilliseconds)
            {
                throw new ProtocolException(
                    "The bounded live Preview stream exceeded its exact " +
                    "transport-time allowance.");
            }
            if (livePreviewScannerReadyPolls >=
                    ActiveMaximumScannerReadyPolls)
            {
                throw new ProtocolException(
                    "The bounded live Preview stream exceeded its exact " +
                    "ScannerReady poll allowance.");
            }
            return ++livePreviewScannerReadyPolls;
        }

        private int BeginLiveFullScanInitialScannerReadyAttempt(
            AspiOperationalReadCommand command, long now)
        {
            if (!liveFullScanInitialScannerReadyRetryArmed ||
                command != AspiOperationalReadCommand.ScannerReady ||
                state != AspiOperationalSequenceState.
                    AwaitInitialScannerReady)
            {
                pendingLiveFullScanInitialScannerReadyElapsedMilliseconds = 0;
                return 0;
            }
            if (liveFullScanInitialScannerReadyStartedMilliseconds < 0)
            {
                liveFullScanInitialScannerReadyStartedMilliseconds = now;
            }
            pendingLiveFullScanInitialScannerReadyElapsedMilliseconds =
                now - liveFullScanInitialScannerReadyStartedMilliseconds;
            if (pendingLiveFullScanInitialScannerReadyElapsedMilliseconds >=
                    MaximumPostWindowScannerReadyPhaseMilliseconds)
            {
                throw new ProtocolException(
                    "The guarded full-scan initial ScannerReady loop " +
                    "exceeded its bounded 60-second phase allowance.");
            }
            if (liveFullScanInitialScannerReadyAttempts >=
                    MaximumPostWindowScannerReadyAttemptsPerPhase)
            {
                throw new ProtocolException(
                    "The guarded full-scan initial ScannerReady loop " +
                    "exceeded its bounded 256-attempt transport allowance.");
            }
            return ++liveFullScanInitialScannerReadyAttempts;
        }

        private int BeginPostWindowScannerReadyPhaseAttempt(
            ref int attempts, ref long phaseStartedMilliseconds, long now,
            string phaseName)
        {
            if (phaseStartedMilliseconds < 0)
            {
                phaseStartedMilliseconds = now;
            }
            pendingPostWindowScannerReadyPhaseElapsedMilliseconds =
                now - phaseStartedMilliseconds;
            if (pendingPostWindowScannerReadyPhaseElapsedMilliseconds >=
                    MaximumPostWindowScannerReadyPhaseMilliseconds)
            {
                throw new ProtocolException(
                    "The " + phaseName + " post-window ScannerReady loop " +
                    "exceeded its bounded 60-second phase allowance.");
            }
            if (attempts >=
                    MaximumPostWindowScannerReadyAttemptsPerPhase)
            {
                throw new ProtocolException(
                    "The " + phaseName + " post-window ScannerReady loop " +
                    "exceeded its bounded 256-attempt transport allowance.");
            }
            return ++attempts;
        }

        private static bool IsPostWindowScannerReadyState(
            AspiOperationalSequenceState candidate)
        {
            return candidate == AspiOperationalSequenceState.
                    AwaitFirstPostWindowScannerReady ||
                candidate == AspiOperationalSequenceState.
                    AwaitSecondPostWindowScannerReady;
        }

        private void AdvanceAfterCompletion()
        {
            switch (pendingCommand)
            {
                case AspiOperationalReadCommand.ScannerReady:
                    if (pendingOriginState == AspiOperationalSequenceState.
                            AwaitInitialScannerReady)
                    {
                        liveFullScanInitialScannerReadyRetryArmed = false;
                        state = AspiOperationalSequenceState.
                            AwaitFaultPixelHeader;
                    }
                    else if (pendingOriginState ==
                        AspiOperationalSequenceState.
                            AwaitPostFaultScannerReady)
                    {
                        state = AspiOperationalSequenceState.
                            AwaitFaultPixelData;
                    }
                    else if (pendingOriginState ==
                        AspiOperationalSequenceState.
                            AwaitFirstPostWindowScannerReady)
                    {
                        state = AspiOperationalSequenceState.
                            AwaitSecondPostWindowScannerReady;
                    }
                    else if (pendingOriginState ==
                        AspiOperationalSequenceState.
                            AwaitSecondPostWindowScannerReady)
                    {
                        state = AspiOperationalSequenceState.
                            AwaitPredictedPreviewImageRead;
                    }
                    else if (pendingOriginState ==
                        AspiOperationalSequenceState.PreviewImageStreaming)
                    {
                        state = AspiOperationalSequenceState.
                            PreviewImageStreaming;
                    }
                    else
                    {
                        ++completedInitializationCycles;
                        state = AspiOperationalSequenceState.
                            PreviewSetWindowReady;
                    }
                    break;
                case AspiOperationalReadCommand.FaultPixelHeader:
                    state = AspiOperationalSequenceState.
                        AwaitPostFaultScannerReady;
                    break;
                case AspiOperationalReadCommand.FaultPixelData:
                    state = AspiOperationalSequenceState.AwaitCalibration;
                    break;
                case AspiOperationalReadCommand.Calibration:
                    state = AspiOperationalSequenceState.
                        AwaitD8Offset55OrPostCalibrationScannerReady;
                    break;
                case AspiOperationalReadCommand.D8Offset55:
                case AspiOperationalReadCommand.DynamicConfigurationD8:
                    state = AspiOperationalSequenceState.
                        AwaitPostCalibrationScannerReady;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unknown operational command completion.");
            }
        }

        private static AspiOperationalReadCommand ClassifyRead(byte[] cdb,
            uint requestedLength)
        {
            if (requestedLength == ScsiFraming.PrecisionTwoScannerReadyLength &&
                Matches(cdb, ScsiFraming.BuildPrecisionTwoScannerReadyCdb()))
            {
                return AspiOperationalReadCommand.ScannerReady;
            }
            if (requestedLength ==
                    ScsiFraming.PrecisionTwoFaultPixelBufferLength &&
                Matches(cdb,
                    ScsiFraming.BuildPrecisionTwoFaultPixelReadBufferCdb()))
            {
                return AspiOperationalReadCommand.FaultPixelHeader;
            }
            if (requestedLength ==
                    ScsiFraming.PrecisionTwoFaultPixelDataLength &&
                Matches(cdb,
                    ScsiFraming.BuildPrecisionTwoFaultPixelDataReadBufferCdb()))
            {
                return AspiOperationalReadCommand.FaultPixelData;
            }
            if (requestedLength ==
                    ScsiFraming.PrecisionTwoCalibrationBufferLength &&
                Matches(cdb,
                    ScsiFraming.BuildPrecisionTwoCalibrationReadBufferCdb()))
            {
                return AspiOperationalReadCommand.Calibration;
            }
            if (requestedLength ==
                    ScsiFraming.PrecisionTwoD8Offset55Length &&
                Matches(cdb,
                    ScsiFraming.BuildPrecisionTwoD8Offset55ReadBufferCdb()))
            {
                return AspiOperationalReadCommand.D8Offset55;
            }
            if (requestedLength ==
                    ScsiFraming.PrecisionTwoDynamicConfigurationLength &&
                Matches(cdb, ScsiFraming.
                    BuildPrecisionTwoDynamicConfigurationD8ReadBufferCdb()))
            {
                return AspiOperationalReadCommand.DynamicConfigurationD8;
            }
            throw new ProtocolException(
                "The CDB is not an exact verified operational read form.");
        }

        private int ActiveImageRowLimit
        {
            get
            {
                return liveFullScanImageStreamArmed
                    ? liveFullScanImageRowLimit
                    : livePreviewImageRowLimit;
            }
        }

        private int ActiveMaximumReadSubmissions
        {
            get
            {
                return liveFullScanImageStreamArmed
                    ? liveFullScanMaximumReadSubmissions
                    : livePreviewMaximumReadSubmissions;
            }
        }

        private int ActiveMaximumShortRetries
        {
            get
            {
                return liveFullScanImageStreamArmed
                    ? liveFullScanMaximumShortRetries
                    : livePreviewMaximumShortRetries;
            }
        }

        private int ActiveMaximumConsecutiveShortRetries
        {
            get
            {
                return liveFullScanImageStreamArmed
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanMaximumConsecutiveShortRetries
                    : livePreviewMaximumConsecutiveShortRetries;
            }
        }

        private int ActiveMaximumScannerReadyPolls
        {
            get
            {
                return liveFullScanImageStreamArmed
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanMaximumScannerReadyPolls
                    : livePreviewMaximumScannerReadyPolls;
            }
        }

        private long ActiveMaximumMilliseconds
        {
            get
            {
                return liveFullScanImageStreamArmed
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanMaximumMilliseconds
                    : livePreviewMaximumMilliseconds;
            }
        }

        private void EnsureUsable()
        {
            if (state == AspiOperationalSequenceState.Failed)
            {
                throw new InvalidOperationException(
                    "The operational ASPI sequence has already failed.");
            }
        }

        private long ObserveTime()
        {
            long now = monotonicMilliseconds();
            if (now < 0 ||
                (hasObservedTime && now < lastObservedMilliseconds))
            {
                throw new InvalidOperationException(
                    "The operational ASPI sequence clock is not monotonic.");
            }
            hasObservedTime = true;
            lastObservedMilliseconds = now;
            return now;
        }

        private void SetFailed()
        {
            completionPending = false;
            livePreviewImageReadPending = false;
            pendingLength = 0;
            pendingPostWindowScannerReadyAttempt = 0;
            pendingLivePreviewScannerReadyPoll = 0;
            pendingLiveFullScanInitialScannerReadyAttempt = 0;
            pendingPostWindowScannerReadyPhaseElapsedMilliseconds = 0;
            pendingLiveFullScanInitialScannerReadyElapsedMilliseconds = 0;
            state = AspiOperationalSequenceState.Failed;
        }

        private static bool Matches(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }
            int difference = 0;
            for (int index = 0; index < left.Length; ++index)
            {
                difference |= left[index] ^ right[index];
            }
            return difference == 0;
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
