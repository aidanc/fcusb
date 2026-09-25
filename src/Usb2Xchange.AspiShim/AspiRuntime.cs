// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using Usb2Xchange.Protocol;
using Usb2Xchange.WinUsb;

namespace Usb2Xchange.AspiShim
{
    internal sealed class AspiRuntimeContext
    {
        internal AspiRuntimeContext(string mode, byte adapterCount,
            IAspiReadOnlyTransport transport, bool loaderDataOutEnabled,
            bool operationalReplayEnabled,
            bool predictedPreviewObservationEnabled,
            bool previewSetWindowEnabled,
            string previewSetWindowSha256,
            bool previewImageReadEnabled)
            : this(mode, adapterCount, transport, loaderDataOutEnabled,
                operationalReplayEnabled,
                predictedPreviewObservationEnabled,
                previewSetWindowEnabled, previewSetWindowSha256,
                previewImageReadEnabled, false)
        {
        }

        internal AspiRuntimeContext(string mode, byte adapterCount,
            IAspiReadOnlyTransport transport, bool loaderDataOutEnabled,
            bool operationalReplayEnabled,
            bool predictedPreviewObservationEnabled,
            bool previewSetWindowEnabled,
            string previewSetWindowSha256,
            bool previewImageReadEnabled,
            bool startupWriteFingerprintEnabled)
        {
            Mode = mode;
            AdapterCount = adapterCount;
            Transport = transport;
            LoaderDataOutEnabled = loaderDataOutEnabled;
            OperationalReplayEnabled = operationalReplayEnabled;
            PredictedPreviewObservationEnabled =
                predictedPreviewObservationEnabled;
            PreviewSetWindowEnabled = previewSetWindowEnabled;
            PreviewSetWindowSha256 = previewSetWindowSha256;
            PreviewImageReadEnabled = previewImageReadEnabled;
            StartupWriteFingerprintEnabled =
                startupWriteFingerprintEnabled;
        }

        internal string Mode { get; private set; }
        internal byte AdapterCount { get; private set; }
        internal IAspiReadOnlyTransport Transport { get; private set; }
        internal bool LoaderDataOutEnabled { get; private set; }
        internal bool OperationalReplayEnabled { get; private set; }
        internal bool PredictedPreviewObservationEnabled { get; private set; }
        internal bool PreviewSetWindowEnabled { get; private set; }
        internal string PreviewSetWindowSha256 { get; private set; }
        internal bool PreviewImageReadEnabled { get; private set; }
        internal bool StartupWriteFingerprintEnabled { get; private set; }
    }

    internal static class AspiRuntime
    {
        internal const string TransportEnvironmentVariable =
            "USB2XCHANGE_ASPI_TRANSPORT";
        internal const string OfflineReplayMode = "offline-replay";
        internal const string OfflineOperatorReplayMode =
            "offline-operator-replay";
        internal const string OfflineCancelReplayMode =
            "offline-replay-cancel";
        internal const string LiveReadOnlyMode = "live-read-only";
        internal const string LiveLoaderMode = "live-loader";
        internal const string LivePreviewFingerprintMode =
            "live-preview-fingerprint";
        internal const string LivePreviewObserveMode = "live-preview-observe";
        internal const string LivePreview24x36SetWindowObserveMode =
            "live-preview-set-window-24x36-observe";
        internal const string LivePreview24x36FirstReadMode =
            "live-preview-first-read-24x36";
        internal const string LivePreview4x5FirstReadMode =
            "live-preview-first-read-4x5";
        internal const string LivePreviewFirstReadMode =
            "live-preview-first-read";
        internal const string LivePreviewBurstMode =
            "live-preview-burst-8";
        internal const string LivePreviewStreamMode =
            "live-preview-stream-256";
        internal const string LivePreviewShortRetryMode =
            "live-preview-stream-256-short-retry";
        internal const string LivePreviewNaturalMode =
            "live-preview-natural-996-short-retry";
        internal const string LivePreviewNaturalPerRowMode =
            "live-preview-natural-996-per-row-8";
        internal const string LivePreviewPoweredNaturalMode =
            "live-preview-powered-natural-911-observe";
        internal const string LivePreviewPoweredCleanupMode =
            "live-preview-powered-natural-911-cleanup-2";
        internal const string LivePreviewPoweredCancellationMode =
            "live-preview-powered-cancel-32-96-cleanup-2";
        internal const string LiveFullScanStartupFingerprintMode =
            "live-full-scan-startup-fingerprint";
        internal const string LiveFullScanSetWindowFirstReadMode =
            "live-full-scan-set-window-first-read";
        internal const string LiveFullScanCompleteMode =
            "live-full-scan-complete-60x60";
        internal const string LiveFullScanTerminalProbeMode =
            "live-full-scan-terminal-probe-60x60";
        internal const string LiveFullScanNaturalCompleteMode =
            "live-full-scan-natural-996-complete-60x60";
        internal const string LiveFullScanRow997CompleteMode =
            "live-full-scan-row997-complete-60x60";
        internal const string LiveFullScanProgressCompleteMode =
            "live-full-scan-progress-complete-60x60";
        internal const string LiveOperatorSessionMode =
            "live-operator-session-60x60";
        internal const string TrustedFlexColorPassThroughMode =
            "trusted-flexcolor-pass-through";
        internal const string LoaderApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_LOADER_APPROVAL";
        internal const string ReplayIdentifierEnvironmentVariable =
            "USB2XCHANGE_ASPI_REPLAY_IDENTIFIER";
        internal const string OfflineOperatorApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_OFFLINE_OPERATOR_APPROVAL";
        internal const string OfflineOperatorApprovalToken =
            "I-APPROVE-OFFLINE-TWO-COMPLETE-PREVIEW-SCAN-SAVE-" +
            "TRANSACTIONS";
        internal const string OfflineCancelApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_OFFLINE_CANCEL_APPROVAL";
        internal const string OfflineCancelApprovalToken =
            "I-APPROVE-OFFLINE-PREVIEW-CANCEL-STOP-THEN-HEALTHY-REPEAT";
        internal const string PreviewApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_APPROVAL";
        internal const string Preview24x36SetWindowApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_24X36_SET_WINDOW_APPROVAL";
        internal const string Preview24x36FirstReadApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_24X36_FIRST_READ_APPROVAL";
        internal const string Preview4x5FirstReadApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_4X5_FIRST_READ_APPROVAL";
        internal const string PreviewFingerprintApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_FINGERPRINT_APPROVAL";
        internal const string PreviewFirstReadApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_FIRST_READ_APPROVAL";
        internal const string PreviewBurstApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_BURST_APPROVAL";
        internal const string PreviewStreamApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_STREAM_APPROVAL";
        internal const string PreviewShortRetryApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_SHORT_RETRY_APPROVAL";
        internal const string PreviewNaturalApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_NATURAL_APPROVAL";
        internal const string
            PreviewNaturalPerRowApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_PREVIEW_NATURAL_PER_ROW_APPROVAL";
        internal const string
            PreviewPoweredNaturalApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_PREVIEW_POWERED_NATURAL_APPROVAL";
        internal const string
            PreviewPoweredCleanupApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_PREVIEW_POWERED_CLEANUP_APPROVAL";
        internal const string
            PreviewPoweredCancellationApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_PREVIEW_POWERED_CANCEL_APPROVAL";
        internal const string
            FullScanStartupFingerprintApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_FULL_SCAN_STARTUP_FINGERPRINT_APPROVAL";
        internal const string
            FullScanSetWindowApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_FULL_SCAN_SET_WINDOW_APPROVAL";
        internal const string FullScanCompleteApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_FULL_SCAN_COMPLETE_APPROVAL";
        internal const string
            FullScanTerminalProbeApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_FULL_SCAN_TERMINAL_PROBE_APPROVAL";
        internal const string
            FullScanNaturalCompleteApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_FULL_SCAN_NATURAL_COMPLETE_APPROVAL";
        internal const string
            FullScanRow997CompleteApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_FULL_SCAN_ROW997_COMPLETE_APPROVAL";
        internal const string
            FullScanProgressCompleteApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_FULL_SCAN_PROGRESS_COMPLETE_APPROVAL";
        internal const string
            OperatorSessionApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_OPERATOR_SESSION_APPROVAL";
        internal const string
            TrustedFlexColorApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_TRUSTED_FLEXCOLOR_APPROVAL";
        internal const string LoaderApprovalToken =
            "I-APPROVE-PRECISION2-COMPLETE-LOADER-SEQUENCE-" +
            "D8D71885-7C3D21A4";
        internal const string PreviewApprovalToken =
            "I-APPROVE-PRECISION2-ONE-SET-WINDOW-THEN-BLOCK-FIRST-READ-" +
            "96EB9049";
        internal const string Preview24x36SetWindowApprovalToken =
            "I-APPROVE-PRECISION2-24X36-ONE-SET-WINDOW-" +
            "78D93BC3-THEN-BLOCK-FIRST-IMAGE-READ";
        internal const string Preview24x36FirstReadApprovalToken =
            "I-APPROVE-PRECISION2-24X36-ONE-SET-WINDOW-ONE-3996-BYTE-" +
            "READ-78D93BC3-280F9C";
        internal const string Preview4x5FirstReadApprovalToken =
            "I-APPROVE-PRECISION2-4X5-ONE-SET-WINDOW-ONE-3996-BYTE-" +
            "READ-A44969F9-280F9C";
        internal const string PreviewFingerprintApprovalToken =
            "I-APPROVE-PRECISION2-FINGERPRINT-ONLY-NO-SET-WINDOW-USB";
        internal const string PreviewFirstReadApprovalToken =
            "I-APPROVE-PRECISION2-ONE-SET-WINDOW-ONE-3996-BYTE-READ-" +
            "96EB9049-280F9C";
        internal const string PreviewBurstApprovalToken =
            "I-APPROVE-PRECISION2-ONE-SET-WINDOW-EIGHT-3996-BYTE-READS-" +
            "96EB9049-280F9C-BURST8";
        internal const string PreviewStreamApprovalToken =
            "I-APPROVE-PRECISION2-ONE-SET-WINDOW-256-3996-BYTE-READS-" +
            "BOUNDED-IN-STREAM-DF-96EB9049-280F9C-STREAM256";
        internal const string PreviewShortRetryApprovalToken =
            "I-APPROVE-PRECISION2-STREAM256-EXACT-SHORT10-ASPI-BUSY-" +
            "MAX64-TOTAL-MAX8-CONSECUTIVE-96EB9049-280F9C";
        internal const string PreviewNaturalApprovalToken =
            "I-APPROVE-PRECISION2-NATURAL996-EXACT-SHORT10-ASPI-BUSY-" +
            "MAX512-TOTAL-MAX8-CONSECUTIVE-MAX1508-SUBMISSIONS-" +
            "96EB9049-280F9C-NO-CLEANUP";
        internal const string PreviewNaturalPerRowApprovalToken =
            "I-APPROVE-PRECISION2-NATURAL996-EXACT-SHORT10-ASPI-BUSY-" +
            "MAX7968-TOTAL-MAX8-PER-ROW-MAX8964-SUBMISSIONS-" +
            "96EB9049-280F9C-NO-CLEANUP";
        internal const string PreviewPoweredNaturalApprovalToken =
            "I-APPROVE-PRECISION2-POWERED-NATURAL911-EXACT-SHORT10-" +
            "ASPI-BUSY-MAX7288-TOTAL-MAX32-PER-ROW-BACKOFF50MS-" +
            "MAX8199-SUBMISSIONS-96EB9049-280F9C-NO-CLEANUP";
        internal const string PreviewPoweredCleanupApprovalToken =
            "I-APPROVE-PRECISION2-POWERED-NATURAL911-EXACT-SHORT10-" +
            "ASPI-BUSY-MAX7288-TOTAL-MAX32-PER-ROW-BACKOFF50MS-" +
            "MAX8199-SUBMISSIONS-EXACT-CLEANUP2-3092FA15";
        internal const string PreviewPoweredCancellationApprovalToken =
            "I-APPROVE-PRECISION2-POWERED-CANCEL-ROWS32-96-" +
            "EXACT-SHORT10-ASPI-BUSY-MAX32-PER-ROW-BACKOFF50MS-" +
            "EXACT-CLEANUP2-3092FA15";
        internal const string FullScanStartupFingerprintApprovalToken =
            "I-APPROVE-PRECISION2-POWERED-PREVIEW-CLEANUP2-THEN-" +
            "BACKOFF50MS-MAX32-READONLY-FULL-SCAN-INIT-AND-" +
            "BLOCK-FIRST-WRITE-OR-WINDOW";
        internal const string FullScanSetWindowApprovalToken =
            "I-APPROVE-PRECISION2-BACKOFF50MS-MAX32-FULL-SCAN-" +
            "SET-WINDOW-210F3499-THEN-BLOCK-FIRST-IMAGE-READ";
        internal const string FullScanCompleteApprovalToken =
            "I-APPROVE-PRECISION2-BACKOFF50MS-MAX32-FULL-SCAN-" +
            "762X4494-MAX1800000MS-CLEANUP-23B3C62B";
        internal const string FullScanTerminalProbeApprovalToken =
            "I-APPROVE-PRECISION2-FULL-SCAN-762X4494-" +
            "MAX1800000MS-ONE-" +
            "QUARANTINED-ROW763-TERMINAL-PROBE";
        internal const string FullScanNaturalCompleteApprovalToken =
            "I-APPROVE-PRECISION2-FULL-SCAN-NATURAL996X4494-" +
            "MAX7968-SHORT-MAX8964-SUBMISSIONS-MAX1800000MS-" +
            "CLEANUP2-23B3C62B";
        internal const string FullScanRow997CompleteApprovalToken =
            "I-APPROVE-PRECISION2-FULL-SCAN-ROW997X4494-" +
            "MAX7976-SHORT-MAX8973-SUBMISSIONS-MAX1800000MS-" +
            "BLOCK-ROW998-CLEANUP2-23B3C62B";
        internal const string FullScanProgressCompleteApprovalToken =
            "I-APPROVE-PRECISION2-FULL-SCAN-EXACT998X4494-" +
            "MAX7984-SHORT-MAX8982-SUBMISSIONS-MAX1800000MS-" +
            "PROGRESS-OBSERVED-CLEANUP2-23B3C62B";
        internal const string OperatorSessionApprovalToken =
            "I-APPROVE-PRECISION2-60X60-COLD-THEN-WARM-OPERATOR-" +
            "SESSION-EXACT998X4494-CLEANUP2";
        internal const string TrustedFlexColorApprovalToken =
            "I-APPROVE-EXACT-FLEXCOLOR-4.0.3-TRANSPARENT-SCSI-" +
            "PASS-THROUGH-TARGET5-LUN0-16MIB-120S";

        internal static AspiRuntimeContext Create(IUsbLog log)
        {
            return Create(log, null);
        }

        internal static AspiRuntimeContext Create(IUsbLog log,
            Func<bool> trustedProcessOverride)
        {
            string mode = Environment.GetEnvironmentVariable(
                TransportEnvironmentVariable);
            if (string.Equals(mode, TrustedFlexColorPassThroughMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    TrustedFlexColorApprovalEnvironmentVariable);
                bool trustedProcess = trustedProcessOverride == null
                    ? TrustedFlexColorProcessGate.ValidateCurrentProcess(log)
                    : trustedProcessOverride();
                if (string.Equals(approval,
                        TrustedFlexColorApprovalToken,
                        StringComparison.Ordinal) && trustedProcess)
                {
                    log.Warning("ASPI transport mode is trusted-flexcolor-" +
                        "pass-through. Exact English FlexColor 4.0.3 may " +
                        "submit structurally valid target-5/LUN-0 SCSI " +
                        "commands without scanner semantic filtering; " +
                        "transfer data remains redacted from ASPI logs.");
                    return new AspiRuntimeContext(mode, 1,
                        new TransparentWinUsbAspiTransport(log), false,
                        false, false, false,
                        PrecisionTwoPreviewCommandManifest.SetWindowSha256,
                        false);
                }
                log.Warning("ASPI trusted-flexcolor-pass-through mode was " +
                    "requested without its exact approval token and exact " +
                    "process/private-tree identity; transport remains " +
                    "disabled.");
            }
            if (string.Equals(mode, OfflineOperatorReplayMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    OfflineOperatorApprovalEnvironmentVariable);
                if (string.Equals(approval, OfflineOperatorApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Info("ASPI transport mode is " +
                        "offline-operator-replay; WinUSB is disabled and " +
                        "each exact completed Preview/Scan cycle receives " +
                        "a fresh bounded synthetic transport.");
                    string replayIdentifier =
                        Environment.GetEnvironmentVariable(
                            ReplayIdentifierEnvironmentVariable);
                    bool firstCycle = true;
                    return new AspiRuntimeContext(mode, 1,
                        new AspiOperatorSessionTransport(delegate
                        {
                            bool warmCycle = !firstCycle;
                            firstCycle = false;
                            return new OfflineReplayAspiTransport(log,
                                PrecisionTwoPreviewCommandManifest.
                                    SetWindowSha256,
                                replayIdentifier, 0, warmCycle);
                        }), false, true, true, false,
                        PrecisionTwoPreviewCommandManifest.SetWindowSha256,
                        false);
                }
                log.Warning("ASPI offline-operator-replay mode was " +
                    "requested without its exact separate approval token; " +
                    "transport remains disabled.");
            }
            if (string.Equals(mode, OfflineReplayMode,
                    StringComparison.Ordinal))
            {
                log.Info("ASPI transport mode is offline-replay; WinUSB is disabled.");
                string replayIdentifier = Environment.GetEnvironmentVariable(
                    ReplayIdentifierEnvironmentVariable);
                return new AspiRuntimeContext(mode, 1,
                    new OfflineReplayAspiTransport(log,
                        PrecisionTwoPreviewCommandManifest.SetWindowSha256,
                        replayIdentifier), false, true, true, false,
                    PrecisionTwoPreviewCommandManifest.SetWindowSha256,
                    false);
            }
            if (string.Equals(mode, OfflineCancelReplayMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    OfflineCancelApprovalEnvironmentVariable);
                if (string.Equals(approval, OfflineCancelApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Info("ASPI transport mode is offline-replay-cancel; " +
                        "WinUSB is disabled and each synthetic image row is " +
                        "delayed 20 ms for exact Stop-button observation.");
                    string replayIdentifier =
                        Environment.GetEnvironmentVariable(
                            ReplayIdentifierEnvironmentVariable);
                    return new AspiRuntimeContext(mode, 1,
                        new OfflineReplayAspiTransport(log,
                            PrecisionTwoPreviewCommandManifest.SetWindowSha256,
                            replayIdentifier, 20), false, true, true, false,
                        PrecisionTwoPreviewCommandManifest.SetWindowSha256,
                        false);
                }
                log.Warning("ASPI offline-replay-cancel mode was requested " +
                    "without its exact separate approval token; transport " +
                    "remains disabled.");
            }
            if (string.Equals(mode, LiveReadOnlyMode,
                    StringComparison.Ordinal))
            {
                log.Warning("ASPI transport mode is live-read-only; read-only " +
                    "USB2Xchange access is explicitly enabled.");
                return new AspiRuntimeContext(mode, 1,
                    new WinUsbAspiTransport(log), false, false, false,
                    false, PrecisionTwoPreviewCommandManifest.SetWindowSha256,
                    false);
            }
            if (string.Equals(mode, LiveOperatorSessionMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    OperatorSessionApprovalEnvironmentVariable);
                if (string.Equals(approval, OperatorSessionApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-operator-session-60x60; the first exact " +
                        "Preview/Scan cycle is cold and every later cycle " +
                        "requires the bounded warm INQUIRY/DF prefix. " +
                        "Cycle rotation requires the hardware-proven " +
                        "998-row full Scan and two full-scan cleanups; " +
                        "cancellation is not enabled in this acceptance " +
                        "mode.");
                    return new AspiRuntimeContext(mode, 1,
                        WinUsbAspiTransport.CreateFullScanRepeatSession(log),
                        false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true, true);
                }
                log.Warning("ASPI live-operator-session-60x60 mode was " +
                    "requested without its exact separate approval token; " +
                    "transport remains disabled.");
            }
            if (string.Equals(mode, LiveLoaderMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    LoaderApprovalEnvironmentVariable);
                if (string.Equals(approval, LoaderApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is live-loader; the exact " +
                        "Precision II loader sequence is explicitly enabled.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, true), true, false,
                        false, false,
                        PrecisionTwoPreviewCommandManifest.SetWindowSha256,
                        false);
                }
                log.Warning("ASPI live-loader mode was requested without its " +
                    "exact separate approval token; transport remains disabled.");
            }
            if (string.Equals(mode, LivePreviewObserveMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    PreviewApprovalEnvironmentVariable);
                if (string.Equals(approval, PreviewApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is live-preview-observe; " +
                        "one exact Preview SET WINDOW and its two read-only " +
                        "successor polls are explicitly enabled. The first " +
                        "image READ remains blocked before USB.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256),
                        false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, false);
                }
                log.Warning("ASPI live-preview-observe mode was requested " +
                    "without its exact separate approval token; transport " +
                    "remains disabled.");
            }
            if (string.Equals(mode, LivePreview24x36SetWindowObserveMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    Preview24x36SetWindowApprovalEnvironmentVariable);
                if (string.Equals(approval,
                        Preview24x36SetWindowApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-preview-set-window-24x36-observe; one exact " +
                        "powered 24x36 Preview SET WINDOW and its two " +
                        "bounded read-only readiness phases are enabled. " +
                        "The first image READ remains blocked before USB.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLive24x36SetWindowSha256),
                        false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLive24x36SetWindowSha256, false);
                }
                log.Warning("ASPI 24x36 SET WINDOW observation mode was " +
                    "requested without its exact separate approval token; " +
                    "transport remains disabled.");
            }
            if (string.Equals(mode, LivePreview24x36FirstReadMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    Preview24x36FirstReadApprovalEnvironmentVariable);
                if (string.Equals(approval,
                        Preview24x36FirstReadApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-preview-first-read-24x36; one exact powered " +
                        "24x36 SET WINDOW, its bounded readiness phases, " +
                        "and one exact 3,996-byte selector-28 image READ " +
                        "are enabled. All later commands remain blocked.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLive24x36SetWindowSha256, true),
                        false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLive24x36SetWindowSha256, true);
                }
                log.Warning("ASPI 24x36 first-image-read mode was requested " +
                    "without its exact separate approval token; transport " +
                    "remains disabled.");
            }
            if (string.Equals(mode, LivePreview4x5FirstReadMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    Preview4x5FirstReadApprovalEnvironmentVariable);
                if (string.Equals(approval,
                        Preview4x5FirstReadApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-preview-first-read-4x5; one exact powered " +
                        "4x5 SET WINDOW, its bounded readiness phases, and " +
                        "one exact 3,996-byte selector-28 image READ are " +
                        "enabled. All later commands remain blocked.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLive4x5SetWindowSha256, true),
                        false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLive4x5SetWindowSha256, true);
                }
                log.Warning("ASPI 4x5 first-image-read mode was requested " +
                    "without its exact separate approval token; transport " +
                    "remains disabled.");
            }
            if (string.Equals(mode, LivePreviewFirstReadMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    PreviewFirstReadApprovalEnvironmentVariable);
                if (string.Equals(approval, PreviewFirstReadApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-preview-first-read; one exact SET WINDOW, its " +
                        "bounded readiness phases, and one exact 3,996-byte " +
                        "selector-28 image READ are explicitly enabled. All " +
                        "later image and cleanup commands remain blocked.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256, true),
                        false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true);
                }
                log.Warning("ASPI live-preview-first-read mode was requested " +
                    "without its exact separate approval token; transport " +
                    "remains disabled.");
            }
            if (string.Equals(mode, LivePreviewBurstMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    PreviewBurstApprovalEnvironmentVariable);
                if (string.Equals(approval, PreviewBurstApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is live-preview-burst-8; " +
                        "one exact SET WINDOW, its bounded readiness phases, " +
                        "and exactly eight ordered 3,996-byte selector-28 " +
                        "image READs are enabled. Row 9 and all cleanup " +
                        "commands remain blocked.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewBurstRows),
                        false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true);
                }
                log.Warning("ASPI live-preview-burst-8 mode was requested " +
                    "without its exact separate approval token; transport " +
                    "remains disabled.");
            }
            if (string.Equals(mode, LivePreviewStreamMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    PreviewStreamApprovalEnvironmentVariable);
                if (string.Equals(approval, PreviewStreamApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-preview-stream-256; one exact SET WINDOW, its " +
                        "bounded readiness phases, exactly 256 ordered " +
                        "3,996-byte selector-28 image READs, and at most 256 " +
                        "in-stream DF polls are enabled. Row 257 and all " +
                        "cleanup commands remain blocked.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewStreamRows),
                        false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true);
                }
                log.Warning("ASPI live-preview-stream-256 mode was requested " +
                    "without its exact separate approval token; transport " +
                    "remains disabled.");
            }
            if (string.Equals(mode, LivePreviewShortRetryMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    PreviewShortRetryApprovalEnvironmentVariable);
                if (string.Equals(approval, PreviewShortRetryApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-preview-stream-256-short-retry; one exact SET " +
                        "WINDOW, bounded readiness, 256 full ordered image " +
                        "rows, at most 64 exact 10-byte short completions, " +
                        "at most eight consecutive short completions, at " +
                        "most 320 image submissions, and bounded in-stream " +
                        "DF are enabled. Exact short data is never copied; " +
                        "it is exposed to FlexColor only as target BUSY. " +
                        "Row 257 and cleanup remain blocked.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewStreamRows, true),
                        false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true);
                }
                log.Warning("ASPI live-preview-stream-256-short-retry mode " +
                    "was requested without its exact separate approval " +
                    "token; transport remains disabled.");
            }
            if (string.Equals(mode, LivePreviewNaturalMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    PreviewNaturalApprovalEnvironmentVariable);
                if (string.Equals(approval, PreviewNaturalApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-preview-natural-996-short-retry; one exact " +
                        "SET WINDOW, bounded readiness, 996 full ordered " +
                        "image rows, at most 512 exact 10-byte short " +
                        "completions, at most eight consecutive shorts, at " +
                        "most 1,508 image submissions, at most 1,024 " +
                        "in-stream DF polls, and five minutes of streaming " +
                        "are enabled. Exact short data is never copied; it " +
                        "is exposed to FlexColor only as target BUSY. Row " +
                        "997 and cleanup remain blocked.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalRows, true),
                        false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true);
                }
                log.Warning("ASPI live-preview-natural-996-short-retry " +
                    "mode was requested without its exact separate approval " +
                    "token; transport remains disabled.");
            }
            if (string.Equals(mode, LivePreviewNaturalPerRowMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    PreviewNaturalPerRowApprovalEnvironmentVariable);
                if (string.Equals(approval,
                        PreviewNaturalPerRowApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-preview-natural-996-per-row-8; one exact SET " +
                        "WINDOW, bounded readiness, 996 full ordered image " +
                        "rows, at most 7,968 exact 10-byte short " +
                        "completions, at most eight consecutive shorts for " +
                        "one pending row, at most 8,964 image submissions, " +
                        "at most 1,024 in-stream DF polls, and five minutes " +
                        "of streaming are enabled. Exact short data is " +
                        "never copied; it is exposed to FlexColor only as " +
                        "target BUSY. Row 997 and cleanup remain blocked.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalRows, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalPerRowMaximumReadSubmissions,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalPerRowMaximumShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumConsecutiveShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumScannerReadyPolls,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumMilliseconds),
                        false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true);
                }
                log.Warning("ASPI live-preview-natural-996-per-row-8 mode " +
                    "was requested without its exact separate approval " +
                    "token; transport remains disabled.");
            }
            if (string.Equals(mode, LivePreviewPoweredNaturalMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    PreviewPoweredNaturalApprovalEnvironmentVariable);
                if (string.Equals(approval,
                        PreviewPoweredNaturalApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-preview-powered-natural-911-observe; one " +
                        "exact SET WINDOW, bounded readiness, 911 full " +
                        "ordered image rows, at most 7,288 exact 10-byte " +
                        "short completions, at most 32 consecutive shorts " +
                        "for one pending row with 50 ms backoff, at most " +
                        "8,199 image " +
                        "submissions, at most 1,024 in-stream DF polls, and " +
                        "five minutes of streaming are enabled. Exact short " +
                        "data is never copied. Row 912 and cleanup remain " +
                        "blocked before USB.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalRows, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredMaximumConsecutiveShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumScannerReadyPolls,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumMilliseconds),
                        false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true);
                }
                log.Warning("ASPI live-preview-powered-natural-911-observe " +
                    "mode was requested without its exact separate approval " +
                    "token; transport remains disabled.");
            }
            if (string.Equals(mode, LivePreviewPoweredCleanupMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    PreviewPoweredCleanupApprovalEnvironmentVariable);
                if (string.Equals(approval,
                        PreviewPoweredCleanupApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-preview-powered-natural-911-cleanup-2; one " +
                        "exact initial SET WINDOW, bounded readiness, 911 " +
                        "full ordered image rows, at most 7,288 exact " +
                        "10-byte short completions, at most 32 consecutive " +
                        "shorts for one pending row with 50 ms backoff, at " +
                        "most " +
                        "8,199 image submissions, at most 1,024 in-stream " +
                        "DF polls, five minutes of streaming, and exactly " +
                        "two hash-pinned post-image cleanup SET WINDOW " +
                        "commands are enabled. Exact short data is never " +
                        "copied. Row 912, a third cleanup, and every other " +
                        "successor remain blocked before USB.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalRows, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredMaximumConsecutiveShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumScannerReadyPolls,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumMilliseconds,
                            true), false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true);
                }
                log.Warning("ASPI live-preview-powered-natural-911-" +
                    "cleanup-2 mode was requested without its exact " +
                    "separate approval token; transport remains disabled.");
            }
            if (string.Equals(mode, LivePreviewPoweredCancellationMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    PreviewPoweredCancellationApprovalEnvironmentVariable);
                if (string.Equals(approval,
                        PreviewPoweredCancellationApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-preview-powered-cancel-32-96-cleanup-2; one " +
                        "exact initial SET WINDOW, bounded readiness, 32 to " +
                        "96 full ordered image rows, bounded exact 10-byte " +
                        "short completions, and exactly two hash-pinned " +
                        "cancellation cleanup SET WINDOW commands are " +
                        "enabled. Exact short data is never copied. Row 97, " +
                        "normal-completion cleanup, a third cleanup, and " +
                        "every other successor remain blocked before USB.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalRows, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredMaximumConsecutiveShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumScannerReadyPolls,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumMilliseconds,
                            false, true), false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true);
                }
                log.Warning("ASPI live-preview-powered-cancel-32-96-" +
                    "cleanup-2 mode was requested without its exact " +
                    "separate approval token; transport remains disabled.");
            }
            if (string.Equals(mode, LiveFullScanStartupFingerprintMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    FullScanStartupFingerprintApprovalEnvironmentVariable);
                if (string.Equals(approval,
                        FullScanStartupFingerprintApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-full-scan-startup-fingerprint; one complete " +
                        "powered Preview and its two exact cleanup windows " +
                        "are enabled. One operational INQUIRY and bounded " +
                        "read-only repeat initialization may follow. The " +
                        "first startup WRITE BUFFER or second SET WINDOW is " +
                        "hashed and blocked before USB unconditionally.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalRows, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredMaximumConsecutiveShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumScannerReadyPolls,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumMilliseconds,
                            true, false, true), false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true, true);
                }
                log.Warning("ASPI live-full-scan-startup-fingerprint mode " +
                    "was requested without its exact separate approval " +
                    "token; transport remains disabled.");
            }
            if (string.Equals(mode, LiveFullScanSetWindowFirstReadMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    FullScanSetWindowApprovalEnvironmentVariable);
                if (string.Equals(approval,
                        FullScanSetWindowApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-full-scan-set-window-first-read; one complete " +
                        "powered Preview, cleanup pair, operational INQUIRY, " +
                        "bounded repeat initialization, and the exact " +
                        "hash-pinned full-scan SET WINDOW are enabled. Its " +
                        "two readiness phases may follow; the first image " +
                        "READ is fingerprinted and blocked before USB.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalRows, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredMaximumConsecutiveShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumScannerReadyPolls,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumMilliseconds,
                            true, false, true, true), false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true, true);
                }
                log.Warning("ASPI live-full-scan-set-window-first-read mode " +
                    "was requested without its exact separate approval " +
                    "token; transport remains disabled.");
            }
            if (string.Equals(mode, LiveFullScanCompleteMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    FullScanCompleteApprovalEnvironmentVariable);
                if (string.Equals(approval, FullScanCompleteApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-full-scan-complete-60x60; the exact proven " +
                        "Preview, repeat initialization, hash-pinned " +
                        "full-scan SET WINDOW, 762-row/4,494-byte bounded " +
                        "short-retry stream, readiness polls, a 30-minute " +
                        "stream deadline, and two hash-pinned cleanup " +
                        "windows are enabled.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalRows, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredMaximumConsecutiveShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumScannerReadyPolls,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumMilliseconds,
                            true, false, true, true, true), false, false, true,
                        true, PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true, true);
                }
                log.Warning("ASPI live-full-scan-complete-60x60 mode was " +
                    "requested without its exact separate approval token; " +
                    "transport remains disabled.");
            }
            if (string.Equals(mode, LiveFullScanTerminalProbeMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    FullScanTerminalProbeApprovalEnvironmentVariable);
                if (string.Equals(approval,
                        FullScanTerminalProbeApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-full-scan-terminal-probe-60x60; the exact " +
                        "proven Preview and 762-row full-scan prefix plus " +
                        "one quarantined row-763 READ are enabled under a " +
                        "30-minute stream deadline. The probe result is " +
                        "logged as metadata/hash only, is never returned to " +
                        "FlexColor, and closes all transport authority.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalRows, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredMaximumConsecutiveShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumScannerReadyPolls,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumMilliseconds,
                            true, false, true, true, true, true), false,
                        false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true, true);
                }
                log.Warning("ASPI live-full-scan-terminal-probe-60x60 mode " +
                    "was requested without its exact separate approval " +
                    "token; transport remains disabled.");
            }
            if (string.Equals(mode, LiveFullScanNaturalCompleteMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    FullScanNaturalCompleteApprovalEnvironmentVariable);
                if (string.Equals(approval,
                        FullScanNaturalCompleteApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-full-scan-natural-996-complete-60x60; the " +
                        "exact proven Preview/repeat/window prefix, 996 " +
                        "full-scan rows, bounded short/DF/INQUIRY handling, " +
                        "30-minute stream deadline, and two hash-pinned " +
                        "cleanup windows are enabled.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalRows, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredMaximumConsecutiveShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumScannerReadyPolls,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumMilliseconds,
                            true, false, true, true, true, false, true),
                        false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true, true);
                }
                log.Warning("ASPI live-full-scan-natural-996-complete-" +
                    "60x60 mode was requested without its exact separate " +
                    "approval token; transport remains disabled.");
            }
            if (string.Equals(mode, LiveFullScanRow997CompleteMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    FullScanRow997CompleteApprovalEnvironmentVariable);
                if (string.Equals(approval,
                        FullScanRow997CompleteApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-full-scan-row997-complete-60x60; the exact " +
                        "proven Preview/repeat/window prefix, 997 full-scan " +
                        "rows, bounded short/DF/INQUIRY handling, 30-minute " +
                        "stream deadline, and two hash-pinned cleanup " +
                        "windows are enabled. Row 998 remains blocked.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalRows, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredMaximumConsecutiveShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumScannerReadyPolls,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumMilliseconds,
                            true, false, true, true, true, false, false,
                            true), false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true, true);
                }
                log.Warning("ASPI live-full-scan-row997-complete-60x60 " +
                    "mode was requested without its exact separate approval " +
                    "token; transport remains disabled.");
            }
            if (string.Equals(mode, LiveFullScanProgressCompleteMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    FullScanProgressCompleteApprovalEnvironmentVariable);
                if (string.Equals(approval,
                        FullScanProgressCompleteApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-full-scan-progress-complete-60x60; FlexColor " +
                        "may repeat only its exact 4,494-byte image READ " +
                        "through the hardware-proven 998-request natural " +
                        "boundary and known cleanup pair. " +
                        "Short/DF/INQUIRY handling, a 30-minute deadline, " +
                        "and both cleanup payloads remain independently " +
                        "bounded and hash-pinned.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalRows, true,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredNaturalMaximumShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewPoweredMaximumConsecutiveShortRetries,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumScannerReadyPolls,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewNaturalMaximumMilliseconds,
                            true, false, true, true, true, false, false,
                            false, true), false, false, true, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, true, true);
                }
                log.Warning("ASPI live-full-scan-progress-complete-60x60 " +
                    "mode was requested without its exact separate " +
                    "approval token; transport remains disabled.");
            }
            if (string.Equals(mode, LivePreviewFingerprintMode,
                    StringComparison.Ordinal))
            {
                string approval = Environment.GetEnvironmentVariable(
                    PreviewFingerprintApprovalEnvironmentVariable);
                if (string.Equals(approval, PreviewFingerprintApprovalToken,
                        StringComparison.Ordinal))
                {
                    log.Warning("ASPI transport mode is " +
                        "live-preview-fingerprint; operational " +
                        "initialization is enabled, but SET WINDOW is hashed " +
                        "and blocked before USB unconditionally.");
                    return new AspiRuntimeContext(mode, 1,
                        new WinUsbAspiTransport(log, false, true, false,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveSetWindowSha256),
                        false, false, true, false,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveSetWindowSha256, false);
                }
                log.Warning("ASPI live-preview-fingerprint mode was requested " +
                    "without its exact separate approval token; transport " +
                    "remains disabled.");
            }

            string displayed = string.IsNullOrEmpty(mode) ? "<unset>" : mode;
            log.Warning("ASPI transport disabled because " +
                TransportEnvironmentVariable + " is " + displayed + ".");
            return new AspiRuntimeContext("disabled", 0,
                new DisabledAspiTransport(), false, false, false, false,
                PrecisionTwoPreviewCommandManifest.SetWindowSha256, false);
        }
    }

    internal sealed class DisabledAspiTransport : IAspiReadOnlyTransport
    {
        public AspiTransportResult Execute(byte target, byte lun, byte[] cdb,
            uint requestedLength)
        {
            throw new InvalidOperationException(
                "ASPI transport is disabled. Select an explicit transport mode.");
        }
    }
}
