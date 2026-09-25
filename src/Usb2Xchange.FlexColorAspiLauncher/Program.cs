// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Usb2Xchange.FlexColorPatch;

namespace Usb2Xchange.FlexColorAspiLauncher
{
    internal static class Program
    {
        private const string FlexColorExeSha256 =
            "4C5D402F3668F06BEAFC871B9F152D55BF5ACCA191C43082C6A8C5647916AF28";
        private const string ReplayApproval = "--approve-offline-replay";
        private const string SmokeApproval =
            "--approve-offline-replay-smoke";
        private const string OperationalReplaySmokeApproval =
            "--approve-offline-operational-replay-smoke";
        private const string FullScanReplaySmokeApproval =
            "--approve-offline-full-scan-replay-smoke";
        private const string OfflineOperatorRepeatSmokeApproval =
            "--approve-offline-operator-repeat-replay-smoke";
        private const string LiveOperatorRepeatSmokeApproval =
            "--approve-live-operator-repeat-60x60-smoke";
        private const string OfflineCancelReplaySmokeApproval =
            "--approve-offline-preview-cancel-replay-smoke";
        private const string OfflineFullScan60x60SetWindowSha256 =
            "E8F5D0217E816D7F17E43A17573A466AA863873B18D6BC0E4660D2FECF0B0485";
        private const string LivePreviewSetWindowSha256 =
            "96EB9049828909D06A5E8AB32861124D1FE59B67005D3BF0EBDA235584D0C6FB";
        private const string LivePreview24x36SetWindowSha256 =
            "78D93BC3FBA82976A49AA369301DFB065DD392CA381EB56CC504E79A643DAC63";
        private const string LivePreview4x5SetWindowSha256 =
            "A44969F96CACE039220A55E5FCC7F8EFB4EE054526039D806B9391C4D9097DAD";
        private const string LiveApproval = "--approve-live-read-only";
        private const string LiveSmokeApproval =
            "--approve-live-read-only-smoke";
        private const string TrustedPassThroughLaunchApproval =
            "--approve-trusted-flexcolor-pass-through";
        private const string LoaderLaunchApproval =
            "--approve-scanner-firmware-state-change";
        private const string LoaderApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_LOADER_APPROVAL";
        private const string PreviewApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_APPROVAL";
        private const string
            Preview24x36SetWindowApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_PREVIEW_24X36_SET_WINDOW_APPROVAL";
        private const string
            Preview24x36FirstReadApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_PREVIEW_24X36_FIRST_READ_APPROVAL";
        private const string Preview4x5FirstReadApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_4X5_FIRST_READ_APPROVAL";
        private const string PreviewFingerprintApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_FINGERPRINT_APPROVAL";
        private const string PreviewFirstReadApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_FIRST_READ_APPROVAL";
        private const string PreviewBurstApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_BURST_APPROVAL";
        private const string PreviewStreamApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_STREAM_APPROVAL";
        private const string PreviewShortRetryApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_SHORT_RETRY_APPROVAL";
        private const string PreviewNaturalApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_PREVIEW_NATURAL_APPROVAL";
        private const string
            PreviewNaturalPerRowApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_PREVIEW_NATURAL_PER_ROW_APPROVAL";
        private const string
            PreviewPoweredNaturalApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_PREVIEW_POWERED_NATURAL_APPROVAL";
        private const string
            PreviewPoweredCleanupApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_PREVIEW_POWERED_CLEANUP_APPROVAL";
        private const string
            PreviewPoweredCancellationApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_PREVIEW_POWERED_CANCEL_APPROVAL";
        private const string
            FullScanStartupFingerprintApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_FULL_SCAN_STARTUP_FINGERPRINT_APPROVAL";
        private const string
            FullScanSetWindowApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_FULL_SCAN_SET_WINDOW_APPROVAL";
        private const string FullScanCompleteApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_FULL_SCAN_COMPLETE_APPROVAL";
        private const string
            FullScanTerminalProbeApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_FULL_SCAN_TERMINAL_PROBE_APPROVAL";
        private const string
            FullScanNaturalCompleteApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_FULL_SCAN_NATURAL_COMPLETE_APPROVAL";
        private const string
            FullScanRow997CompleteApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_FULL_SCAN_ROW997_COMPLETE_APPROVAL";
        private const string
            FullScanProgressCompleteApprovalEnvironmentVariable =
                "USB2XCHANGE_ASPI_FULL_SCAN_PROGRESS_COMPLETE_APPROVAL";
        private const string ReplayIdentifierEnvironmentVariable =
            "USB2XCHANGE_ASPI_REPLAY_IDENTIFIER";
        private const string OfflineCancelApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_OFFLINE_CANCEL_APPROVAL";
        private const string OfflineOperatorApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_OFFLINE_OPERATOR_APPROVAL";
        private const string OperatorSessionApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_OPERATOR_SESSION_APPROVAL";
        private const string TrustedFlexColorApprovalEnvironmentVariable =
            "USB2XCHANGE_ASPI_TRUSTED_FLEXCOLOR_APPROVAL";
        private const string OfflineOperatorApprovalToken =
            "I-APPROVE-OFFLINE-TWO-COMPLETE-PREVIEW-SCAN-SAVE-" +
            "TRANSACTIONS";
        private const string OperatorSessionApprovalToken =
            "I-APPROVE-PRECISION2-60X60-COLD-THEN-WARM-OPERATOR-" +
            "SESSION-EXACT998X4494-CLEANUP2";
        private const string TrustedFlexColorApprovalToken =
            "I-APPROVE-EXACT-FLEXCOLOR-4.0.3-TRANSPARENT-SCSI-" +
            "PASS-THROUGH-TARGET5-LUN0-16MIB-120S";
        private const string OfflineCancelApprovalToken =
            "I-APPROVE-OFFLINE-PREVIEW-CANCEL-STOP-THEN-HEALTHY-REPEAT";
        private const string LoaderApprovalToken =
            "I-APPROVE-PRECISION2-COMPLETE-LOADER-SEQUENCE-" +
            "D8D71885-7C3D21A4";
        private const string PreviewLaunchApproval =
            "--approve-one-preview-set-window-observation";
        private const string PreviewSmokeApproval =
            "--approve-bounded-preview-observation-smoke";
        private const string Preview24x36SetWindowSmokeApproval =
            "--approve-bounded-24x36-preview-set-window-observation-smoke";
        private const string Preview24x36FirstReadSmokeApproval =
            "--approve-one-24x36-preview-first-image-read-smoke";
        private const string Preview4x5FirstReadSmokeApproval =
            "--approve-one-4x5-preview-first-image-read-smoke";
        private const string PreviewFingerprintSmokeApproval =
            "--approve-preview-fingerprint-only-smoke";
        private const string PreviewFirstReadSmokeApproval =
            "--approve-one-preview-first-image-read-smoke";
        private const string PreviewBurstSmokeApproval =
            "--approve-eight-preview-image-read-burst-smoke";
        private const string PreviewStreamSmokeApproval =
            "--approve-256-preview-image-read-stream-smoke";
        private const string PreviewShortRetrySmokeApproval =
            "--approve-256-preview-image-read-short-retry-smoke";
        private const string PreviewNaturalSmokeApproval =
            "--approve-natural-996-preview-image-read-smoke";
        private const string PreviewNaturalPerRowSmokeApproval =
            "--approve-natural-996-per-row-preview-image-read-smoke";
        private const string PreviewPoweredNaturalSmokeApproval =
            "--approve-powered-natural-911-preview-observation-smoke";
        private const string PreviewPoweredCleanupSmokeApproval =
            "--approve-powered-natural-911-cleanup-2-smoke";
        private const string PreviewPoweredCancellationSmokeApproval =
            "--approve-powered-preview-cancel-32-96-cleanup-2-smoke";
        private const string FullScanStartupFingerprintSmokeApproval =
            "--approve-full-scan-startup-fingerprint-smoke";
        private const string FullScanSetWindowSmokeApproval =
            "--approve-full-scan-set-window-first-read-smoke";
        private const string FullScanCompleteSmokeApproval =
            "--approve-full-scan-complete-60x60-smoke";
        private const string FullScanTerminalProbeSmokeApproval =
            "--approve-full-scan-terminal-probe-60x60-smoke";
        private const string FullScanNaturalCompleteSmokeApproval =
            "--approve-full-scan-natural-996-complete-60x60-smoke";
        private const string FullScanRow997CompleteSmokeApproval =
            "--approve-full-scan-row997-complete-60x60-smoke";
        private const string FullScanProgressCompleteSmokeApproval =
            "--approve-full-scan-progress-complete-60x60-smoke";
        private const string PreviewApprovalToken =
            "I-APPROVE-PRECISION2-ONE-SET-WINDOW-THEN-BLOCK-FIRST-READ-" +
            "96EB9049";
        private const string Preview24x36SetWindowApprovalToken =
            "I-APPROVE-PRECISION2-24X36-ONE-SET-WINDOW-" +
            "78D93BC3-THEN-BLOCK-FIRST-IMAGE-READ";
        private const string Preview24x36FirstReadApprovalToken =
            "I-APPROVE-PRECISION2-24X36-ONE-SET-WINDOW-ONE-3996-BYTE-" +
            "READ-78D93BC3-280F9C";
        private const string Preview4x5FirstReadApprovalToken =
            "I-APPROVE-PRECISION2-4X5-ONE-SET-WINDOW-ONE-3996-BYTE-" +
            "READ-A44969F9-280F9C";
        private const string PreviewFingerprintApprovalToken =
            "I-APPROVE-PRECISION2-FINGERPRINT-ONLY-NO-SET-WINDOW-USB";
        private const string PreviewFirstReadApprovalToken =
            "I-APPROVE-PRECISION2-ONE-SET-WINDOW-ONE-3996-BYTE-READ-" +
            "96EB9049-280F9C";
        private const string PreviewBurstApprovalToken =
            "I-APPROVE-PRECISION2-ONE-SET-WINDOW-EIGHT-3996-BYTE-READS-" +
            "96EB9049-280F9C-BURST8";
        private const string PreviewStreamApprovalToken =
            "I-APPROVE-PRECISION2-ONE-SET-WINDOW-256-3996-BYTE-READS-" +
            "BOUNDED-IN-STREAM-DF-96EB9049-280F9C-STREAM256";
        private const string PreviewShortRetryApprovalToken =
            "I-APPROVE-PRECISION2-STREAM256-EXACT-SHORT10-ASPI-BUSY-" +
            "MAX64-TOTAL-MAX8-CONSECUTIVE-96EB9049-280F9C";
        private const string PreviewNaturalApprovalToken =
            "I-APPROVE-PRECISION2-NATURAL996-EXACT-SHORT10-ASPI-BUSY-" +
            "MAX512-TOTAL-MAX8-CONSECUTIVE-MAX1508-SUBMISSIONS-" +
            "96EB9049-280F9C-NO-CLEANUP";
        private const string PreviewNaturalPerRowApprovalToken =
            "I-APPROVE-PRECISION2-NATURAL996-EXACT-SHORT10-ASPI-BUSY-" +
            "MAX7968-TOTAL-MAX8-PER-ROW-MAX8964-SUBMISSIONS-" +
            "96EB9049-280F9C-NO-CLEANUP";
        private const string PreviewPoweredNaturalApprovalToken =
            "I-APPROVE-PRECISION2-POWERED-NATURAL911-EXACT-SHORT10-" +
            "ASPI-BUSY-MAX7288-TOTAL-MAX32-PER-ROW-BACKOFF50MS-" +
            "MAX8199-SUBMISSIONS-96EB9049-280F9C-NO-CLEANUP";
        private const string PreviewPoweredCleanupApprovalToken =
            "I-APPROVE-PRECISION2-POWERED-NATURAL911-EXACT-SHORT10-" +
            "ASPI-BUSY-MAX7288-TOTAL-MAX32-PER-ROW-BACKOFF50MS-" +
            "MAX8199-SUBMISSIONS-EXACT-CLEANUP2-3092FA15";
        private const string PreviewPoweredCancellationApprovalToken =
            "I-APPROVE-PRECISION2-POWERED-CANCEL-ROWS32-96-" +
            "EXACT-SHORT10-ASPI-BUSY-MAX32-PER-ROW-BACKOFF50MS-" +
            "EXACT-CLEANUP2-3092FA15";
        private const string FullScanStartupFingerprintApprovalToken =
            "I-APPROVE-PRECISION2-POWERED-PREVIEW-CLEANUP2-THEN-" +
            "BACKOFF50MS-MAX32-READONLY-FULL-SCAN-INIT-AND-" +
            "BLOCK-FIRST-WRITE-OR-WINDOW";
        private const string FullScanSetWindowApprovalToken =
            "I-APPROVE-PRECISION2-BACKOFF50MS-MAX32-FULL-SCAN-" +
            "SET-WINDOW-210F3499-THEN-BLOCK-FIRST-IMAGE-READ";
        private const string FullScanSetWindowSha256 =
            "210F3499FB1BE7F689F44A26C68D8D079E6FB01E17AE19B320B5011E6F8A9EDA";
        private const string FullScanCompleteApprovalToken =
            "I-APPROVE-PRECISION2-BACKOFF50MS-MAX32-FULL-SCAN-" +
            "762X4494-MAX1800000MS-CLEANUP-23B3C62B";
        private const string FullScanTerminalProbeApprovalToken =
            "I-APPROVE-PRECISION2-FULL-SCAN-762X4494-" +
            "MAX1800000MS-ONE-" +
            "QUARANTINED-ROW763-TERMINAL-PROBE";
        private const string FullScanNaturalCompleteApprovalToken =
            "I-APPROVE-PRECISION2-FULL-SCAN-NATURAL996X4494-" +
            "MAX7968-SHORT-MAX8964-SUBMISSIONS-MAX1800000MS-" +
            "CLEANUP2-23B3C62B";
        private const string FullScanRow997CompleteApprovalToken =
            "I-APPROVE-PRECISION2-FULL-SCAN-ROW997X4494-" +
            "MAX7976-SHORT-MAX8973-SUBMISSIONS-MAX1800000MS-" +
            "BLOCK-ROW998-CLEANUP2-23B3C62B";
        private const string FullScanProgressCompleteApprovalToken =
            "I-APPROVE-PRECISION2-FULL-SCAN-EXACT998X4494-" +
            "MAX7984-SHORT-MAX8982-SUBMISSIONS-MAX1800000MS-" +
            "PROGRESS-OBSERVED-CLEANUP2-23B3C62B";
        private const int PreviewBurstRows = 8;
        private const int PreviewStreamRows = 256;
        private const int PreviewNaturalRows = 996;
        private const int PreviewStreamMaximumShortRetries = 64;
        private const int PreviewStreamMaximumSubmissions = 320;
        private const int PreviewNaturalMaximumShortRetries = 512;
        private const int PreviewNaturalMaximumSubmissions = 1508;
        private const int PreviewNaturalPerRowMaximumShortRetries = 7968;
        private const int PreviewNaturalPerRowMaximumSubmissions = 8964;
        private const int PreviewPoweredNaturalRows = 911;
        private const int PreviewCancellationTriggerRows = 32;
        private const int PreviewCancellationMaximumRows = 96;
        private const int PreviewPoweredNaturalMaximumShortRetries = 7288;
        private const int PreviewPoweredNaturalMaximumSubmissions = 8199;
        private const int OfflineCancelTriggerRows = 32;
        private const int OfflineCancelMaximumRows = 96;
        private const string PrecisionTwoFirmwareSha256 =
            "D8D7188574C52B255CF7940BEF7CC9192F744693AABC1F735758B7FECDEC65F3";

        private static int Main(string[] arguments)
        {
            try
            {
                if (arguments.Length == 2 && arguments[0] == "preflight")
                {
                    PreflightResult result = Preflight(arguments[1]);
                    PrintPreflight(result);
                    return 0;
                }
                if (arguments.Length == 2 &&
                    arguments[0] == "loader-preflight")
                {
                    PreflightResult result = PreflightLoader(arguments[1]);
                    PrintPreflight(result);
                    PrintLoaderPreflight(result);
                    return 0;
                }
                if (arguments.Length == 3 &&
                    arguments[0] == "launch-replay" &&
                    arguments[2] == ReplayApproval)
                {
                    PreflightResult result = Preflight(arguments[1]);
                    PrintPreflight(result);
                    LaunchResult launch = StartTransport(result,
                        "offline-replay", "aspi-replay");
                    Console.WriteLine("Started offline-replay FlexColor PID {0}.",
                        launch.Process.Id);
                    Console.WriteLine("ASPI log: {0}", launch.LogPath);
                    Console.WriteLine(
                        "WinUSB is disabled in this process. Close FlexColor " +
                        "normally when inspection is complete.");
                    return 0;
                }
                if (arguments.Length == 3 &&
                    arguments[0] == "smoke-replay" &&
                    arguments[2] == SmokeApproval)
                {
                    return SmokeReplay(Preflight(arguments[1]));
                }
                if (arguments.Length == 3 &&
                    arguments[0] == "smoke-operational-replay" &&
                    arguments[2] == OperationalReplaySmokeApproval)
                {
                    return SmokeOperationalReplay(Preflight(arguments[1]),
                        null);
                }
                if (arguments.Length == 4 &&
                    arguments[0] == "smoke-operational-replay-frame" &&
                    arguments[3] == OperationalReplaySmokeApproval)
                {
                    return SmokeOperationalReplay(Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 4 &&
                    arguments[0] == "smoke-offline-full-scan" &&
                    arguments[3] == FullScanReplaySmokeApproval)
                {
                    return SmokeOfflineFullScan(Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 4 &&
                    arguments[0] == "smoke-offline-operator-repeat" &&
                    arguments[3] == OfflineOperatorRepeatSmokeApproval)
                {
                    return SmokeOfflineOperatorRepeat(
                        Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 4 &&
                    arguments[0] == "smoke-live-operator-repeat-60x60" &&
                    arguments[3] == LiveOperatorRepeatSmokeApproval)
                {
                    return SmokeLiveOperatorRepeat(
                        Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 4 &&
                    arguments[0] == "smoke-offline-preview-cancel" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == OfflineCancelReplaySmokeApproval)
                {
                    return SmokeOfflinePreviewCancel(
                        Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 3 &&
                    arguments[0] == "launch-live-read-only" &&
                    arguments[2] == LiveApproval)
                {
                    PreflightResult result = Preflight(arguments[1]);
                    PrintPreflight(result);
                    LaunchResult launch = StartTransport(result,
                        "live-read-only", "aspi-live-read-only");
                    Console.WriteLine(
                        "Started live-read-only FlexColor PID {0}.",
                        launch.Process.Id);
                    Console.WriteLine("ASPI log: {0}", launch.LogPath);
                    Console.WriteLine(
                        "Only standard diagnostics, one exact target-5 D8, " +
                        "and one sequenced ScannerReady poll are enabled. " +
                        "All data-out and scanning commands remain blocked.");
                    return 0;
                }
                if (arguments.Length == 3 &&
                    arguments[0] == "smoke-live-read-only" &&
                    arguments[2] == LiveSmokeApproval)
                {
                    return SmokeLiveReadOnly(Preflight(arguments[1]));
                }
                if (arguments.Length == 3 &&
                    arguments[0] ==
                        "launch-trusted-flexcolor-pass-through" &&
                    arguments[2] == TrustedPassThroughLaunchApproval)
                {
                    PreflightResult result = Preflight(arguments[1]);
                    PrintPreflight(result);
                    LaunchResult launch = StartTransport(result,
                        "trusted-flexcolor-pass-through",
                        "aspi-trusted-flexcolor",
                        trustedFlexColorApproval:
                            TrustedFlexColorApprovalToken);
                    Console.WriteLine(
                        "Started trusted transparent FlexColor PID {0}.",
                        launch.Process.Id);
                    Console.WriteLine("ASPI log: {0}", launch.LogPath);
                    Console.WriteLine(
                        "The exact supported FlexColor process now controls " +
                        "scanner command order. The provider enforces ASPI " +
                        "structure, target 5/LUN 0, 16 MiB transfer and " +
                        "120-second per-transfer bounds, serialization, and " +
                        "status/sense mapping; payloads are not logged.");
                    return 0;
                }
                if (arguments.Length == 3 &&
                    arguments[0] == "launch-live-loader" &&
                    arguments[2] == LoaderLaunchApproval)
                {
                    PreflightResult result = PreflightLoader(arguments[1]);
                    PrintPreflight(result);
                    PrintLoaderPreflight(result);
                    LaunchResult launch = StartTransport(result,
                        "live-loader", "aspi-live-loader",
                        LoaderApprovalToken);
                    Console.WriteLine(
                        "Started state-changing loader FlexColor PID {0}.",
                        launch.Process.Id);
                    Console.WriteLine("ASPI log: {0}", launch.LogPath);
                    Console.WriteLine(
                        "Only the exact sixteen-record Precision II loader " +
                        "sequence and terminal are enabled after loader " +
                        "identity and D8. Close FlexColor normally when done.");
                    return 0;
                }
                if (arguments.Length == 4 &&
                    arguments[0] == "smoke-live-preview-fingerprint" &&
                    arguments[3] == PreviewFingerprintSmokeApproval)
                {
                    return SmokeLivePreviewFingerprint(
                        Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 4 &&
                    arguments[0] == "smoke-live-preview-observe" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == PreviewSmokeApproval)
                {
                    return SmokeLivePreview(Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]),
                        0);
                }
                if (arguments.Length == 4 &&
                    arguments[0] ==
                        "smoke-live-preview-set-window-24x36-observe" &&
                    arguments[2] == "24x36" &&
                    arguments[3] == Preview24x36SetWindowSmokeApproval)
                {
                    return SmokeLive24x36PreviewSetWindow(
                        Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 4 &&
                    arguments[0] == "smoke-live-preview-first-read-24x36" &&
                    arguments[2] == "24x36" &&
                    arguments[3] == Preview24x36FirstReadSmokeApproval)
                {
                    return SmokeLive24x36PreviewFirstRead(
                        Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 4 &&
                    arguments[0] == "smoke-live-preview-first-read-4x5" &&
                    arguments[2] == "4x5" &&
                    arguments[3] == Preview4x5FirstReadSmokeApproval)
                {
                    return SmokeLive4x5PreviewFirstRead(
                        Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 4 &&
                    arguments[0] == "smoke-live-preview-first-read" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == PreviewFirstReadSmokeApproval)
                {
                    return SmokeLivePreview(Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]),
                        1);
                }
                if (arguments.Length == 4 &&
                    arguments[0] == "smoke-live-preview-burst-8" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == PreviewBurstSmokeApproval)
                {
                    return SmokeLivePreview(Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]),
                        PreviewBurstRows);
                }
                if (arguments.Length == 4 &&
                    arguments[0] == "smoke-live-preview-stream-256" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == PreviewStreamSmokeApproval)
                {
                    return SmokeLivePreview(Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]),
                        PreviewStreamRows);
                }
                if (arguments.Length == 4 &&
                    arguments[0] ==
                        "smoke-live-preview-stream-256-short-retry" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == PreviewShortRetrySmokeApproval)
                {
                    return SmokeLivePreview(Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]),
                        PreviewStreamRows, true);
                }
                if (arguments.Length == 4 &&
                    arguments[0] ==
                        "smoke-live-preview-natural-996-short-retry" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == PreviewNaturalSmokeApproval)
                {
                    return SmokeLivePreview(Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]),
                        PreviewNaturalRows, true);
                }
                if (arguments.Length == 4 &&
                    arguments[0] ==
                        "smoke-live-preview-natural-996-per-row-8" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == PreviewNaturalPerRowSmokeApproval)
                {
                    return SmokeLivePreview(Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]),
                        PreviewNaturalRows, true, true);
                }
                if (arguments.Length == 4 &&
                    arguments[0] ==
                        "smoke-live-preview-powered-natural-911-observe" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == PreviewPoweredNaturalSmokeApproval)
                {
                    return SmokeLivePreview(Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]),
                        PreviewPoweredNaturalRows, true, false, true);
                }
                if (arguments.Length == 4 &&
                    arguments[0] ==
                        "smoke-live-preview-powered-natural-911-cleanup-2" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == PreviewPoweredCleanupSmokeApproval)
                {
                    return SmokeLivePreview(Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]),
                        PreviewPoweredNaturalRows, true, false, true, true);
                }
                if (arguments.Length == 4 &&
                    arguments[0] ==
                        "smoke-live-preview-powered-cancel-32-96-cleanup-2" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == PreviewPoweredCancellationSmokeApproval)
                {
                    return SmokeLivePreview(Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]),
                        PreviewPoweredNaturalRows, true, false, true, false,
                        true);
                }
                if (arguments.Length == 4 &&
                    arguments[0] ==
                        "smoke-live-full-scan-startup-fingerprint" &&
                    arguments[2] == "60x60" &&
                    arguments[3] ==
                        FullScanStartupFingerprintSmokeApproval)
                {
                    return SmokeLiveFullScanStartupFingerprint(
                        Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 4 &&
                    arguments[0] ==
                        "smoke-live-full-scan-set-window-first-read" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == FullScanSetWindowSmokeApproval)
                {
                    return SmokeLiveFullScanSetWindowFirstRead(
                        Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 4 &&
                    arguments[0] == "smoke-live-full-scan-complete-60x60" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == FullScanCompleteSmokeApproval)
                {
                    return SmokeLiveFullScanComplete(
                        Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 4 &&
                    arguments[0] ==
                        "smoke-live-full-scan-terminal-probe-60x60" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == FullScanTerminalProbeSmokeApproval)
                {
                    return SmokeLiveFullScanTerminalProbe(
                        Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 4 &&
                    arguments[0] ==
                        "smoke-live-full-scan-natural-996-complete-60x60" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == FullScanNaturalCompleteSmokeApproval)
                {
                    return SmokeLiveFullScanNaturalComplete(
                        Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 4 &&
                    arguments[0] ==
                        "smoke-live-full-scan-row997-complete-60x60" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == FullScanRow997CompleteSmokeApproval)
                {
                    return SmokeLiveFullScanRow997Complete(
                        Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 4 &&
                    arguments[0] ==
                        "smoke-live-full-scan-progress-complete-60x60" &&
                    arguments[2] == "60x60" &&
                    arguments[3] == FullScanProgressCompleteSmokeApproval)
                {
                    return SmokeLiveFullScanProgressComplete(
                        Preflight(arguments[1]),
                        FlexColorFrameSelector.GetExactLabel(arguments[2]));
                }
                if (arguments.Length == 3 &&
                    arguments[0] == "launch-live-preview-observe" &&
                    arguments[2] == PreviewLaunchApproval)
                {
                    PreflightResult result = Preflight(arguments[1]);
                    PrintPreflight(result);
                    LaunchResult launch = StartTransport(result,
                        "live-preview-observe", "aspi-live-preview-observe",
                        null, null, PreviewApprovalToken);
                    Console.WriteLine(
                        "Started state-changing Preview-observation FlexColor " +
                        "PID {0}.", launch.Process.Id);
                    Console.WriteLine("ASPI log: {0}", launch.LogPath);
                    Console.WriteLine(
                        "Only the pinned one-shot Preview SET WINDOW, its two " +
                        "exact ScannerReady successors, and the already " +
                        "bounded operational initialization are enabled. The " +
                        "first selector-28 image READ is recorded and blocked " +
                        "before USB; loader and cleanup writes remain disabled.");
                    return 0;
                }

                PrintUsage();
                return 2;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("REFUSED: {0}", exception.Message);
                return 3;
            }
        }

        private static PreflightResult Preflight(string privateRoot)
        {
            string root = Path.GetFullPath(privateRoot).TrimEnd('\\');
            if (!Directory.Exists(root))
            {
                throw new InvalidOperationException(
                    "Private FlexColor root was not found: " + root);
            }
            RequirePrivatePath(root);
            RejectReparsePoints(root);

            string executable = Path.Combine(root, "FlexColor.exe");
            string flexColorDll = Path.Combine(root, "DLLS", "FlexColor.dll");
            string provider = Path.Combine(root, "wnaspi32.dll");
            string config = executable + ".config";
            RequireFile(executable);
            RequireFile(flexColorDll);
            RequireFile(provider);
            RequireFile(config);

            string executableHash = Sha256(executable);
            if (executableHash != FlexColorExeSha256)
            {
                throw new InvalidOperationException(
                    "FlexColor.exe hash is not the supported 4.0.3 image: " +
                    executableHash);
            }

            FlexColorPatchEngine patcher =
                FlexColorPatchEngine.CreateProduction();
            PatchInspection patch = patcher.Inspect(flexColorDll);
            if (patch.State != PatchState.Active ||
                patch.BackupState != BackupState.Original)
            {
                throw new InvalidOperationException(string.Format(
                    "FlexColor.dll must be Active with an Original backup; " +
                    "found {0}/{1}.", patch.State, patch.BackupState));
            }

            PeImageInfo pe = PeExportInspector.Inspect(provider);
            if (pe.Machine != 0x014c || pe.OptionalHeaderMagic != 0x010b)
            {
                throw new InvalidOperationException(
                    "wnaspi32.dll is not PE32/I386.");
            }
            RequireExport(pe, "GetASPI32SupportInfo");
            RequireExport(pe, "SendASPI32Command");

            var dependencyHashes = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            dependencyHashes["wnaspi32.dll"] = RequireExactHash(provider,
                AspiProviderBuildHashes.NativeProviderSha256);

            string[] dependencies = new string[]
            {
                "Usb2Xchange.AspiShim.Managed.dll",
                "Usb2Xchange.WinUsb.dll",
                "Usb2Xchange.Protocol.dll",
                "wnaspi32.dll.config"
            };
            string[] expectedDependencyHashes = new string[]
            {
                AspiProviderBuildHashes.ManagedShimSha256,
                AspiProviderBuildHashes.WinUsbSha256,
                AspiProviderBuildHashes.ProtocolSha256,
                AspiProviderBuildHashes.ProviderConfigSha256
            };
            for (int index = 0; index < dependencies.Length; ++index)
            {
                string dependency = dependencies[index];
                dependencyHashes[dependency] = RequireExactHash(
                    Path.Combine(root, dependency),
                    expectedDependencyHashes[index]);
            }
            dependencyHashes["FlexColor.exe.config"] = RequireExactHash(
                config, AspiProviderBuildHashes.FlexColorConfigSha256);

            string configText = File.ReadAllText(config);
            if (configText.IndexOf("supportedRuntime version=\"v4.0\"",
                    StringComparison.Ordinal) < 0 ||
                configText.IndexOf("useLegacyV2RuntimeActivationPolicy=\"true\"",
                    StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException(
                    "FlexColor.exe.config does not enable the required CLR v4 runtime.");
            }
            return new PreflightResult(root, executable, provider,
                executableHash, dependencyHashes, patch);
        }

        private static PreflightResult PreflightLoader(string privateRoot)
        {
            PreflightResult result = Preflight(privateRoot);
            string firmware = Path.Combine(result.Root, "Firmware",
                "MICROCOD.3XX");
            RequireFile(firmware);
            var info = new FileInfo(firmware);
            string hash = Sha256(firmware);
            if (info.Length != 65536 || hash != PrecisionTwoFirmwareSha256)
            {
                throw new InvalidOperationException(string.Format(
                    "MICROCOD.3XX is not the exact 65,536-byte Precision II " +
                    "image: length={0}, SHA-256={1}.", info.Length, hash));
            }
            result.LoaderFirmwareHash = hash;
            return result;
        }

        private static LaunchResult StartTransport(PreflightResult preflight,
            string transportMode, string logPrefix,
            string loaderApproval = null, string replayIdentifier = null,
            string previewApproval = null,
            string previewFingerprintApproval = null,
            string previewFirstReadApproval = null,
            string previewBurstApproval = null,
            string previewStreamApproval = null,
            string previewShortRetryApproval = null,
            string previewNaturalApproval = null,
            string previewNaturalPerRowApproval = null,
            string previewPoweredNaturalApproval = null,
            string previewPoweredCleanupApproval = null,
            string offlineCancelApproval = null,
            string previewPoweredCancellationApproval = null,
            string fullScanStartupFingerprintApproval = null,
            string fullScanSetWindowApproval = null,
            string fullScanCompleteApproval = null,
            string fullScanTerminalProbeApproval = null,
            string fullScanNaturalCompleteApproval = null,
            string fullScanRow997CompleteApproval = null,
            string fullScanProgressCompleteApproval = null,
            string preview24x36SetWindowApproval = null,
            string preview24x36FirstReadApproval = null,
            string preview4x5FirstReadApproval = null,
            string offlineOperatorApproval = null,
            string operatorSessionApproval = null,
            string trustedFlexColorApproval = null)
        {
            Process[] existing = Process.GetProcessesByName("FlexColor");
            if (existing.Length != 0)
            {
                foreach (Process existingProcess in existing)
                {
                    existingProcess.Dispose();
                }
                throw new InvalidOperationException(
                    "A FlexColor process is already running. Close it first.");
            }

            string logDirectory = Path.Combine(preflight.Root,
                "Usb2XchangeLogs");
            string roaming = Path.Combine(preflight.Root,
                "Usb2XchangeProfile", "Roaming");
            string local = Path.Combine(preflight.Root,
                "Usb2XchangeProfile", "Local");
            Directory.CreateDirectory(logDirectory);
            Directory.CreateDirectory(roaming);
            Directory.CreateDirectory(local);
            string logPath = Path.Combine(logDirectory,
                logPrefix + "-" + DateTime.UtcNow.ToString(
                    "yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) +
                ".log");

            var start = new ProcessStartInfo(preflight.Executable);
            start.WorkingDirectory = preflight.Root;
            start.UseShellExecute = false;
            start.EnvironmentVariables["USB2XCHANGE_ASPI_TRANSPORT"] =
                transportMode;
            start.EnvironmentVariables["USB2XCHANGE_ASPI_LOG_PATH"] = logPath;
            start.EnvironmentVariables["APPDATA"] = roaming;
            start.EnvironmentVariables["LOCALAPPDATA"] = local;
            if (!string.IsNullOrEmpty(loaderApproval))
            {
                start.EnvironmentVariables[
                    LoaderApprovalEnvironmentVariable] = loaderApproval;
            }
            if (!string.IsNullOrEmpty(replayIdentifier))
            {
                start.EnvironmentVariables[
                    ReplayIdentifierEnvironmentVariable] = replayIdentifier;
            }
            if (!string.IsNullOrEmpty(previewApproval))
            {
                start.EnvironmentVariables[
                    PreviewApprovalEnvironmentVariable] = previewApproval;
            }
            if (!string.IsNullOrEmpty(preview24x36SetWindowApproval))
            {
                start.EnvironmentVariables[
                    Preview24x36SetWindowApprovalEnvironmentVariable] =
                        preview24x36SetWindowApproval;
            }
            if (!string.IsNullOrEmpty(preview24x36FirstReadApproval))
            {
                start.EnvironmentVariables[
                    Preview24x36FirstReadApprovalEnvironmentVariable] =
                        preview24x36FirstReadApproval;
            }
            if (!string.IsNullOrEmpty(preview4x5FirstReadApproval))
            {
                start.EnvironmentVariables[
                    Preview4x5FirstReadApprovalEnvironmentVariable] =
                        preview4x5FirstReadApproval;
            }
            if (!string.IsNullOrEmpty(previewFingerprintApproval))
            {
                start.EnvironmentVariables[
                    PreviewFingerprintApprovalEnvironmentVariable] =
                        previewFingerprintApproval;
            }
            if (!string.IsNullOrEmpty(previewFirstReadApproval))
            {
                start.EnvironmentVariables[
                    PreviewFirstReadApprovalEnvironmentVariable] =
                        previewFirstReadApproval;
            }
            if (!string.IsNullOrEmpty(previewBurstApproval))
            {
                start.EnvironmentVariables[
                    PreviewBurstApprovalEnvironmentVariable] =
                        previewBurstApproval;
            }
            if (!string.IsNullOrEmpty(previewStreamApproval))
            {
                start.EnvironmentVariables[
                    PreviewStreamApprovalEnvironmentVariable] =
                        previewStreamApproval;
            }
            if (!string.IsNullOrEmpty(previewShortRetryApproval))
            {
                start.EnvironmentVariables[
                    PreviewShortRetryApprovalEnvironmentVariable] =
                        previewShortRetryApproval;
            }
            if (!string.IsNullOrEmpty(previewNaturalApproval))
            {
                start.EnvironmentVariables[
                    PreviewNaturalApprovalEnvironmentVariable] =
                        previewNaturalApproval;
            }
            if (!string.IsNullOrEmpty(previewNaturalPerRowApproval))
            {
                start.EnvironmentVariables[
                    PreviewNaturalPerRowApprovalEnvironmentVariable] =
                        previewNaturalPerRowApproval;
            }
            if (!string.IsNullOrEmpty(previewPoweredNaturalApproval))
            {
                start.EnvironmentVariables[
                    PreviewPoweredNaturalApprovalEnvironmentVariable] =
                        previewPoweredNaturalApproval;
            }
            if (!string.IsNullOrEmpty(previewPoweredCleanupApproval))
            {
                start.EnvironmentVariables[
                    PreviewPoweredCleanupApprovalEnvironmentVariable] =
                        previewPoweredCleanupApproval;
            }
            if (!string.IsNullOrEmpty(offlineCancelApproval))
            {
                start.EnvironmentVariables[
                    OfflineCancelApprovalEnvironmentVariable] =
                        offlineCancelApproval;
            }
            if (!string.IsNullOrEmpty(offlineOperatorApproval))
            {
                start.EnvironmentVariables[
                    OfflineOperatorApprovalEnvironmentVariable] =
                        offlineOperatorApproval;
            }
            if (!string.IsNullOrEmpty(operatorSessionApproval))
            {
                start.EnvironmentVariables[
                    OperatorSessionApprovalEnvironmentVariable] =
                        operatorSessionApproval;
            }
            if (!string.IsNullOrEmpty(trustedFlexColorApproval))
            {
                start.EnvironmentVariables[
                    TrustedFlexColorApprovalEnvironmentVariable] =
                        trustedFlexColorApproval;
            }
            if (!string.IsNullOrEmpty(previewPoweredCancellationApproval))
            {
                start.EnvironmentVariables[
                    PreviewPoweredCancellationApprovalEnvironmentVariable] =
                        previewPoweredCancellationApproval;
            }
            if (!string.IsNullOrEmpty(fullScanStartupFingerprintApproval))
            {
                start.EnvironmentVariables[
                    FullScanStartupFingerprintApprovalEnvironmentVariable] =
                        fullScanStartupFingerprintApproval;
            }
            if (!string.IsNullOrEmpty(fullScanSetWindowApproval))
            {
                start.EnvironmentVariables[
                    FullScanSetWindowApprovalEnvironmentVariable] =
                        fullScanSetWindowApproval;
            }
            if (!string.IsNullOrEmpty(fullScanCompleteApproval))
            {
                start.EnvironmentVariables[
                    FullScanCompleteApprovalEnvironmentVariable] =
                        fullScanCompleteApproval;
            }
            if (!string.IsNullOrEmpty(fullScanTerminalProbeApproval))
            {
                start.EnvironmentVariables[
                    FullScanTerminalProbeApprovalEnvironmentVariable] =
                        fullScanTerminalProbeApproval;
            }
            if (!string.IsNullOrEmpty(fullScanNaturalCompleteApproval))
            {
                start.EnvironmentVariables[
                    FullScanNaturalCompleteApprovalEnvironmentVariable] =
                        fullScanNaturalCompleteApproval;
            }
            if (!string.IsNullOrEmpty(fullScanRow997CompleteApproval))
            {
                start.EnvironmentVariables[
                    FullScanRow997CompleteApprovalEnvironmentVariable] =
                    fullScanRow997CompleteApproval;
            }
            if (!string.IsNullOrEmpty(fullScanProgressCompleteApproval))
            {
                start.EnvironmentVariables[
                    FullScanProgressCompleteApprovalEnvironmentVariable] =
                        fullScanProgressCompleteApproval;
            }
            // The PowerShell workflow captures this launcher's output. Ensure
            // the long-lived GUI cannot inherit any of that pipeline and keep
            // Start blocked until FlexColor exits.
            Process process = StartWithoutInheritedHandles(start);
            if (process == null)
            {
                throw new InvalidOperationException("FlexColor did not start.");
            }
            return new LaunchResult(process, logPath);
        }

        private static Process StartWithoutInheritedHandles(
            ProcessStartInfo start)
        {
            var environmentEntries = new List<string>();
            foreach (string key in start.EnvironmentVariables.Keys)
            {
                environmentEntries.Add(
                    key + "=" + start.EnvironmentVariables[key]);
            }
            environmentEntries.Sort(StringComparer.OrdinalIgnoreCase);
            string environmentBlock = string.Join("\0",
                environmentEntries.ToArray()) + "\0\0";
            IntPtr environment = Marshal.StringToHGlobalUni(environmentBlock);
            var startup = new NativeMethods.StartupInfo();
            startup.Size = Marshal.SizeOf(typeof(NativeMethods.StartupInfo));
            var commandLine = new StringBuilder(
                "\"" + start.FileName + "\"");
            NativeMethods.ProcessInformation information;
            try
            {
                if (!NativeMethods.CreateProcess(start.FileName, commandLine,
                        IntPtr.Zero, IntPtr.Zero, false,
                        NativeMethods.CreateUnicodeEnvironment |
                            NativeMethods.CreateNoWindow,
                        environment, start.WorkingDirectory, ref startup,
                        out information))
                {
                    throw new InvalidOperationException(
                        "Could not start FlexColor without inherited handles. " +
                        "Windows error " +
                        Marshal.GetLastWin32Error().ToString(
                            CultureInfo.InvariantCulture) + ".");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(environment);
            }

            try
            {
                return Process.GetProcessById(
                    unchecked((int)information.ProcessId));
            }
            finally
            {
                NativeMethods.CloseHandle(information.ThreadHandle);
                NativeMethods.CloseHandle(information.ProcessHandle);
            }
        }

        private static int SmokeReplay(PreflightResult preflight)
        {
            PrintPreflight(preflight);
            LaunchResult launch = StartTransport(preflight,
                "offline-replay", "aspi-replay");
            bool support = false;
            bool command = false;
            bool replay = false;
            bool identity = false;
            try
            {
                // This 2005 application can spend more than 30 seconds in
                // process/UI initialization before it loads FlexColor.dll.
                DateTime deadline = DateTime.UtcNow.AddSeconds(120);
                while (DateTime.UtcNow < deadline && !launch.Process.HasExited)
                {
                    if (File.Exists(launch.LogPath))
                    {
                        string text = ReadSharedText(launch.LogPath);
                        support = text.IndexOf("GetASPI32SupportInfo",
                            StringComparison.Ordinal) >= 0;
                        command = text.IndexOf("SendASPI32Command",
                            StringComparison.Ordinal) >= 0;
                        replay = text.IndexOf(
                            "OFFLINE REPLAY request: target=5, lun=0",
                            StringComparison.Ordinal) >= 0;
                        identity = text.IndexOf(
                            "OFFLINE REPLAY synthetic INQUIRY returned",
                            StringComparison.Ordinal) >= 0;
                        if (support && command && replay && identity)
                        {
                            break;
                        }
                    }
                    Thread.Sleep(100);
                }
            }
            finally
            {
                CloseTestProcess(launch.Process);
            }

            // FlexColor may defer DLL initialization until it processes the
            // close request. Count a completed replay that reached the log
            // before this launcher finished closing its own test process.
            if (File.Exists(launch.LogPath))
            {
                string finalText = ReadSharedText(launch.LogPath);
                support = support || finalText.IndexOf(
                    "GetASPI32SupportInfo", StringComparison.Ordinal) >= 0;
                command = command || finalText.IndexOf(
                    "SendASPI32Command", StringComparison.Ordinal) >= 0;
                replay = replay || finalText.IndexOf(
                    "OFFLINE REPLAY request: target=5, lun=0",
                    StringComparison.Ordinal) >= 0;
                identity = identity || finalText.IndexOf(
                    "OFFLINE REPLAY synthetic INQUIRY returned",
                    StringComparison.Ordinal) >= 0;
            }

            Console.WriteLine("ASPI log: {0}", launch.LogPath);
            Console.WriteLine("Support export observed: {0}", support);
            Console.WriteLine("ASPI command observed: {0}", command);
            Console.WriteLine("Target-5 replay reached: {0}", replay);
            Console.WriteLine("Synthetic identity returned: {0}", identity);
            Console.WriteLine("WinUSB disabled: yes");
            if (!support || !command || !replay || !identity)
            {
                Console.Error.WriteLine(
                    "Offline replay did not complete FlexColor's target-5 " +
                    "synthetic INQUIRY.");
                return 4;
            }
            Console.WriteLine("Offline FlexColor ASPI smoke test passed.");
            return 0;
        }

        private static int SmokeLiveReadOnly(PreflightResult preflight)
        {
            PrintPreflight(preflight);
            LaunchResult launch = StartTransport(preflight,
                "live-read-only", "aspi-live-read-only");
            bool support = false;
            bool operationalIdentity = false;
            bool loaderIdentity = false;
            try
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(120);
                while (DateTime.UtcNow < deadline && !launch.Process.HasExited)
                {
                    if (File.Exists(launch.LogPath))
                    {
                        string text = ReadSharedText(launch.LogPath);
                        support = text.IndexOf("GetASPI32SupportInfo",
                            StringComparison.Ordinal) >= 0;
                        operationalIdentity = text.IndexOf(
                            "LIVE ASPI target-5 identity: Imacon/" +
                            "FlexTight II/M333, type=0x06, " +
                            "classification=Operational.",
                            StringComparison.Ordinal) >= 0;
                        loaderIdentity = text.IndexOf(
                            "LIVE ASPI target-5 identity: Imacon/" +
                            "SCSI Loader/L302, type=0x06, " +
                            "classification=Loader.",
                            StringComparison.Ordinal) >= 0;
                        if (support &&
                            (operationalIdentity || loaderIdentity))
                        {
                            break;
                        }
                    }
                    Thread.Sleep(100);
                }
            }
            finally
            {
                CloseTestProcess(launch.Process);
            }

            if (File.Exists(launch.LogPath))
            {
                string finalText = ReadSharedText(launch.LogPath);
                support = support || finalText.IndexOf(
                    "GetASPI32SupportInfo", StringComparison.Ordinal) >= 0;
                operationalIdentity = operationalIdentity ||
                    finalText.IndexOf(
                        "LIVE ASPI target-5 identity: Imacon/" +
                        "FlexTight II/M333, type=0x06, " +
                        "classification=Operational.",
                        StringComparison.Ordinal) >= 0;
                loaderIdentity = loaderIdentity || finalText.IndexOf(
                    "LIVE ASPI target-5 identity: Imacon/" +
                    "SCSI Loader/L302, type=0x06, " +
                    "classification=Loader.",
                    StringComparison.Ordinal) >= 0;
            }

            Console.WriteLine("ASPI log: {0}", launch.LogPath);
            Console.WriteLine("Support export observed: {0}", support);
            Console.WriteLine("Exact operational M333 identity: {0}",
                operationalIdentity);
            Console.WriteLine("Exact loader L302 identity: {0}",
                loaderIdentity);
            Console.WriteLine("Data-out enabled: no");
            if (!support || !operationalIdentity)
            {
                Console.Error.WriteLine(
                    loaderIdentity
                        ? "Live read-only smoke found exact loader L302; " +
                            "the reviewed firmware loader must complete " +
                            "before an operational test."
                        : "Live read-only smoke did not return exact " +
                            "operational FlexTight II/M333 identity.");
                return 4;
            }
            Console.WriteLine("Live read-only FlexColor ASPI smoke test passed.");
            return 0;
        }

        private static int SmokeOperationalReplay(
            PreflightResult preflight, string requestedFrameLabel)
        {
            PrintPreflight(preflight);
            string replayIdentifier = FindLocalReplayIdentifier();
            Console.WriteLine("Compatible local device marker available: {0}",
                replayIdentifier == null ? "no" : "yes");
            LaunchResult launch = StartTransport(preflight,
                "offline-replay", "aspi-operational-replay", null,
                replayIdentifier);
            bool support = false;
            bool d8 = false;
            bool cycle = false;
            bool setWindow = false;
            bool imageRead = false;
            bool cleanup = false;
            bool crashLogDeleted = false;
            bool registrationDeferred = false;
            bool frameVerified = requestedFrameLabel == null;
            bool previewInvoked = false;
            try
            {
                // Keep the parent launcher alive after this 2005 process
                // finally creates its UI (often just over three minutes), so
                // an offline-only Preview action can be inspected/invoked.
                DateTime deadline = DateTime.UtcNow.AddSeconds(300);
                while (DateTime.UtcNow < deadline && !launch.Process.HasExited)
                {
                    if (!crashLogDeleted &&
                        DeleteCrashNotificationIfPresent(launch.Process))
                    {
                        crashLogDeleted = true;
                        Console.WriteLine(
                            "Clicked only FlexColor bomblog notification > " +
                            "Delete Log, as requested; the report was not " +
                            "viewed or sent.");
                    }
                    if (!registrationDeferred &&
                        DeferRegistrationIfPresent(launch.Process))
                    {
                        registrationDeferred = true;
                        Console.WriteLine(
                            "Clicked only Registration > Register Later; " +
                            "no registration data was entered or sent.");
                    }
                    if (!frameVerified && FlexColorFrameSelector.TrySelectExact(
                            launch.Process, requestedFrameLabel))
                    {
                        frameVerified = true;
                        Console.WriteLine(
                            "Selected and re-verified exact Frame item: {0}",
                            requestedFrameLabel);
                    }
                    if (frameVerified && !previewInvoked &&
                        ClickPreviewIfPresent(launch.Process))
                    {
                        previewInvoked = true;
                        Console.WriteLine(
                            "Invoked the one normal-sized, visible, enabled " +
                            "Preview button in the offline process.");
                    }
                    if (File.Exists(launch.LogPath))
                    {
                        string text = ReadSharedText(launch.LogPath);
                        support = text.IndexOf("GetASPI32SupportInfo",
                            StringComparison.Ordinal) >= 0;
                        d8 = text.IndexOf(
                            "OFFLINE REPLAY accepted operational D8",
                            StringComparison.Ordinal) >= 0;
                        cycle = text.IndexOf("cycles=1",
                            StringComparison.Ordinal) >= 0;
                        setWindow = text.IndexOf(
                            "OFFLINE REPLAY simulated structurally valid " +
                            "Preview SET WINDOW",
                            StringComparison.Ordinal) >= 0;
                        imageRead = text.IndexOf(
                            "OFFLINE REPLAY returned deterministic synthetic " +
                            "Preview image row",
                            StringComparison.Ordinal) >= 0;
                        cleanup = text.IndexOf(
                            "OFFLINE REPLAY completed two post-image SET " +
                            "WINDOW cleanup requests",
                            StringComparison.Ordinal) >= 0;
                        if (support && d8 && cycle && setWindow && imageRead &&
                            cleanup)
                        {
                            break;
                        }
                    }
                    Thread.Sleep(100);
                }
            }
            finally
            {
                CloseTestProcess(launch.Process);
            }

            PreviewReplayMetadata metadata = null;
            if (File.Exists(launch.LogPath))
            {
                string text = ReadSharedText(launch.LogPath);
                support = support || text.IndexOf("GetASPI32SupportInfo",
                    StringComparison.Ordinal) >= 0;
                d8 = d8 || text.IndexOf(
                    "OFFLINE REPLAY accepted operational D8",
                    StringComparison.Ordinal) >= 0;
                cycle = cycle || text.IndexOf("cycles=1",
                    StringComparison.Ordinal) >= 0;
                setWindow = setWindow || text.IndexOf(
                    "OFFLINE REPLAY simulated structurally valid Preview " +
                    "SET WINDOW",
                    StringComparison.Ordinal) >= 0;
                imageRead = imageRead || text.IndexOf(
                    "OFFLINE REPLAY returned deterministic synthetic Preview " +
                    "image row",
                    StringComparison.Ordinal) >= 0;
                cleanup = cleanup || text.IndexOf(
                    "OFFLINE REPLAY completed two post-image SET WINDOW " +
                    "cleanup requests",
                    StringComparison.Ordinal) >= 0;
                if (cleanup)
                {
                    metadata = PreviewReplayInspector.Inspect(text);
                }
            }

            Console.WriteLine("ASPI log: {0}", launch.LogPath);
            Console.WriteLine("Support export observed: {0}", support);
            Console.WriteLine("Operational D8 replayed: {0}", d8);
            Console.WriteLine("Initialization cycle completed: {0}", cycle);
            Console.WriteLine("SET WINDOW simulated offline: {0}", setWindow);
            Console.WriteLine("Synthetic image row returned: {0}", imageRead);
            Console.WriteLine("Two cleanup windows completed: {0}", cleanup);
            Console.WriteLine("Requested Frame: {0}",
                requestedFrameLabel ?? "current UI setting");
            Console.WriteLine("Frame selector verified: {0}", frameVerified);
            Console.WriteLine("Initial SET WINDOW payload SHA-256: {0}",
                metadata == null ? "not observed" :
                    metadata.SetWindowSha256);
            Console.WriteLine("First image READ CDB: {0}",
                metadata == null ? "not observed" : metadata.ImageReadCdb);
            Console.WriteLine("Image row width/bytes: {0}/{1}",
                metadata == null ? "not observed" :
                    metadata.ScanWidth.ToString(CultureInfo.InvariantCulture),
                metadata == null ? "not observed" :
                    metadata.RowBytes.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("Image rows completed: {0}",
                metadata == null ? "not observed" :
                    metadata.RowCount.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("WinUSB disabled: yes");
            if (!support || !d8 || !cycle || !setWindow || !imageRead ||
                !cleanup || !frameVerified || metadata == null)
            {
                Console.Error.WriteLine(
                    "Operational replay stopped before image cleanup; inspect " +
                    "the metadata-only log for the last exact command reached.");
                return 4;
            }
            Console.WriteLine(
                "Offline operational FlexColor replay completed synthetic " +
                "Preview rows and both cleanup windows.");
            return 0;
        }

        private static int SmokeLivePreviewFingerprint(
            PreflightResult preflight, string requestedFrameLabel)
        {
            PrintPreflight(preflight);
            LaunchResult launch = StartTransport(preflight,
                "live-preview-fingerprint", "aspi-live-preview-fingerprint",
                null, null, null, PreviewFingerprintApprovalToken);
            bool support = false;
            bool identity = false;
            bool frameVerified = false;
            bool previewInvoked = false;
            bool fingerprintCaptured = false;
            bool fingerprintEnvelopeRejected = false;
            bool structuredMetadataCaptured = false;
            bool crashLogDeleted = false;
            bool registrationDeferred = false;
            string payloadSha256 = null;
            try
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(300);
                while (DateTime.UtcNow < deadline && !launch.Process.HasExited)
                {
                    if (!crashLogDeleted &&
                        DeleteCrashNotificationIfPresent(launch.Process))
                    {
                        crashLogDeleted = true;
                        Console.WriteLine(
                            "Clicked only FlexColor bomblog notification > " +
                            "Delete Log, as requested; the report was not " +
                            "viewed or sent.");
                    }
                    if (!registrationDeferred &&
                        DeferRegistrationIfPresent(launch.Process))
                    {
                        registrationDeferred = true;
                        Console.WriteLine(
                            "Clicked only Registration > Register Later; " +
                            "no registration data was entered or sent.");
                    }
                    if (!frameVerified && FlexColorFrameSelector.TrySelectExact(
                            launch.Process, requestedFrameLabel))
                    {
                        frameVerified = true;
                        Console.WriteLine(
                            "Selected and re-verified exact Frame item: {0}",
                            requestedFrameLabel);
                    }
                    if (frameVerified && !previewInvoked &&
                        ClickPreviewIfPresent(launch.Process))
                    {
                        previewInvoked = true;
                        Console.WriteLine(
                            "Invoked the one normal-sized, visible, enabled " +
                            "Preview button.");
                    }
                    if (File.Exists(launch.LogPath))
                    {
                        string text = ReadSharedText(launch.LogPath);
                        support = text.IndexOf("GetASPI32SupportInfo",
                            StringComparison.Ordinal) >= 0;
                        identity = text.IndexOf(
                            "LIVE ASPI target-5 identity:",
                            StringComparison.Ordinal) >= 0;
                        fingerprintCaptured = text.IndexOf(
                            "Preview SET WINDOW fingerprint captured and " +
                            "blocked before USB; payload-sha256=",
                            StringComparison.Ordinal) >= 0;
                        fingerprintEnvelopeRejected = text.IndexOf(
                            "Preview SET WINDOW fingerprint envelope was " +
                            "invalid and blocked before USB",
                            StringComparison.Ordinal) >= 0;
                        structuredMetadataCaptured = text.IndexOf(
                            "Preview SET WINDOW structured metadata captured " +
                            "without retaining the payload:",
                            StringComparison.Ordinal) >= 0;
                        payloadSha256 = FindPayloadSha256After(text,
                            "Preview SET WINDOW fingerprint captured and " +
                            "blocked before USB;") ??
                            FindPayloadSha256After(text,
                                "Preview SET WINDOW fingerprint envelope " +
                                "was invalid and blocked before USB") ??
                            payloadSha256;
                        if (fingerprintCaptured || fingerprintEnvelopeRejected)
                        {
                            break;
                        }
                    }
                    Thread.Sleep(100);
                }
            }
            finally
            {
                CloseTestProcess(launch.Process);
            }

            if (File.Exists(launch.LogPath))
            {
                string text = ReadSharedText(launch.LogPath);
                support = support || text.IndexOf("GetASPI32SupportInfo",
                    StringComparison.Ordinal) >= 0;
                identity = identity || text.IndexOf(
                    "LIVE ASPI target-5 identity:",
                    StringComparison.Ordinal) >= 0;
                fingerprintCaptured = fingerprintCaptured || text.IndexOf(
                    "Preview SET WINDOW fingerprint captured and blocked " +
                    "before USB; payload-sha256=",
                    StringComparison.Ordinal) >= 0;
                fingerprintEnvelopeRejected =
                    fingerprintEnvelopeRejected || text.IndexOf(
                        "Preview SET WINDOW fingerprint envelope was invalid " +
                        "and blocked before USB",
                        StringComparison.Ordinal) >= 0;
                structuredMetadataCaptured = structuredMetadataCaptured ||
                    text.IndexOf(
                        "Preview SET WINDOW structured metadata captured " +
                        "without retaining the payload:",
                        StringComparison.Ordinal) >= 0;
                payloadSha256 = FindPayloadSha256After(text,
                    "Preview SET WINDOW fingerprint captured and blocked " +
                    "before USB;") ??
                    FindPayloadSha256After(text,
                        "Preview SET WINDOW fingerprint envelope was invalid " +
                        "and blocked before USB") ?? payloadSha256;
            }

            Console.WriteLine("ASPI log: {0}", launch.LogPath);
            Console.WriteLine("Support export observed: {0}", support);
            Console.WriteLine("Real target-5 identity returned: {0}", identity);
            Console.WriteLine("Requested Frame: {0}", requestedFrameLabel);
            Console.WriteLine("Frame selector verified: {0}", frameVerified);
            Console.WriteLine("Preview button invoked: {0}", previewInvoked);
            Console.WriteLine("SET WINDOW fingerprint captured: {0}",
                fingerprintCaptured);
            Console.WriteLine("SET WINDOW envelope rejected: {0}",
                fingerprintEnvelopeRejected);
            Console.WriteLine("Structured SET WINDOW metadata captured: {0}",
                structuredMetadataCaptured);
            Console.WriteLine("SET WINDOW payload SHA-256: {0}",
                payloadSha256 ?? "not observed");
            Console.WriteLine("SET WINDOW submitted to USB: no");
            if (!support || !identity || !frameVerified || !previewInvoked ||
                !fingerprintCaptured || !structuredMetadataCaptured ||
                payloadSha256 == null)
            {
                Console.Error.WriteLine(
                    "Fingerprint-only Preview smoke did not reach its exact " +
                    "terminal boundary; FlexColor was closed.");
                return 4;
            }
            Console.WriteLine(
                "Fingerprint-only Preview smoke passed; FlexColor was " +
                "closed after the one metadata observation.");
            return 0;
        }

        private static int SmokeOfflineFullScan(PreflightResult preflight,
            string requestedFrameLabel)
        {
            if (!string.Equals(requestedFrameLabel, "60x60",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The bounded offline full-scan replay currently pins " +
                    "only exact Frame 60x60.");
            }
            PrintPreflight(preflight);
            string replayIdentifier = FindLocalReplayIdentifier();
            Console.WriteLine("Compatible local device marker available: {0}",
                replayIdentifier == null ? "no" : "yes");
            string outputDirectory = Path.GetFullPath(Path.Combine(
                preflight.Root, "Usb2XchangeOutputs"));
            string rootPrefix = Path.GetFullPath(preflight.Root).
                TrimEnd('\\') + Path.DirectorySeparatorChar;
            if (!outputDirectory.StartsWith(rootPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Offline full-scan output escaped the private root.");
            }
            Directory.CreateDirectory(outputDirectory);
            RejectReparsePoints(outputDirectory);
            LaunchResult launch = StartTransport(preflight,
                "offline-replay", "aspi-offline-full-scan", null,
                replayIdentifier);
            string outputBase = Path.Combine(outputDirectory,
                string.Format(CultureInfo.InvariantCulture,
                    "offline-full-scan-60x60-{0}", launch.Process.Id));
            string requestedOutput = outputBase + ".tif";
            string normalizedOutput = outputBase + ".tiff";
            string defaultOutput = Path.Combine(outputDirectory,
                "Untitled0.tif");
            string normalizedDefaultOutput = Path.ChangeExtension(
                defaultOutput, ".tiff");
            if (File.Exists(requestedOutput) || File.Exists(normalizedOutput) ||
                File.Exists(defaultOutput) ||
                File.Exists(normalizedDefaultOutput))
            {
                CloseTestProcess(launch.Process);
                throw new InvalidOperationException(
                    "Offline full-scan output already exists; refusing to " +
                    "overwrite it.");
            }

            bool crashLogDeleted = false;
            bool registrationDeferred = false;
            bool frameVerified = false;
            bool previewInvoked = false;
            bool previewCompleted = false;
            bool scanInvoked = false;
            bool saveSubmitted = false;
            bool fullScanCompleted = false;
            bool progressDialogSeen = false;
            bool progressSampleSeen = false;
            bool progressReachedMaximum = false;
            bool progressClosedAfterSeen = false;
            int maximumObservedPercent = -1;
            string progressSource = string.Empty;
            string validatedOutput = null;
            try
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(360);
                while (DateTime.UtcNow < deadline &&
                    !launch.Process.HasExited)
                {
                    if (!crashLogDeleted &&
                        DeleteCrashNotificationIfPresent(launch.Process))
                    {
                        crashLogDeleted = true;
                        Console.WriteLine(
                            "Clicked only FlexColor bomblog notification > " +
                            "Delete Log, as requested; the report was not " +
                            "viewed or sent.");
                    }
                    if (!registrationDeferred &&
                        DeferRegistrationIfPresent(launch.Process))
                    {
                        registrationDeferred = true;
                        Console.WriteLine(
                            "Clicked only Registration > Register Later; " +
                            "no registration data was entered or sent.");
                    }
                    if (!frameVerified && FlexColorFrameSelector.TrySelectExact(
                            launch.Process, requestedFrameLabel))
                    {
                        frameVerified = true;
                        Console.WriteLine(
                            "Selected and re-verified exact Frame item: {0}",
                            requestedFrameLabel);
                    }
                    if (frameVerified && !previewInvoked &&
                        ClickPreviewIfPresent(launch.Process))
                    {
                        previewInvoked = true;
                        Console.WriteLine(
                            "Invoked the exact Preview button before the " +
                            "bounded offline full scan.");
                    }
                    if (File.Exists(launch.LogPath))
                    {
                        string text = ReadSharedText(launch.LogPath);
                        previewCompleted = text.IndexOf(
                            "cleanup requests without a WinUSB call; " +
                            "streams=1.", StringComparison.Ordinal) >= 0;
                        fullScanCompleted = text.IndexOf(
                            "cleanup requests without a WinUSB call; " +
                            "streams=2.", StringComparison.Ordinal) >= 0;
                    }
                    if (previewCompleted && !scanInvoked &&
                        ClickNormalButtonIfPresent(launch.Process, "Scan..."))
                    {
                        scanInvoked = true;
                        Console.WriteLine(
                            "Invoked the exact Scan... button after Preview " +
                            "completion.");
                    }
                    if (scanInvoked && !saveSubmitted &&
                        SubmitDefaultSaveIfPresent(launch.Process,
                            defaultOutput))
                    {
                        saveSubmitted = true;
                        Console.WriteLine(
                            "Accepted FlexColor's non-existing default TIFF " +
                            "target in the exact ignored output directory.");
                    }
                    if (scanInvoked && saveSubmitted)
                    {
                        FlexColorProgressObservation progress =
                            FlexColorProgressMonitor.TryRead(launch.Process);
                        progressDialogSeen |= progress.DialogVisible;
                        if (progressDialogSeen && !progress.DialogVisible)
                        {
                            progressClosedAfterSeen = true;
                        }
                        if (progress.SampleAvailable)
                        {
                            progressSampleSeen = true;
                            progressSource = progress.Source;
                            progressReachedMaximum |= progress.AtMaximum;
                            if (progress.Percent > maximumObservedPercent)
                            {
                                maximumObservedPercent = progress.Percent;
                                Console.WriteLine(
                                    "FlexColor full-scan progress: {0}% " +
                                    "({1}/{2}, source={3})",
                                    progress.Percent, progress.Position,
                                    progress.Maximum, progress.Source);
                            }
                        }
                    }
                    if (HasExactDialog(launch.Process, "Confirm Save As"))
                    {
                        throw new InvalidOperationException(
                            "Offline full-scan Save dialog requested an " +
                            "overwrite; no confirmation was sent.");
                    }
                    if (fullScanCompleted)
                    {
                        string candidate = File.Exists(normalizedDefaultOutput)
                            ? normalizedDefaultOutput
                            : defaultOutput;
                        if (File.Exists(candidate))
                        {
                            try
                            {
                                TiffOutputInspector.Inspect(candidate,
                                    748, 762, 300);
                                validatedOutput = candidate;
                                break;
                            }
                            catch (IOException)
                            {
                                // FlexColor may still own the output file.
                            }
                            catch (InvalidOperationException)
                            {
                                // A newly created TIFF may still be partial.
                            }
                        }
                    }
                    Thread.Sleep(100);
                }
            }
            finally
            {
                CloseTestProcess(launch.Process);
            }

            string actualOutput = null;
            if (validatedOutput != null && File.Exists(validatedOutput))
            {
                File.Move(validatedOutput, requestedOutput);
                actualOutput = requestedOutput;
            }
            if (!frameVerified || !previewInvoked || !previewCompleted ||
                !scanInvoked || !saveSubmitted || !fullScanCompleted ||
                !progressDialogSeen || !progressSampleSeen ||
                (!progressReachedMaximum && !progressClosedAfterSeen) ||
                actualOutput == null ||
                !File.Exists(actualOutput))
            {
                throw new InvalidOperationException(
                    "Offline full-scan replay stopped before its exact file " +
                    "and second cleanup boundary.");
            }
            OfflineFullScanLogMetadata scan = OfflineFullScanLogInspector.
                Inspect(ReadSharedText(launch.LogPath));
            if (!string.Equals(scan.SetWindowSha256,
                    OfflineFullScan60x60SetWindowSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Offline full-scan SET WINDOW fingerprint changed.");
            }
            TiffOutputMetadata tiff = TiffOutputInspector.Inspect(actualOutput,
                748, 762, 300);
            Console.WriteLine("ASPI log: {0}", launch.LogPath);
            Console.WriteLine("Full-scan output: {0}", actualOutput);
            Console.WriteLine("Full-scan output SHA-256: {0}", tiff.Sha256);
            Console.WriteLine("Scanner rows / TIFF pixels: {0} / {1}x{2}",
                scan.RowCount, tiff.Width, tiff.Height);
            Console.WriteLine("TIFF contract: RGB8, uncompressed, {0} ppi, " +
                "pixel bytes={1}", tiff.Ppi, tiff.PixelBytes);
            Console.WriteLine("WinUSB disabled: yes");
            Console.WriteLine("Progress UI: source={0}, maximum={1}%, " +
                "reached-max={2}, closed-after-seen={3}", progressSource,
                maximumObservedPercent, progressReachedMaximum,
                progressClosedAfterSeen);
            Console.WriteLine(
                "Offline full-scan replay completed Preview, one repeat " +
                "scan, both cleanup pairs, and the exact TIFF writer join.");
            return 0;
        }

        private static int SmokeOfflineOperatorRepeat(
            PreflightResult preflight, string requestedFrameLabel)
        {
            return SmokeOperatorRepeat(preflight, requestedFrameLabel,
                false);
        }

        private static int SmokeLiveOperatorRepeat(
            PreflightResult preflight, string requestedFrameLabel)
        {
            return SmokeOperatorRepeat(preflight, requestedFrameLabel,
                true);
        }

        private static int SmokeOperatorRepeat(PreflightResult preflight,
            string requestedFrameLabel, bool live)
        {
            const int transactionCount = 2;
            string previewCompletionMarker = live
                ? "LIVE ASPI exact post-image cleanup SET WINDOW " +
                    "completed: ordinal=2/2,"
                : "cleanup requests without a WinUSB call; streams=1.";
            string fullScanCompletionMarker = live
                ? "LIVE ASPI exact full-scan transport completed after " +
                    "998 rows and two cleanup SET WINDOW completions."
                : "cleanup requests without a WinUSB call; streams=2.";
            if (!string.Equals(requestedFrameLabel, "60x60",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    (live ? "The live" : "The offline") +
                    " operator repeat acceptance pins exact Frame " +
                    "60x60.");
            }

            PrintPreflight(preflight);
            string replayIdentifier = live ? null :
                FindLocalReplayIdentifier();
            if (!live)
            {
                Console.WriteLine(
                    "Compatible local device marker available: {0}",
                    replayIdentifier == null ? "no" : "yes");
            }
            string outputDirectory = Path.GetFullPath(Path.Combine(
                preflight.Root, "Usb2XchangeOutputs"));
            string rootPrefix = Path.GetFullPath(preflight.Root).
                TrimEnd('\\') + Path.DirectorySeparatorChar;
            if (!outputDirectory.StartsWith(rootPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    (live ? "Live" : "Offline") +
                    " operator output escaped the private root.");
            }
            Directory.CreateDirectory(outputDirectory);
            RejectReparsePoints(outputDirectory);

            LaunchResult launch = StartTransport(preflight,
                live ? "live-operator-session-60x60" :
                    "offline-operator-replay",
                live ? "aspi-live-operator-repeat-60x60" :
                    "aspi-offline-operator-repeat",
                replayIdentifier: replayIdentifier,
                offlineOperatorApproval: live ? null :
                    OfflineOperatorApprovalToken,
                operatorSessionApproval: live ?
                    OperatorSessionApprovalToken : null);
            var defaultOutputs = new string[transactionCount];
            var normalizedDefaultOutputs = new string[transactionCount];
            var outputs = new string[transactionCount];
            var progressSources = new string[transactionCount];
            var maximumPercents = new int[transactionCount];
            var progressReachedMaximum = new bool[transactionCount];
            var progressClosedAfterSeen = new bool[transactionCount];
            for (int index = 0; index < transactionCount; ++index)
            {
                outputs[index] = Path.Combine(outputDirectory,
                    string.Format(CultureInfo.InvariantCulture,
                        (live ? "live" : "offline") +
                        "-operator-repeat-60x60-{0}-{1}.tif",
                        launch.Process.Id, index + 1));
                defaultOutputs[index] = Path.Combine(outputDirectory,
                    string.Format(CultureInfo.InvariantCulture,
                        "Untitled{0}.tif", index));
                normalizedDefaultOutputs[index] = Path.ChangeExtension(
                    defaultOutputs[index], ".tiff");
                maximumPercents[index] = -1;
                if (File.Exists(outputs[index]) ||
                    File.Exists(defaultOutputs[index]) ||
                    File.Exists(normalizedDefaultOutputs[index]))
                {
                    CloseTestProcess(launch.Process);
                    throw new InvalidOperationException(
                        (live ? "Live" : "Offline") +
                        " operator output or exact numbered default " +
                        "already exists; refusing to overwrite it.");
                }
            }

            bool crashLogDeleted = false;
            bool registrationDeferred = false;
            bool frameVerified = false;
            bool previewInvoked = false;
            bool previewCompleted = false;
            bool scanInvoked = false;
            bool saveSubmitted = false;
            bool fullScanCompleted = false;
            bool progressDialogSeen = false;
            bool progressSampleSeen = false;
            bool currentProgressReachedMaximum = false;
            bool currentProgressClosedAfterSeen = false;
            int currentMaximumPercent = -1;
            string currentProgressSource = string.Empty;
            int completedTransactions = 0;
            try
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(
                    live ? 4800 : 600);
                while (DateTime.UtcNow < deadline &&
                    !launch.Process.HasExited &&
                    completedTransactions < transactionCount)
                {
                    if (!crashLogDeleted &&
                        DeleteCrashNotificationIfPresent(launch.Process))
                    {
                        crashLogDeleted = true;
                        Console.WriteLine(
                            "Clicked only FlexColor bomblog notification > " +
                            "Delete Log, as requested; the report was not " +
                            "viewed or sent.");
                    }
                    if (!registrationDeferred &&
                        DeferRegistrationIfPresent(launch.Process))
                    {
                        registrationDeferred = true;
                        Console.WriteLine(
                            "Clicked only Registration > Register Later; " +
                            "no registration data was entered or sent.");
                    }
                    if (!frameVerified &&
                        FlexColorFrameSelector.TrySelectExact(
                            launch.Process, requestedFrameLabel))
                    {
                        frameVerified = true;
                        Console.WriteLine(
                            "Transaction {0}/{1}: selected and re-verified " +
                            "exact Frame item {2}.", completedTransactions + 1,
                            transactionCount, requestedFrameLabel);
                    }
                    if (frameVerified && !previewInvoked &&
                        ClickPreviewIfPresent(launch.Process))
                    {
                        previewInvoked = true;
                        Console.WriteLine(
                            "Transaction {0}/{1}: invoked exact Preview.",
                            completedTransactions + 1, transactionCount);
                    }
                    if (File.Exists(launch.LogPath))
                    {
                        string text = ReadSharedText(launch.LogPath);
                        previewCompleted = CountTextMarker(text,
                            previewCompletionMarker) >=
                            completedTransactions + 1;
                        fullScanCompleted = CountTextMarker(text,
                            fullScanCompletionMarker) >=
                            completedTransactions + 1;
                    }
                    if (previewCompleted && !scanInvoked &&
                        ClickNormalButtonIfPresent(launch.Process, "Scan..."))
                    {
                        scanInvoked = true;
                        Console.WriteLine(
                            "Transaction {0}/{1}: invoked exact Scan... " +
                            "after Preview completion.",
                            completedTransactions + 1, transactionCount);
                    }
                    if (scanInvoked && !saveSubmitted &&
                        SubmitDefaultSaveIfPresent(launch.Process,
                            defaultOutputs[completedTransactions]))
                    {
                        saveSubmitted = true;
                        Console.WriteLine(
                            "Transaction {0}/{1}: accepted the exact " +
                            "non-existing default TIFF target.",
                            completedTransactions + 1, transactionCount);
                    }
                    if (scanInvoked && saveSubmitted)
                    {
                        FlexColorProgressObservation progress =
                            FlexColorProgressMonitor.TryRead(launch.Process);
                        progressDialogSeen |= progress.DialogVisible;
                        if (progressDialogSeen && !progress.DialogVisible)
                        {
                            currentProgressClosedAfterSeen = true;
                        }
                        if (progress.SampleAvailable)
                        {
                            progressSampleSeen = true;
                            currentProgressSource = progress.Source;
                            currentProgressReachedMaximum |= progress.AtMaximum;
                            if (progress.Percent > currentMaximumPercent)
                            {
                                currentMaximumPercent = progress.Percent;
                                Console.WriteLine(
                                    "Transaction {0}/{1} progress: {2}% " +
                                    "({3}/{4}, source={5})",
                                    completedTransactions + 1,
                                    transactionCount, progress.Percent,
                                    progress.Position, progress.Maximum,
                                    progress.Source);
                            }
                        }
                    }
                    if (HasExactDialog(launch.Process, "Confirm Save As"))
                    {
                        throw new InvalidOperationException(
                            (live ? "Live" : "Offline") +
                            " operator Save dialog requested an " +
                            "overwrite; no confirmation was sent.");
                    }

                    bool currentUiComplete = frameVerified &&
                        previewInvoked && previewCompleted && scanInvoked &&
                        saveSubmitted && fullScanCompleted &&
                        progressDialogSeen && progressSampleSeen &&
                        (currentProgressReachedMaximum ||
                         currentProgressClosedAfterSeen);
                    if (currentUiComplete)
                    {
                        string candidate =
                            File.Exists(normalizedDefaultOutputs[
                                completedTransactions])
                                ? normalizedDefaultOutputs[
                                    completedTransactions]
                                : defaultOutputs[completedTransactions];
                        if (File.Exists(candidate))
                        {
                            try
                            {
                                TiffOutputInspector.Inspect(candidate,
                                    (uint)(live ? 749 : 748), 762, 300);
                                File.Move(candidate,
                                    outputs[completedTransactions]);
                                progressSources[completedTransactions] =
                                    currentProgressSource;
                                maximumPercents[completedTransactions] =
                                    currentMaximumPercent;
                                progressReachedMaximum[
                                    completedTransactions] =
                                    currentProgressReachedMaximum;
                                progressClosedAfterSeen[
                                    completedTransactions] =
                                    currentProgressClosedAfterSeen;
                                ++completedTransactions;
                                Console.WriteLine(
                                    "Completed and isolated {0} operator " +
                                    "transaction {1}/{2}.",
                                    live ? "live" : "offline",
                                    completedTransactions, transactionCount);

                                frameVerified = false;
                                previewInvoked = false;
                                previewCompleted = false;
                                scanInvoked = false;
                                saveSubmitted = false;
                                fullScanCompleted = false;
                                progressDialogSeen = false;
                                progressSampleSeen = false;
                                currentProgressReachedMaximum = false;
                                currentProgressClosedAfterSeen = false;
                                currentMaximumPercent = -1;
                                currentProgressSource = string.Empty;
                            }
                            catch (IOException)
                            {
                                // FlexColor may still own the output file.
                            }
                            catch (InvalidOperationException)
                            {
                                // A newly created TIFF may still be partial.
                            }
                        }
                    }
                    Thread.Sleep(100);
                }
            }
            finally
            {
                CloseTestProcess(launch.Process);
                for (int index = 0; index < transactionCount; ++index)
                {
                    string[] incompleteCandidates = new string[]
                    {
                        defaultOutputs[index],
                        normalizedDefaultOutputs[index]
                    };
                    foreach (string incomplete in incompleteCandidates)
                    {
                        if (!File.Exists(incomplete))
                        {
                            continue;
                        }
                        string quarantine = outputs[index] +
                            Path.GetExtension(incomplete) + ".partial";
                        try
                        {
                            if (File.Exists(quarantine))
                            {
                                Console.Error.WriteLine(
                                    "Could not quarantine test-created " +
                                    "incomplete output because the exact " +
                                    "destination exists: " + quarantine);
                                continue;
                            }
                            File.Move(incomplete, quarantine);
                            Console.WriteLine("Quarantined test-created " +
                                "incomplete output: " + quarantine);
                        }
                        catch (IOException ex)
                        {
                            Console.Error.WriteLine(
                                "Could not quarantine test-created " +
                                "incomplete output: " + ex.Message);
                        }
                    }
                }
            }

            if (completedTransactions != transactionCount)
            {
                throw new InvalidOperationException(string.Format(
                    (live ? "Live" : "Offline") +
                    " operator replay completed {0}/{1} exact " +
                    "transactions before its boundary.",
                    completedTransactions, transactionCount));
            }
            string logText = ReadSharedText(launch.LogPath);
            int[] rowCounts;
            OfflineOperatorRepeatLogMetadata offlineReplay = null;
            if (live)
            {
                LiveOperatorRepeatLogMetadata liveReplay =
                    LiveOperatorRepeatLogInspector.Inspect(logText,
                        transactionCount);
                rowCounts = liveReplay.RowCounts;
            }
            else
            {
                offlineReplay = OfflineOperatorRepeatLogInspector.Inspect(
                    logText, transactionCount);
                rowCounts = offlineReplay.RowCounts;
            }
            Console.WriteLine("ASPI log: {0}", launch.LogPath);
            for (int index = 0; index < transactionCount; ++index)
            {
                if (!live && !string.Equals(
                        offlineReplay.SetWindowSha256[index],
                        OfflineFullScan60x60SetWindowSha256,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(string.Format(
                        "Offline operator transaction {0} SET WINDOW " +
                        "fingerprint changed.", index + 1));
                }
                TiffOutputMetadata tiff = TiffOutputInspector.Inspect(
                    outputs[index], (uint)(live ? 749 : 748), 762, 300);
                Console.WriteLine(
                    "Transaction {0}: rows={1}, output={2}", index + 1,
                    rowCounts[index], outputs[index]);
                Console.WriteLine(
                    "Transaction {0}: SHA-256={1}, TIFF={2}x{3} RGB8 " +
                    "uncompressed {4} ppi, progress={5}% ({6}), " +
                    "reached-max={7}, closed-after-seen={8}", index + 1,
                    tiff.Sha256, tiff.Width, tiff.Height, tiff.Ppi,
                    maximumPercents[index], progressSources[index],
                    progressReachedMaximum[index],
                    progressClosedAfterSeen[index]);
            }
            Console.WriteLine("WinUSB disabled: {0}", live ? "no" : "yes");
            Console.WriteLine(
                (live ? "Live" : "Offline") +
                " operator repeat replay completed two Preview, " +
                "Scan, cleanup, progress, and TIFF-save transactions in " +
                "one FlexColor process using two fresh bounded cycles.");
            return 0;
        }

        private static int CountTextMarker(string text, string marker)
        {
            int count = 0;
            int offset = 0;
            while ((offset = text.IndexOf(marker, offset,
                       StringComparison.Ordinal)) >= 0)
            {
                ++count;
                offset += marker.Length;
            }
            return count;
        }

        private static int SmokeOfflinePreviewCancel(
            PreflightResult preflight, string requestedFrameLabel)
        {
            if (!string.Equals(requestedFrameLabel, "60x60",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The offline cancellation replay pins exact Frame 60x60.");
            }
            PrintPreflight(preflight);
            LaunchResult launch = StartTransport(preflight,
                "offline-replay-cancel", "aspi-offline-preview-cancel",
                null, FindLocalReplayIdentifier(), null, null, null, null,
                null, null, null, null, null, null,
                OfflineCancelApprovalToken);
            bool crashLogDeleted = false;
            bool registrationDeferred = false;
            bool frameVerified = false;
            bool previewInvoked = false;
            bool stopInvoked = false;
            bool cancellationCleanupCompleted = false;
            bool recoveryFrameVerified = false;
            bool recoveryPreviewInvoked = false;
            bool recoveryCompleted = false;
            int cancellationRows = 0;
            int recoveryRows = 0;
            try
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(300);
                while (DateTime.UtcNow < deadline &&
                    !launch.Process.HasExited)
                {
                    if (!crashLogDeleted &&
                        DeleteCrashNotificationIfPresent(launch.Process))
                    {
                        crashLogDeleted = true;
                        Console.WriteLine(
                            "Clicked only FlexColor bomblog notification > " +
                            "Delete Log, as requested; the report was not " +
                            "viewed or sent.");
                    }
                    if (!registrationDeferred &&
                        DeferRegistrationIfPresent(launch.Process))
                    {
                        registrationDeferred = true;
                        Console.WriteLine(
                            "Clicked only Registration > Register Later; " +
                            "no registration data was entered or sent.");
                    }
                    if (!frameVerified &&
                        FlexColorFrameSelector.TrySelectExact(
                            launch.Process, requestedFrameLabel))
                    {
                        frameVerified = true;
                        Console.WriteLine(
                            "Selected and re-verified exact Frame item: {0}",
                            requestedFrameLabel);
                    }
                    if (frameVerified && !previewInvoked &&
                        ClickPreviewIfPresent(launch.Process))
                    {
                        previewInvoked = true;
                        Console.WriteLine(
                            "Invoked Preview in slowed WinUSB-disabled replay.");
                    }

                    if (File.Exists(launch.LogPath))
                    {
                        string text = ReadSharedText(launch.LogPath);
                        cancellationRows = Math.Max(cancellationRows,
                            LatestOfflineCancelRow(text, 1));
                        recoveryRows = Math.Max(recoveryRows,
                            LatestOfflineCancelRow(text, 2));
                        cancellationCleanupCompleted = text.IndexOf(
                            "cleanup requests without a WinUSB call; " +
                            "streams=1.", StringComparison.Ordinal) >= 0;
                        recoveryCompleted = text.IndexOf(
                            "cleanup requests without a WinUSB call; " +
                            "streams=2.", StringComparison.Ordinal) >= 0;
                    }

                    if (!stopInvoked &&
                        cancellationRows >= OfflineCancelTriggerRows)
                    {
                        if (cancellationRows > OfflineCancelMaximumRows)
                        {
                            throw new InvalidOperationException(
                                "The exact Stop control was not invoked " +
                                "before the offline cancellation row bound.");
                        }
                        if (ClickNormalButtonIfPresent(
                                launch.Process, "Stop"))
                        {
                            stopInvoked = true;
                            Console.WriteLine(
                                "Invoked the one normal-sized, visible, " +
                                "enabled Stop button after replay row {0}.",
                                cancellationRows);
                        }
                    }

                    if (cancellationCleanupCompleted &&
                        !recoveryFrameVerified &&
                        FlexColorFrameSelector.TrySelectExact(
                            launch.Process, requestedFrameLabel))
                    {
                        recoveryFrameVerified = true;
                        Console.WriteLine(
                            "Re-verified Frame {0} after cancellation.",
                            requestedFrameLabel);
                    }
                    if (cancellationCleanupCompleted &&
                        recoveryFrameVerified && !recoveryPreviewInvoked &&
                        ClickPreviewIfPresent(launch.Process))
                    {
                        recoveryPreviewInvoked = true;
                        Console.WriteLine(
                            "Invoked one healthy recovery Preview.");
                    }
                    if (recoveryCompleted)
                    {
                        break;
                    }
                    Thread.Sleep(50);
                }
            }
            finally
            {
                CloseTestProcess(launch.Process);
            }

            if (!frameVerified || !previewInvoked || !stopInvoked ||
                !cancellationCleanupCompleted ||
                cancellationRows < OfflineCancelTriggerRows ||
                cancellationRows > OfflineCancelMaximumRows ||
                !recoveryFrameVerified || !recoveryPreviewInvoked ||
                !recoveryCompleted || recoveryRows != 996)
            {
                throw new InvalidOperationException(string.Format(
                    "Offline cancellation/recovery mismatch: frame={0}, " +
                    "preview={1}, stop={2}, cancel-cleanup={3}, " +
                    "cancel-rows={4}, recovery-frame={5}, " +
                    "recovery-preview={6}, recovery-complete={7}, " +
                    "recovery-rows={8}.", frameVerified, previewInvoked,
                    stopInvoked, cancellationCleanupCompleted,
                    cancellationRows, recoveryFrameVerified,
                    recoveryPreviewInvoked, recoveryCompleted, recoveryRows));
            }
            Console.WriteLine("ASPI log: {0}", launch.LogPath);
            Console.WriteLine(
                "Offline cancellation/recovery passed: Stop after {0} rows, " +
                "two early cleanup windows, then 996-row healthy Preview; " +
                "WinUSB disabled.", cancellationRows);
            return 0;
        }

        private static int LatestOfflineCancelRow(string text, int stream)
        {
            int latest = 0;
            MatchCollection matches = Regex.Matches(text,
                "OFFLINE CANCEL REPLAY completed synthetic Preview image " +
                "row ([0-9]+): stream=([0-9]+),");
            foreach (Match match in matches)
            {
                int parsedStream;
                int parsedRow;
                if (int.TryParse(match.Groups[2].Value,
                        NumberStyles.None, CultureInfo.InvariantCulture,
                        out parsedStream) && parsedStream == stream &&
                    int.TryParse(match.Groups[1].Value,
                        NumberStyles.None, CultureInfo.InvariantCulture,
                        out parsedRow))
                {
                    latest = Math.Max(latest, parsedRow);
                }
            }
            return latest;
        }

        private static int SmokeLiveFullScanStartupFingerprint(
            PreflightResult preflight, string requestedFrameLabel)
        {
            return SmokeLiveFullScanBoundary(preflight, requestedFrameLabel,
                false, false, false, false, false, false);
        }

        private static int SmokeLiveFullScanSetWindowFirstRead(
            PreflightResult preflight, string requestedFrameLabel)
        {
            return SmokeLiveFullScanBoundary(preflight, requestedFrameLabel,
                true, false, false, false, false, false);
        }

        private static int SmokeLiveFullScanComplete(
            PreflightResult preflight, string requestedFrameLabel)
        {
            return SmokeLiveFullScanBoundary(preflight, requestedFrameLabel,
                true, true, false, false, false, false);
        }

        private static int SmokeLiveFullScanTerminalProbe(
            PreflightResult preflight, string requestedFrameLabel)
        {
            return SmokeLiveFullScanBoundary(preflight, requestedFrameLabel,
                true, false, true, false, false, false);
        }

        private static int SmokeLiveFullScanNaturalComplete(
            PreflightResult preflight, string requestedFrameLabel)
        {
            return SmokeLiveFullScanBoundary(preflight, requestedFrameLabel,
                true, true, false, true, false, false);
        }

        private static int SmokeLiveFullScanRow997Complete(
            PreflightResult preflight, string requestedFrameLabel)
        {
            return SmokeLiveFullScanBoundary(preflight, requestedFrameLabel,
                true, true, false, false, true, false);
        }

        private static int SmokeLiveFullScanProgressComplete(
            PreflightResult preflight, string requestedFrameLabel)
        {
            return SmokeLiveFullScanBoundary(preflight, requestedFrameLabel,
                true, true, false, false, false, true);
        }

        private static int SmokeLiveFullScanBoundary(
            PreflightResult preflight, string requestedFrameLabel,
            bool executeFullScanSetWindow, bool completeFullScan,
            bool observeTerminalProbe, bool naturalFullScan,
            bool row997FullScan, bool progressFullScan)
        {
            if ((naturalFullScan ? 1 : 0) + (row997FullScan ? 1 : 0) +
                    (progressFullScan ? 1 : 0) > 1)
            {
                throw new InvalidOperationException(
                    "Historical fixed-row and progress-bounded full-scan " +
                    "modes are mutually exclusive.");
            }
            if (!string.Equals(requestedFrameLabel, "60x60",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The live full-scan startup observer currently pins " +
                    "only exact Frame 60x60.");
            }
            PrintPreflight(preflight);
            string outputDirectory = Path.GetFullPath(Path.Combine(
                preflight.Root, "Usb2XchangeOutputs"));
            string rootPrefix = Path.GetFullPath(preflight.Root).
                TrimEnd('\\') + Path.DirectorySeparatorChar;
            if (!outputDirectory.StartsWith(rootPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Live full-scan observation output escaped the private " +
                    "root.");
            }
            Directory.CreateDirectory(outputDirectory);
            RejectReparsePoints(outputDirectory);
            LaunchResult launch = StartTransport(preflight,
                progressFullScan
                    ? "live-full-scan-progress-complete-60x60"
                    : row997FullScan
                    ? "live-full-scan-row997-complete-60x60"
                    : naturalFullScan
                    ? "live-full-scan-natural-996-complete-60x60"
                    : observeTerminalProbe
                    ? "live-full-scan-terminal-probe-60x60"
                    : completeFullScan
                    ? "live-full-scan-complete-60x60"
                    : executeFullScanSetWindow
                    ? "live-full-scan-set-window-first-read"
                    : "live-full-scan-startup-fingerprint",
                progressFullScan
                    ? "aspi-live-full-scan-progress-complete-60x60"
                    : row997FullScan
                    ? "aspi-live-full-scan-row997-complete-60x60"
                    : naturalFullScan
                    ? "aspi-live-full-scan-natural-996-complete-60x60"
                    : observeTerminalProbe
                    ? "aspi-live-full-scan-terminal-probe-60x60"
                    : completeFullScan
                    ? "aspi-live-full-scan-complete-60x60"
                    : executeFullScanSetWindow
                    ? "aspi-live-full-scan-set-window-first-read"
                    : "aspi-live-full-scan-startup-fingerprint", null, null,
                null, null, null, null, null, null, null, null, null, null,
                null, null,
                executeFullScanSetWindow ? null :
                    FullScanStartupFingerprintApprovalToken,
                executeFullScanSetWindow && !completeFullScan
                    && !observeTerminalProbe
                    ? FullScanSetWindowApprovalToken : null,
                completeFullScan && !naturalFullScan && !row997FullScan &&
                    !progressFullScan
                    ? FullScanCompleteApprovalToken : null,
                observeTerminalProbe
                    ? FullScanTerminalProbeApprovalToken : null,
                naturalFullScan
                    ? FullScanNaturalCompleteApprovalToken : null,
                row997FullScan
                    ? FullScanRow997CompleteApprovalToken : null,
                progressFullScan
                    ? FullScanProgressCompleteApprovalToken : null);
            string requestedOutput = Path.Combine(outputDirectory,
                string.Format(CultureInfo.InvariantCulture,
                    progressFullScan
                        ? "live-full-scan-progress-complete-60x60-{0}.tif"
                        : row997FullScan
                        ? "live-full-scan-row997-complete-60x60-{0}.tif"
                        : naturalFullScan
                        ? "live-full-scan-natural-996-complete-60x60-{0}.tif"
                        : observeTerminalProbe
                        ? "live-full-scan-terminal-probe-60x60-{0}.tif"
                        : completeFullScan
                        ? "live-full-scan-complete-60x60-{0}.tif"
                        : executeFullScanSetWindow
                        ? "live-full-scan-set-window-first-read-60x60-{0}.tif"
                        : "live-full-scan-startup-fingerprint-60x60-{0}.tif",
                    launch.Process.Id));
            string normalizedOutput = Path.ChangeExtension(requestedOutput,
                ".tiff");
            string defaultOutput = Path.Combine(outputDirectory,
                "Untitled0.tif");
            string normalizedDefaultOutput = Path.ChangeExtension(
                defaultOutput, ".tiff");
            if (File.Exists(requestedOutput) || File.Exists(normalizedOutput) ||
                File.Exists(defaultOutput) ||
                File.Exists(normalizedDefaultOutput))
            {
                CloseTestProcess(launch.Process);
                throw new InvalidOperationException(
                    "Live full-scan observation output already exists; " +
                    "refusing to overwrite it.");
            }

            bool crashLogDeleted = false;
            bool registrationDeferred = false;
            bool frameVerified = false;
            bool previewInvoked = false;
            bool previewCompleted = false;
            bool scanInvoked = false;
            bool saveSubmitted = false;
            bool loaderIdentity = false;
            bool progressDialogSeen = false;
            bool progressSampleSeen = false;
            bool progressReachedMaximum = false;
            bool progressClosedAfterSeen = false;
            int maximumObservedPercent = -1;
            string progressSource = string.Empty;
            PreviewObserveLogMetadata preview = null;
            FullScanStartupFingerprintLogMetadata fingerprint = null;
            FullScanCompletionLogMetadata completion = null;
            FullScanTerminalProbeLogMetadata terminalProbe = null;
            string validatedOutput = null;
            try
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(
                    completeFullScan || observeTerminalProbe ? 2400 : 480);
                while (DateTime.UtcNow < deadline &&
                    !launch.Process.HasExited)
                {
                    if (!crashLogDeleted &&
                        DeleteCrashNotificationIfPresent(launch.Process))
                    {
                        crashLogDeleted = true;
                        Console.WriteLine(
                            "Clicked only FlexColor bomblog notification > " +
                            "Delete Log; the report was not viewed or sent.");
                    }
                    if (!registrationDeferred &&
                        DeferRegistrationIfPresent(launch.Process))
                    {
                        registrationDeferred = true;
                        Console.WriteLine(
                            "Clicked only Registration > Register Later; " +
                            "no registration data was entered or sent.");
                    }
                    if (!frameVerified && FlexColorFrameSelector.TrySelectExact(
                            launch.Process, requestedFrameLabel))
                    {
                        frameVerified = true;
                        Console.WriteLine(
                            "Selected and re-verified exact Frame item: {0}",
                            requestedFrameLabel);
                    }
                    if (frameVerified && !previewInvoked &&
                        ClickPreviewIfPresent(launch.Process))
                    {
                        previewInvoked = true;
                        Console.WriteLine(
                            "Invoked exact Preview before the guarded " +
                            "full-scan successor observation.");
                    }
                    if (progressFullScan && scanInvoked && saveSubmitted)
                    {
                        FlexColorProgressObservation progress =
                            FlexColorProgressMonitor.TryRead(launch.Process);
                        progressDialogSeen |= progress.DialogVisible;
                        if (progressDialogSeen && !progress.DialogVisible)
                        {
                            progressClosedAfterSeen = true;
                        }
                        if (progress.SampleAvailable)
                        {
                            progressSampleSeen = true;
                            progressSource = progress.Source;
                            progressReachedMaximum |= progress.AtMaximum;
                            if (progress.Percent > maximumObservedPercent)
                            {
                                maximumObservedPercent = progress.Percent;
                                Console.WriteLine(
                                    "FlexColor full-scan progress: {0}% " +
                                    "({1}/{2}, source={3})",
                                    progress.Percent, progress.Position,
                                    progress.Maximum, progress.Source);
                            }
                        }
                    }
                    if (File.Exists(launch.LogPath))
                    {
                        string text = ReadSharedText(launch.LogPath);
                        loaderIdentity = text.IndexOf(
                            "LIVE ASPI target-5 identity: Imacon/" +
                            "SCSI Loader/L302, type=0x06, " +
                            "classification=Loader.",
                            StringComparison.Ordinal) >= 0;
                        preview = PreviewObserveLogInspector.Inspect(text);
                        previewCompleted =
                            preview.ShortRetryStreamImageReadCompleted &&
                            preview.ShortRetryStreamImageReadCount ==
                                PreviewPoweredNaturalRows &&
                            preview.CleanupCompletionCount == 2 &&
                            preview.CleanupCompletionsExact &&
                            !preview.CleanupTransportFailed;
                        fingerprint =
                            FullScanStartupFingerprintLogInspector.
                                Inspect(text);
                        completion = progressFullScan
                            ? FullScanCompletionLogInspector.
                                InspectProgressBounded(text, 998, 998)
                            : FullScanCompletionLogInspector.
                                Inspect(text, row997FullScan ? 997 :
                                    naturalFullScan ? 996 : 762);
                        terminalProbe = FullScanTerminalProbeLogInspector.
                            Inspect(text);
                        if (loaderIdentity ||
                            (fingerprint != null &&
                             ((!completeFullScan &&
                               (executeFullScanSetWindow
                                ? fingerprint.FirstImageReadObserved
                                : fingerprint.
                                    HasExclusiveTerminalFingerprint)) ||
                              fingerprint.
                                  FailureBeforeTerminalFingerprint)))
                        {
                            break;
                        }
                        if (completeFullScan && completion != null &&
                            completion.TransportCompleted)
                        {
                            string candidate = File.Exists(
                                normalizedDefaultOutput)
                                ? normalizedDefaultOutput : defaultOutput;
                            if (File.Exists(candidate))
                            {
                                try
                                {
                                    TiffOutputInspector.Inspect(candidate,
                                        749, 762, 300);
                                    validatedOutput = candidate;
                                    if (!progressFullScan ||
                                        progressReachedMaximum ||
                                        progressClosedAfterSeen)
                                    {
                                        break;
                                    }
                                }
                                catch (IOException)
                                {
                                }
                                catch (InvalidOperationException)
                                {
                                }
                            }
                        }
                        if (observeTerminalProbe && terminalProbe != null)
                        {
                            break;
                        }
                    }
                    if (previewCompleted && !scanInvoked &&
                        ClickNormalButtonIfPresent(launch.Process, "Scan..."))
                    {
                        scanInvoked = true;
                        Console.WriteLine(
                            "Invoked exact Scan... after powered Preview " +
                            "cleanup completion.");
                    }
                    if (scanInvoked && !saveSubmitted &&
                        SubmitDefaultSaveIfPresent(launch.Process,
                            defaultOutput))
                    {
                        saveSubmitted = true;
                        Console.WriteLine(
                            "Accepted FlexColor's non-existing default TIFF " +
                            "target in the exact ignored output directory.");
                    }
                    if (HasExactDialog(launch.Process, "Confirm Save As"))
                    {
                        throw new InvalidOperationException(
                            "The live full-scan Save dialog requested an " +
                            "overwrite; no confirmation was sent.");
                    }
                    Thread.Sleep(50);
                }
            }
            finally
            {
                CloseTestProcess(launch.Process);
            }

            string generatedOutput = File.Exists(normalizedDefaultOutput)
                ? normalizedDefaultOutput : defaultOutput;
            if (File.Exists(generatedOutput))
            {
                File.Move(generatedOutput, requestedOutput);
                if (validatedOutput != null)
                {
                    validatedOutput = requestedOutput;
                }
            }

            if (File.Exists(launch.LogPath))
            {
                string text = ReadSharedText(launch.LogPath);
                preview = PreviewObserveLogInspector.Inspect(text);
                fingerprint = FullScanStartupFingerprintLogInspector.
                    Inspect(text);
                completion = progressFullScan
                    ? FullScanCompletionLogInspector.InspectProgressBounded(
                        text, 998, 998)
                    : FullScanCompletionLogInspector.Inspect(text,
                        row997FullScan ? 997 : naturalFullScan ? 996 : 762);
                terminalProbe = FullScanTerminalProbeLogInspector.
                    Inspect(text);
            }
            bool previewExact = preview != null && preview.Support &&
                preview.Identity && preview.SetWindowCompleted &&
                preview.ShortRetryStreamImageReadCompleted &&
                preview.ShortRetryStreamImageReadCount ==
                    PreviewPoweredNaturalRows &&
                preview.CleanupCompletionCount == 2 &&
                preview.CleanupCompletionsExact &&
                !preview.CleanupTransportFailed;
            bool commonFingerprintExact = fingerprint != null &&
                fingerprint.Armed && fingerprint.InquiryObserved &&
                fingerprint.InitialScannerReadyAttempts > 0 &&
                fingerprint.InitialScannerReadyExact &&
                fingerprint.InitialScannerReadyCompleted &&
                fingerprint.ExtraScannerReadyExact;
            bool fingerprintExact = commonFingerprintExact &&
                (completeFullScan || observeTerminalProbe
                    ? fingerprint.FullScanSetWindowCompleted &&
                      string.Equals(fingerprint.CompletedSetWindowSha256,
                          FullScanSetWindowSha256,
                          StringComparison.Ordinal) &&
                      !fingerprint.FirstImageReadObserved &&
                      !fingerprint.HasExclusiveTerminalFingerprint
                    : executeFullScanSetWindow
                    ? fingerprint.FullScanSetWindowCompleted &&
                      string.Equals(fingerprint.CompletedSetWindowSha256,
                          FullScanSetWindowSha256,
                          StringComparison.Ordinal) &&
                      fingerprint.FirstImageReadObserved &&
                      fingerprint.FirstImageWidth > 0 &&
                      fingerprint.FirstImageLength == checked(
                          fingerprint.FirstImageWidth * 6) &&
                      fingerprint.FirstImageSelector == 0x28 &&
                      !fingerprint.HasExclusiveTerminalFingerprint
                    : fingerprint.HasExclusiveTerminalFingerprint &&
                      (!fingerprint.StartupWriteObserved ||
                       fingerprint.ExtraScannerReadyCompleted));
            fingerprintExact &= completeFullScan || observeTerminalProbe ||
                !fingerprint.FailureBeforeTerminalFingerprint;
            int expectedFullScanRows = progressFullScan ? 998 :
                row997FullScan ? 997 :
                naturalFullScan ? 996 : 762;
            int expectedMaximumSubmissions = progressFullScan ? 8982 :
                row997FullScan ? 8973 :
                naturalFullScan ? 8964 : 6858;
            int expectedMaximumShortRetries = progressFullScan ? 7984 :
                row997FullScan ? 7976 :
                naturalFullScan ? 7968 : 6096;
            bool streamExact = completion != null &&
                completion.Rows == expectedFullScanRows &&
                completion.RowLimit == expectedFullScanRows &&
                completion.Submissions ==
                    completion.Rows + completion.ShortRetries &&
                completion.MaximumSubmissions == expectedMaximumSubmissions &&
                completion.ShortRetries >= 0 &&
                completion.ShortRetries <= completion.MaximumShortRetries &&
                completion.MaximumShortRetries ==
                    expectedMaximumShortRetries &&
                completion.InStreamInquiryRevalidationsExact;
            bool completionExact = !completeFullScan ||
                completion != null && completion.TransportCompleted &&
                !completion.FailureBeforeCompletion &&
                streamExact &&
                completion.FirstCleanup && completion.SecondCleanup &&
                completion.CleanupExact &&
                (!progressFullScan ||
                    progressDialogSeen && progressSampleSeen &&
                    (progressReachedMaximum ||
                        progressClosedAfterSeen)) &&
                validatedOutput != null &&
                File.Exists(validatedOutput);
            bool terminalProbeExact = !observeTerminalProbe ||
                streamExact && terminalProbe != null &&
                terminalProbe.Row == 763 && terminalProbe.Requested == 4494 &&
                terminalProbe.Width == 749 && terminalProbe.Selector == 0x28 &&
                terminalProbe.Submission == completion.Submissions + 1 &&
                string.Equals(terminalProbe.Cdb,
                    "28 00 00 00 28 00 00 11 8E 00",
                    StringComparison.Ordinal) &&
                terminalProbe.LengthConsistent &&
                terminalProbe.PayloadSha256 != null &&
                terminalProbe.PayloadSha256.Length == 64 &&
                !completion.FirstCleanup && !completion.SecondCleanup &&
                !completion.TransportCompleted;
            if (loaderIdentity || !frameVerified || !previewInvoked ||
                !previewExact || !scanInvoked || !saveSubmitted)
            {
                throw new InvalidOperationException(
                    "Live full-scan setup did not complete its exact Frame, " +
                    "Preview, cleanup, Scan, and save-path prerequisites.");
            }
            if (!fingerprintExact)
            {
                throw new InvalidOperationException(
                    "Live full-scan startup did not match its exact " +
                    "INQUIRY, readiness, SET WINDOW, and first-read boundary.");
            }
            if (!completionExact)
            {
                throw new InvalidOperationException(
                    progressFullScan
                        ? "Live full-scan completion did not reach the " +
                          "observed progress UI, exact 998-request stream, " +
                          "natural cleanup, and TIFF contract."
                        : "Live full-scan completion did not reach its exact " +
                          expectedFullScanRows + "-row, bounded-revalidation, " +
                          "cleanup, and TIFF contract.");
            }
            if (!terminalProbeExact)
            {
                throw new InvalidOperationException(
                    "Live full-scan terminal probing did not observe exactly " +
                    "one quarantined row-763 READ after the 762-row stream.");
            }

            Console.WriteLine("ASPI log: {0}", launch.LogPath);
            Console.WriteLine("Preview rows / cleanup completions: {0} / {1}",
                preview.ShortRetryStreamImageReadCount,
                preview.CleanupCompletionCount);
            Console.WriteLine("Repeat operational INQUIRY observed: {0}",
                fingerprint.InquiryObserved);
            Console.WriteLine("Initial ScannerReady attempts / non-ready: " +
                "{0} / {1}", fingerprint.InitialScannerReadyAttempts,
                fingerprint.InitialScannerReadyNotReadyCount);
            if (observeTerminalProbe)
            {
                Console.WriteLine("Full-scan SET WINDOW SHA-256: {0}",
                    fingerprint.CompletedSetWindowSha256);
                Console.WriteLine("Full-scan accepted rows / submissions / " +
                    "retries: {0} / {1} / {2}", completion.Rows,
                    completion.Submissions, completion.ShortRetries);
                Console.WriteLine("Terminal probe status / actual / residue: " +
                    "0x{0:X2} / {1} / {2}", terminalProbe.Status,
                    terminalProbe.Actual, terminalProbe.Residue);
                Console.WriteLine("Terminal probe payload SHA-256: {0}",
                    terminalProbe.PayloadSha256);
                Console.WriteLine(
                    "The single row-763 result was quarantined and not " +
                    "returned to FlexColor; all successors were blocked.");
            }
            else if (completeFullScan)
            {
                TiffOutputMetadata tiff = TiffOutputInspector.Inspect(
                    validatedOutput, 749, 762, 300);
                Console.WriteLine("Full-scan SET WINDOW SHA-256: {0}",
                    fingerprint.CompletedSetWindowSha256);
                Console.WriteLine("Full-scan rows / submissions / retries: " +
                    "{0} / {1} / {2}", completion.Rows,
                    completion.Submissions, completion.ShortRetries);
                Console.WriteLine("Full-scan in-stream M333 " +
                    "revalidations: {0}/{1}",
                    completion.InStreamInquiryRevalidations,
                    progressFullScan ? 34 :
                        naturalFullScan || row997FullScan ? 34 : 26);
                if (progressFullScan)
                {
                    Console.WriteLine("Progress UI: source={0}, maximum={1}%, " +
                        "reached-max={2}, closed-after-seen={3}",
                        progressSource, maximumObservedPercent,
                        progressReachedMaximum, progressClosedAfterSeen);
                }
                Console.WriteLine("Full-scan cleanup completions: 2/2");
                Console.WriteLine("Full-scan output: {0}", validatedOutput);
                Console.WriteLine("Full-scan output SHA-256: {0}",
                    tiff.Sha256);
                Console.WriteLine("TIFF contract: {0}x{1}, RGB8, " +
                    "uncompressed, {2} ppi, pixel bytes={3}", tiff.Width,
                    tiff.Height, tiff.Ppi, tiff.PixelBytes);
            }
            else if (executeFullScanSetWindow)
            {
                Console.WriteLine("Full-scan SET WINDOW SHA-256: {0}",
                    fingerprint.CompletedSetWindowSha256);
                Console.WriteLine("First full-scan image READ: width={0}, " +
                    "length={1}, selector=0x{2:X2}, CDB={3}",
                    fingerprint.FirstImageWidth,
                    fingerprint.FirstImageLength,
                    fingerprint.FirstImageSelector,
                    fingerprint.FirstImageCdb);
                Console.WriteLine(
                    "The hash-pinned full-scan SET WINDOW completed once; " +
                    "the first image READ was fingerprinted and blocked " +
                    "before USB.");
            }
            else
            {
                Console.WriteLine("Extra ScannerReady attempts / " +
                    "non-ready: {0} / {1}",
                    fingerprint.ExtraScannerReadyAttempts,
                    fingerprint.ExtraScannerReadyNotReadyCount);
                Console.WriteLine("Guarded successor: {0}",
                    fingerprint.StartupWriteObserved
                        ? "startup WRITE BUFFER OUT 1,082"
                        : "second SET WINDOW OUT 84");
                Console.WriteLine("Guarded payload SHA-256: {0}",
                    fingerprint.StartupWriteSha256 ??
                        fingerprint.SetWindowSha256);
                Console.WriteLine(
                    "The guarded successor was fingerprinted and blocked " +
                    "before USB; no full-scan SET WINDOW or image READ was " +
                    "authorized.");
            }
            return 0;
        }

        private static int SmokeLivePreview(PreflightResult preflight,
            string requestedFrameLabel, int permittedImageReads)
        {
            return SmokeLivePreview(preflight, requestedFrameLabel,
                permittedImageReads, false);
        }

        private static int SmokeLivePreview(PreflightResult preflight,
            string requestedFrameLabel, int permittedImageReads,
            bool permitShortRetryStream)
        {
            return SmokeLivePreview(preflight, requestedFrameLabel,
                permittedImageReads, permitShortRetryStream, false);
        }

        private static int SmokeLivePreview(PreflightResult preflight,
            string requestedFrameLabel, int permittedImageReads,
            bool permitShortRetryStream,
            bool permitNaturalPerRowRetryStream)
        {
            return SmokeLivePreview(preflight, requestedFrameLabel,
                permittedImageReads, permitShortRetryStream,
                permitNaturalPerRowRetryStream, false);
        }

        private static int SmokeLivePreview(PreflightResult preflight,
            string requestedFrameLabel, int permittedImageReads,
            bool permitShortRetryStream,
            bool permitNaturalPerRowRetryStream,
            bool permitPoweredNaturalStream)
        {
            return SmokeLivePreview(preflight, requestedFrameLabel,
                permittedImageReads, permitShortRetryStream,
                permitNaturalPerRowRetryStream, permitPoweredNaturalStream,
                false);
        }

        private static int SmokeLivePreview(PreflightResult preflight,
            string requestedFrameLabel, int permittedImageReads,
            bool permitShortRetryStream,
            bool permitNaturalPerRowRetryStream,
            bool permitPoweredNaturalStream,
            bool permitPoweredCleanup)
        {
            return SmokeLivePreview(preflight, requestedFrameLabel,
                permittedImageReads, permitShortRetryStream,
                permitNaturalPerRowRetryStream, permitPoweredNaturalStream,
                permitPoweredCleanup, false);
        }

        private static int SmokeLivePreview(PreflightResult preflight,
            string requestedFrameLabel, int permittedImageReads,
            bool permitShortRetryStream,
            bool permitNaturalPerRowRetryStream,
            bool permitPoweredNaturalStream,
            bool permitPoweredCleanup,
            bool permitPoweredCancellation)
        {
            return SmokeLivePreview(preflight, requestedFrameLabel,
                permittedImageReads, permitShortRetryStream,
                permitNaturalPerRowRetryStream, permitPoweredNaturalStream,
                permitPoweredCleanup, permitPoweredCancellation, false,
                false, false);
        }

        private static int SmokeLive24x36PreviewSetWindow(
            PreflightResult preflight, string requestedFrameLabel)
        {
            return SmokeLivePreview(preflight, requestedFrameLabel, 0,
                false, false, false, false, false, true, false, false);
        }

        private static int SmokeLive24x36PreviewFirstRead(
            PreflightResult preflight, string requestedFrameLabel)
        {
            return SmokeLivePreview(preflight, requestedFrameLabel, 1,
                false, false, false, false, false, false, true, false);
        }

        private static int SmokeLive4x5PreviewFirstRead(
            PreflightResult preflight, string requestedFrameLabel)
        {
            return SmokeLivePreview(preflight, requestedFrameLabel, 1,
                false, false, false, false, false, false, false, true);
        }

        private static int SmokeLivePreview(PreflightResult preflight,
            string requestedFrameLabel, int permittedImageReads,
            bool permitShortRetryStream,
            bool permitNaturalPerRowRetryStream,
            bool permitPoweredNaturalStream,
            bool permitPoweredCleanup,
            bool permitPoweredCancellation,
            bool permit24x36SetWindowObservation,
            bool permit24x36FirstRead,
            bool permit4x5FirstRead)
        {
            if (permit24x36SetWindowObservation &&
                (requestedFrameLabel != "24x36" || permittedImageReads != 0 ||
                 permitShortRetryStream || permitNaturalPerRowRetryStream ||
                 permitPoweredNaturalStream || permitPoweredCleanup ||
                 permitPoweredCancellation || permit4x5FirstRead))
            {
                throw new ArgumentException(
                    "The powered 24x36 SET WINDOW observer requires exact " +
                    "Frame 24x36 and grants no image or cleanup authority.",
                    "permit24x36SetWindowObservation");
            }
            if (permit24x36FirstRead &&
                (requestedFrameLabel != "24x36" ||
                 permittedImageReads != 1 || permitShortRetryStream ||
                 permitNaturalPerRowRetryStream || permitPoweredNaturalStream ||
                 permitPoweredCleanup || permitPoweredCancellation ||
                 permit24x36SetWindowObservation || permit4x5FirstRead))
            {
                throw new ArgumentException(
                    "The powered 24x36 first-read observer requires exact " +
                    "Frame 24x36 and grants exactly one image READ with no " +
                    "cleanup authority.", "permit24x36FirstRead");
            }
            if (permit4x5FirstRead &&
                (requestedFrameLabel != "4\"x5\"" ||
                 permittedImageReads != 1 || permitShortRetryStream ||
                 permitNaturalPerRowRetryStream || permitPoweredNaturalStream ||
                 permitPoweredCleanup || permitPoweredCancellation ||
                 permit24x36SetWindowObservation || permit24x36FirstRead))
            {
                throw new ArgumentException(
                    "The powered 4x5 first-read observer requires exact " +
                    "Frame 4\"x5\" and grants exactly one image READ with no " +
                    "cleanup authority.", "permit4x5FirstRead");
            }
            bool permitFirstImageRead = permittedImageReads == 1;
            bool permitImageBurst = permittedImageReads == PreviewBurstRows;
            bool permitImageStream = permittedImageReads == PreviewStreamRows &&
                !permitShortRetryStream;
            bool permitImageShortRetryStream =
                permittedImageReads == PreviewStreamRows &&
                permitShortRetryStream;
            bool permitLegacyNaturalImageStream =
                permittedImageReads == PreviewNaturalRows &&
                permitShortRetryStream &&
                !permitNaturalPerRowRetryStream &&
                !permitPoweredNaturalStream;
            bool permitNaturalPerRowImageStream =
                permittedImageReads == PreviewNaturalRows &&
                permitShortRetryStream && permitNaturalPerRowRetryStream &&
                !permitPoweredNaturalStream;
            bool permitPoweredNaturalImageStream =
                permittedImageReads == PreviewPoweredNaturalRows &&
                permitShortRetryStream && !permitNaturalPerRowRetryStream &&
                permitPoweredNaturalStream;
            bool permitNaturalImageStream =
                permitLegacyNaturalImageStream ||
                permitNaturalPerRowImageStream ||
                permitPoweredNaturalImageStream;
            if (permitNaturalPerRowRetryStream &&
                !permitNaturalPerRowImageStream)
            {
                throw new ArgumentException(
                    "Per-row natural retry requires the exact 996-row " +
                    "short-retry policy.",
                    "permitNaturalPerRowRetryStream");
            }
            if (permitPoweredNaturalStream &&
                !permitPoweredNaturalImageStream)
            {
                throw new ArgumentException(
                    "Powered natural observation requires the exact " +
                    "911-row short-retry policy.",
                    "permitPoweredNaturalStream");
            }
            if (permitPoweredCleanup &&
                !permitPoweredNaturalImageStream)
            {
                throw new ArgumentException(
                    "Powered cleanup requires the exact powered 911-row " +
                    "short-retry policy.", "permitPoweredCleanup");
            }
            if (permitPoweredCancellation &&
                (!permitPoweredNaturalImageStream || permitPoweredCleanup))
            {
                throw new ArgumentException(
                    "Powered cancellation requires the exact powered " +
                    "911-row short-retry policy and separate cleanup " +
                    "authority.", "permitPoweredCancellation");
            }
            if (permittedImageReads != 0 && !permitFirstImageRead &&
                !permitImageBurst && !permitImageStream &&
                !permitImageShortRetryStream && !permitNaturalImageStream)
            {
                throw new ArgumentOutOfRangeException("permittedImageReads");
            }
            PrintPreflight(preflight);
            LaunchResult launch = StartTransport(preflight,
                permit4x5FirstRead
                    ? "live-preview-first-read-4x5"
                : permit24x36FirstRead
                    ? "live-preview-first-read-24x36"
                : permit24x36SetWindowObservation
                    ? "live-preview-set-window-24x36-observe"
                : permitPoweredCancellation
                    ? "live-preview-powered-cancel-32-96-cleanup-2"
                : permitPoweredCleanup
                    ? "live-preview-powered-natural-911-cleanup-2"
                : permitPoweredNaturalImageStream
                    ? "live-preview-powered-natural-911-observe"
                : permitNaturalPerRowImageStream
                    ? "live-preview-natural-996-per-row-8"
                : permitLegacyNaturalImageStream
                    ? "live-preview-natural-996-short-retry"
                : permitImageShortRetryStream
                    ? "live-preview-stream-256-short-retry"
                    : permitImageStream
                    ? "live-preview-stream-256"
                    : permitImageBurst
                    ? "live-preview-burst-8"
                    : permitFirstImageRead
                        ? "live-preview-first-read"
                        : "live-preview-observe",
                permit4x5FirstRead
                    ? "aspi-live-preview-first-read-4x5-smoke"
                : permit24x36FirstRead
                    ? "aspi-live-preview-first-read-24x36-smoke"
                : permit24x36SetWindowObservation
                    ? "aspi-live-preview-set-window-24x36-observe-smoke"
                : permitPoweredCancellation
                    ? "aspi-live-preview-powered-cancel-32-96-cleanup-2-smoke"
                : permitPoweredCleanup
                    ? "aspi-live-preview-powered-natural-911-cleanup-2-smoke"
                : permitPoweredNaturalImageStream
                    ? "aspi-live-preview-powered-natural-911-observe-smoke"
                : permitNaturalPerRowImageStream
                    ? "aspi-live-preview-natural-996-per-row-8-smoke"
                : permitLegacyNaturalImageStream
                    ? "aspi-live-preview-natural-996-short-retry-smoke"
                : permitImageShortRetryStream
                    ? "aspi-live-preview-stream-256-short-retry-smoke"
                    : permitImageStream
                    ? "aspi-live-preview-stream-256-smoke"
                    : permitImageBurst
                    ? "aspi-live-preview-burst-8-smoke"
                    : permitFirstImageRead
                        ? "aspi-live-preview-first-read-smoke"
                        : "aspi-live-preview-observe-smoke",
                null, null,
                permittedImageReads == 0 &&
                    !permit24x36SetWindowObservation
                    ? PreviewApprovalToken : null,
                null,
                permitFirstImageRead && !permit24x36FirstRead &&
                    !permit4x5FirstRead
                    ? PreviewFirstReadApprovalToken
                    : null,
                permitImageBurst ? PreviewBurstApprovalToken : null,
                permitImageStream ? PreviewStreamApprovalToken : null,
                permitImageShortRetryStream
                    ? PreviewShortRetryApprovalToken
                    : null,
                permitLegacyNaturalImageStream
                    ? PreviewNaturalApprovalToken
                    : null,
                permitNaturalPerRowImageStream
                    ? PreviewNaturalPerRowApprovalToken
                    : null,
                permitPoweredNaturalImageStream
                    ? (permitPoweredCleanup || permitPoweredCancellation
                        ? null
                        : PreviewPoweredNaturalApprovalToken)
                    : null,
                permitPoweredCleanup
                    ? PreviewPoweredCleanupApprovalToken
                    : null,
                null,
                permitPoweredCancellation
                    ? PreviewPoweredCancellationApprovalToken
                    : null,
                preview24x36SetWindowApproval:
                    permit24x36SetWindowObservation
                        ? Preview24x36SetWindowApprovalToken
                        : null,
                preview24x36FirstReadApproval:
                    permit24x36FirstRead
                        ? Preview24x36FirstReadApprovalToken
                        : null,
                preview4x5FirstReadApproval:
                    permit4x5FirstRead
                        ? Preview4x5FirstReadApprovalToken
                        : null);
            bool support = false;
            bool identity = false;
            bool loaderIdentity = false;
            bool frameVerified = false;
            bool previewInvoked = false;
            bool setWindowCompleted = false;
            bool predictedReadBlocked = false;
            bool manifestRejected = false;
            bool transportRejected = false;
            bool candidateValidated = false;
            bool structuredMetadataCaptured = false;
            bool postWindowNotReadyObserved = false;
            bool postWindowRetryLimitReached = false;
            bool firstImageReadCompleted = false;
            bool crashLogDeleted = false;
            bool registrationDeferred = false;
            bool stopInvoked = false;
            string payloadSha256 = null;
            PreviewObserveLogMetadata finalObserved = null;
            DateTime naturalTerminalObservedAt = DateTime.MinValue;
            try
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(
                    permitNaturalImageStream ? 420 : 300);
                while (DateTime.UtcNow < deadline && !launch.Process.HasExited)
                {
                    if (!crashLogDeleted &&
                        DeleteCrashNotificationIfPresent(launch.Process))
                    {
                        crashLogDeleted = true;
                        Console.WriteLine(
                            "Clicked only FlexColor bomblog notification > " +
                            "Delete Log, as requested; the report was not " +
                            "viewed or sent.");
                    }
                    if (!registrationDeferred &&
                        DeferRegistrationIfPresent(launch.Process))
                    {
                        registrationDeferred = true;
                        Console.WriteLine(
                            "Clicked only Registration > Register Later; " +
                            "no registration data was entered or sent.");
                    }
                    if (!frameVerified && FlexColorFrameSelector.TrySelectExact(
                            launch.Process, requestedFrameLabel))
                    {
                        frameVerified = true;
                        Console.WriteLine(
                            "Selected and re-verified exact Frame item: {0}",
                            requestedFrameLabel);
                    }
                    if (frameVerified && !previewInvoked &&
                        ClickPreviewIfPresent(launch.Process))
                    {
                        previewInvoked = true;
                        Console.WriteLine(
                            "Invoked the one normal-sized, visible, enabled " +
                            "Preview button.");
                    }
                    if (File.Exists(launch.LogPath))
                    {
                        string text = ReadSharedText(launch.LogPath);
                        PreviewObserveLogMetadata observed =
                            PreviewObserveLogInspector.Inspect(text);
                        finalObserved = observed;
                        support = observed.Support;
                        identity = observed.Identity;
                        loaderIdentity = text.IndexOf(
                            "LIVE ASPI target-5 identity: Imacon/" +
                            "SCSI Loader/L302, type=0x06, " +
                            "classification=Loader.",
                            StringComparison.Ordinal) >= 0;
                        setWindowCompleted = observed.SetWindowCompleted;
                        predictedReadBlocked = observed.PredictedReadBlocked;
                        manifestRejected = observed.ManifestRejected;
                        transportRejected = observed.TransportRejected;
                        candidateValidated = observed.CandidateValidated;
                        structuredMetadataCaptured =
                            observed.StructuredMetadataCaptured;
                        postWindowNotReadyObserved =
                            observed.PostWindowNotReadyObserved;
                        postWindowRetryLimitReached =
                            observed.PostWindowRetryLimitReached;
                        firstImageReadCompleted =
                            observed.FirstImageReadCompleted;
                        payloadSha256 = observed.PayloadSha256 ??
                            payloadSha256;
                        if (loaderIdentity)
                        {
                            break;
                        }
                        if (permitPoweredCancellation && !stopInvoked &&
                            observed.ShortRetryStreamImageReadCount >=
                                PreviewCancellationTriggerRows)
                        {
                            if (observed.ShortRetryStreamImageReadCount >
                                    PreviewCancellationMaximumRows)
                            {
                                throw new InvalidOperationException(
                                    "The live Preview passed the 96-row " +
                                    "cancellation boundary before Stop could " +
                                    "be invoked.");
                            }
                            if (ClickNormalButtonIfPresent(
                                    launch.Process, "Stop"))
                            {
                                stopInvoked = true;
                                Console.WriteLine(
                                    "Invoked the one normal-sized, visible, " +
                                    "enabled Stop button after {0} completed " +
                                    "Preview rows.",
                                    observed.
                                        ShortRetryStreamImageReadCount);
                            }
                            else if (observed.ShortRetryStreamImageReadCount ==
                                    PreviewCancellationMaximumRows)
                            {
                                throw new InvalidOperationException(
                                    "The exact Stop control was not available " +
                                    "before the 96-row cancellation bound.");
                            }
                        }
                        if (observed.ReachedTerminalBoundary)
                        {
                            if (permitPoweredCancellation)
                            {
                                if (observed.CleanupTransportFailed ||
                                    observed.
                                        ShortRetryStreamImageReadFailed)
                                {
                                    break;
                                }
                                if (observed.CleanupCompletionCount != 2)
                                {
                                    Thread.Sleep(25);
                                    continue;
                                }
                                if (naturalTerminalObservedAt ==
                                        DateTime.MinValue)
                                {
                                    naturalTerminalObservedAt =
                                        DateTime.UtcNow;
                                }
                                if (DateTime.UtcNow -
                                        naturalTerminalObservedAt <
                                    TimeSpan.FromSeconds(2))
                                {
                                    Thread.Sleep(25);
                                    continue;
                                }
                                break;
                            }
                            if (permitNaturalImageStream &&
                                observed.
                                    ShortRetryStreamImageReadCompleted)
                            {
                                bool cleanupBoundaryReached =
                                    permitPoweredCleanup
                                        ? observed.CleanupCompletionCount == 2
                                        : observed.CleanupCandidateCount != 0;
                                if (permitPoweredCleanup &&
                                    observed.CleanupTransportFailed)
                                {
                                    break;
                                }
                                if (!cleanupBoundaryReached)
                                {
                                    Thread.Sleep(100);
                                    continue;
                                }
                                if (naturalTerminalObservedAt ==
                                        DateTime.MinValue)
                                {
                                    naturalTerminalObservedAt =
                                        DateTime.UtcNow;
                                }
                                if (DateTime.UtcNow -
                                        naturalTerminalObservedAt <
                                    TimeSpan.FromSeconds(2))
                                {
                                    Thread.Sleep(100);
                                    continue;
                                }
                            }
                            break;
                        }
                    }
                    Thread.Sleep(permitPoweredCancellation ? 25 : 100);
                }
            }
            finally
            {
                CloseTestProcess(launch.Process);
            }

            if (File.Exists(launch.LogPath))
            {
                string text = ReadSharedText(launch.LogPath);
                PreviewObserveLogMetadata observed =
                    PreviewObserveLogInspector.Inspect(text);
                finalObserved = observed;
                support = observed.Support;
                identity = observed.Identity;
                loaderIdentity = text.IndexOf(
                    "LIVE ASPI target-5 identity: Imacon/" +
                    "SCSI Loader/L302, type=0x06, " +
                    "classification=Loader.",
                    StringComparison.Ordinal) >= 0;
                setWindowCompleted = observed.SetWindowCompleted;
                predictedReadBlocked = observed.PredictedReadBlocked;
                manifestRejected = observed.ManifestRejected;
                transportRejected = observed.TransportRejected;
                candidateValidated = observed.CandidateValidated;
                structuredMetadataCaptured =
                    observed.StructuredMetadataCaptured;
                postWindowNotReadyObserved =
                    observed.PostWindowNotReadyObserved;
                postWindowRetryLimitReached =
                    observed.PostWindowRetryLimitReached;
                firstImageReadCompleted = observed.FirstImageReadCompleted;
                payloadSha256 = observed.PayloadSha256 ?? payloadSha256;
            }

            Console.WriteLine("ASPI log: {0}", launch.LogPath);
            Console.WriteLine("Support export observed: {0}", support);
            Console.WriteLine("Real target-5 identity returned: {0}", identity);
            Console.WriteLine("Loader L302 identity returned: {0}",
                loaderIdentity);
            Console.WriteLine("Requested Frame: {0}", requestedFrameLabel);
            Console.WriteLine("Frame selector verified: {0}", frameVerified);
            Console.WriteLine("Preview button invoked: {0}", previewInvoked);
            Console.WriteLine("Stop button invoked: {0}", stopInvoked);
            Console.WriteLine("SET WINDOW completed on scanner: {0}",
                setWindowCompleted);
            Console.WriteLine("First image READ blocked before USB: {0}",
                predictedReadBlocked);
            Console.WriteLine("First image READ completed once: {0}",
                firstImageReadCompleted);
            Console.WriteLine("Bounded image burst completed: {0}",
                finalObserved != null &&
                    finalObserved.BurstImageReadCompleted);
            Console.WriteLine("Bounded image burst failed closed: {0}",
                finalObserved != null &&
                    finalObserved.BurstImageReadFailed);
            Console.WriteLine("Bounded image stream completed: {0}",
                finalObserved != null &&
                    finalObserved.StreamImageReadCompleted);
            Console.WriteLine("Bounded image stream failed closed: {0}",
                finalObserved != null &&
                    finalObserved.StreamImageReadFailed);
            Console.WriteLine(
                "Bounded short-retry image stream completed: {0}",
                finalObserved != null &&
                    finalObserved.ShortRetryStreamImageReadCompleted);
            Console.WriteLine(
                "Bounded short-retry image stream failed closed: {0}",
                finalObserved != null &&
                    finalObserved.ShortRetryStreamImageReadFailed);
            if (finalObserved != null &&
                finalObserved.BurstImageReadCount != 0)
            {
                Console.WriteLine("Bounded image burst rows: {0}/{1}",
                    finalObserved.BurstImageReadCount,
                    finalObserved.BurstImageReadLimit);
            }
            if (finalObserved != null &&
                finalObserved.StreamImageReadCount != 0)
            {
                Console.WriteLine("Bounded image stream rows: {0}/{1}",
                    finalObserved.StreamImageReadCount,
                    finalObserved.StreamImageReadLimit);
                Console.WriteLine("In-stream ScannerReady polls: {0}",
                    finalObserved.InStreamScannerReadyPollCount);
                Console.WriteLine(
                    "Exact cleanup successors observed/blocked: {0}/2",
                    finalObserved.CleanupCandidateCount);
                Console.WriteLine(
                    "Cleanup transport failed closed: {0}",
                    finalObserved.CleanupTransportFailed);
            }
            if (finalObserved != null &&
                finalObserved.ShortRetryStreamImageReadCount != 0)
            {
                Console.WriteLine(
                    "Bounded short-retry stream rows: {0}/{1}",
                    finalObserved.ShortRetryStreamImageReadCount,
                    finalObserved.ShortRetryStreamImageReadLimit);
                Console.WriteLine(
                    "Short retries / image submissions: {0}/{1} / {2}/{3}",
                    finalObserved.ShortRetryCount,
                    finalObserved.ShortRetryMaximum,
                    finalObserved.ShortRetryStreamSubmissionCount,
                    finalObserved.ShortRetryStreamMaximumSubmissions);
                Console.WriteLine("In-stream ScannerReady polls: {0}",
                    finalObserved.InStreamScannerReadyPollCount);
                Console.WriteLine(
                    "Exact cleanup SET WINDOW completions: {0}/2",
                    finalObserved.CleanupCompletionCount);
                Console.WriteLine(
                    "Cancellation cleanup completions: {0}/2",
                    finalObserved.CancellationCleanupCompletionCount);
                Console.WriteLine(
                    "Exact cleanup successors observed/blocked: {0}/2",
                    finalObserved.CleanupCandidateCount);
            }
            Console.WriteLine("SET WINDOW candidate validated: {0}",
                candidateValidated);
            Console.WriteLine("SET WINDOW manifest rejected: {0}",
                manifestRejected);
            Console.WriteLine("SET WINDOW transport failed: {0}",
                transportRejected);
            Console.WriteLine("SET WINDOW rejected before USB: {0}",
                manifestRejected || transportRejected);
            Console.WriteLine(
                "Post-window non-ready ScannerReady observed: {0}",
                postWindowNotReadyObserved);
            Console.WriteLine(
                "Post-window ScannerReady safety limit reached: {0}",
                postWindowRetryLimitReached);
            if (finalObserved != null &&
                finalObserved.FirstPostWindowAttempts > 0)
            {
                Console.WriteLine(
                    "First readiness phase: attempts={0}, not-ready={1}, " +
                    "elapsed-ms={2}, final={3:X2} {4:X2}",
                    finalObserved.FirstPostWindowAttempts,
                    finalObserved.FirstPostWindowNotReadyCount,
                    finalObserved.FirstPostWindowElapsedMilliseconds,
                    finalObserved.FirstPostWindowFinalFirstByte,
                    finalObserved.FirstPostWindowFinalSecondByte);
            }
            if (finalObserved != null &&
                finalObserved.SecondPostWindowAttempts > 0)
            {
                Console.WriteLine(
                    "Second readiness phase: attempts={0}, not-ready={1}, " +
                    "elapsed-ms={2}, final={3:X2} {4:X2}",
                    finalObserved.SecondPostWindowAttempts,
                    finalObserved.SecondPostWindowNotReadyCount,
                    finalObserved.SecondPostWindowElapsedMilliseconds,
                    finalObserved.SecondPostWindowFinalFirstByte,
                    finalObserved.SecondPostWindowFinalSecondByte);
            }
            Console.WriteLine("Structured SET WINDOW metadata captured: {0}",
                structuredMetadataCaptured);
            Console.WriteLine("SET WINDOW payload SHA-256: {0}",
                payloadSha256 ?? "not observed");
            if (finalObserved != null &&
                finalObserved.FirstImageReadCompleted)
            {
                Console.WriteLine(
                    "First image READ result: status={0:X2}, requested={1}, " +
                    "actual={2}, residue={3}, width={4}, selector={5:X2}",
                    finalObserved.FirstImageReadStatus,
                    finalObserved.FirstImageReadRequested,
                    finalObserved.FirstImageReadActual,
                    finalObserved.FirstImageReadResidue,
                    finalObserved.FirstImageReadWidth,
                    finalObserved.FirstImageReadSelector);
                Console.WriteLine("First image payload SHA-256: {0}",
                    finalObserved.FirstImageReadSha256);
            }
            string expectedSetWindowSha256 =
                permit4x5FirstRead
                    ? LivePreview4x5SetWindowSha256
                : permit24x36SetWindowObservation || permit24x36FirstRead
                    ? LivePreview24x36SetWindowSha256
                    : LivePreviewSetWindowSha256;
            bool commonFailure =
                !support || !identity || loaderIdentity || !frameVerified ||
                !previewInvoked ||
                !structuredMetadataCaptured || payloadSha256 == null ||
                !string.Equals(payloadSha256, expectedSetWindowSha256,
                    StringComparison.Ordinal) ||
                ((predictedReadBlocked || transportRejected ||
                    postWindowRetryLimitReached) &&
                    !candidateValidated) ||
                ((predictedReadBlocked || postWindowRetryLimitReached) &&
                    !setWindowCompleted);
            bool firstReadFailure = permitFirstImageRead &&
                (!setWindowCompleted || predictedReadBlocked ||
                 manifestRejected || transportRejected ||
                 postWindowRetryLimitReached || !firstImageReadCompleted ||
                 finalObserved == null ||
                 finalObserved.FirstImageReadStatus != 0 ||
                 finalObserved.FirstImageReadRequested != 3996 ||
                 finalObserved.FirstImageReadActual != 3996 ||
                 finalObserved.FirstImageReadResidue != 0 ||
                 finalObserved.FirstImageReadWidth != 666 ||
                 finalObserved.FirstImageReadSelector != 0x28 ||
                 finalObserved.FirstImageReadSha256 == null);
            bool burstFailure = permitImageBurst &&
                (!setWindowCompleted || predictedReadBlocked ||
                 manifestRejected || transportRejected ||
                 postWindowRetryLimitReached || finalObserved == null ||
                 finalObserved.BurstImageReadFailed ||
                 !finalObserved.BurstImageReadCompleted ||
                 !finalObserved.BurstImageReadsExact ||
                 finalObserved.BurstImageReadCount != PreviewBurstRows ||
                 finalObserved.BurstImageReadLimit != PreviewBurstRows);
            bool streamFailure = permitImageStream &&
                (!setWindowCompleted || predictedReadBlocked ||
                 manifestRejected || transportRejected ||
                 postWindowRetryLimitReached || finalObserved == null ||
                 finalObserved.StreamImageReadFailed ||
                 !finalObserved.StreamImageReadCompleted ||
                 !finalObserved.StreamImageReadsExact ||
                 !finalObserved.InStreamScannerReadyPollsExact ||
                 finalObserved.StreamImageReadCount != PreviewStreamRows ||
                 finalObserved.StreamImageReadLimit != PreviewStreamRows);
            int expectedShortRetryMaximum =
                permitPoweredNaturalImageStream
                ? PreviewPoweredNaturalMaximumShortRetries
                : permitNaturalPerRowImageStream
                ? PreviewNaturalPerRowMaximumShortRetries
                : permitLegacyNaturalImageStream
                ? PreviewNaturalMaximumShortRetries
                : PreviewStreamMaximumShortRetries;
            int expectedSubmissionMaximum =
                permitPoweredNaturalImageStream
                ? PreviewPoweredNaturalMaximumSubmissions
                : permitNaturalPerRowImageStream
                ? PreviewNaturalPerRowMaximumSubmissions
                : permitLegacyNaturalImageStream
                ? PreviewNaturalMaximumSubmissions
                : PreviewStreamMaximumSubmissions;
            bool shortRetryStreamFailure =
                !permitPoweredCancellation &&
                (permitImageShortRetryStream || permitNaturalImageStream) &&
                (!setWindowCompleted || predictedReadBlocked ||
                 manifestRejected || transportRejected ||
                 postWindowRetryLimitReached || finalObserved == null ||
                 finalObserved.ShortRetryStreamImageReadFailed ||
                 !finalObserved.ShortRetryStreamImageReadCompleted ||
                 !finalObserved.ShortRetryStreamImageReadsExact ||
                 !finalObserved.ShortRetriesExact ||
                 finalObserved.ShortRetryStreamImageReadCount !=
                    permittedImageReads ||
                 finalObserved.ShortRetryStreamImageReadLimit !=
                    permittedImageReads ||
                 finalObserved.ShortRetryMaximum !=
                    expectedShortRetryMaximum ||
                 finalObserved.ShortRetryStreamMaximumSubmissions !=
                    expectedSubmissionMaximum ||
                 (permitNaturalImageStream &&
                    (permitPoweredCleanup
                        ? (finalObserved.CleanupCompletionCount != 2 ||
                           !finalObserved.CleanupCompletionsExact ||
                           finalObserved.CleanupTransportFailed ||
                           finalObserved.CleanupCandidateCount != 0)
                        : (finalObserved.CleanupCandidateCount == 0 ||
                           !finalObserved.CleanupCandidatesExact ||
                           finalObserved.CleanupCompletionCount != 0))) ||
                 finalObserved.ShortRetryCount > expectedShortRetryMaximum ||
                 finalObserved.ShortRetryStreamSubmissionCount !=
                    permittedImageReads + finalObserved.ShortRetryCount ||
                 (finalObserved.InStreamScannerReadyPollCount != 0 &&
                    !finalObserved.InStreamScannerReadyPollsExact));
            bool cancellationFailure = permitPoweredCancellation &&
                (!setWindowCompleted || predictedReadBlocked ||
                 manifestRejected || transportRejected ||
                 postWindowRetryLimitReached || !stopInvoked ||
                 finalObserved == null ||
                 finalObserved.ShortRetryStreamImageReadFailed ||
                 finalObserved.ShortRetryStreamImageReadCompleted ||
                 !finalObserved.ShortRetryStreamImageReadsExact ||
                 !finalObserved.ShortRetriesExact ||
                 finalObserved.ShortRetryStreamImageReadCount <
                    PreviewCancellationTriggerRows ||
                 finalObserved.ShortRetryStreamImageReadCount >
                    PreviewCancellationMaximumRows ||
                 finalObserved.ShortRetryStreamImageReadLimit !=
                    PreviewPoweredNaturalRows ||
                 finalObserved.ShortRetryMaximum !=
                    PreviewPoweredNaturalMaximumShortRetries ||
                 finalObserved.ShortRetryStreamMaximumSubmissions !=
                    PreviewPoweredNaturalMaximumSubmissions ||
                 finalObserved.ShortRetryCount >
                    PreviewPoweredNaturalMaximumShortRetries ||
                 finalObserved.ShortRetryStreamSubmissionCount !=
                    finalObserved.ShortRetryStreamImageReadCount +
                        finalObserved.ShortRetryCount ||
                 finalObserved.CleanupCompletionCount != 2 ||
                 finalObserved.CancellationCleanupCompletionCount != 2 ||
                 !finalObserved.CleanupCompletionsExact ||
                 finalObserved.CleanupTransportFailed ||
                 finalObserved.CleanupCandidateCount != 0 ||
                 (finalObserved.InStreamScannerReadyPollCount != 0 &&
                    !finalObserved.InStreamScannerReadyPollsExact));
            bool blockedReadFailure = permittedImageReads == 0 &&
                (!predictedReadBlocked && !manifestRejected &&
                 !transportRejected && !postWindowRetryLimitReached);
            if (commonFailure || firstReadFailure || burstFailure ||
                streamFailure || shortRetryStreamFailure ||
                cancellationFailure || blockedReadFailure)
            {
                Console.Error.WriteLine(
                    permitPoweredCancellation
                        ? "Powered Preview cancellation did not complete its " +
                            "exact 32-to-96-row/two-cleanup contract; " +
                            "FlexColor was closed."
                    : permitPoweredNaturalImageStream
                        ? (permitPoweredCleanup
                            ? "Powered-natural 911-row Preview cleanup test " +
                                "did not complete its exact transfer/retry/" +
                                "poll/two-cleanup contract; FlexColor was " +
                                "closed."
                            : "Powered-natural 911-row Preview observation " +
                                "did not complete its exact transfer/retry/" +
                                "poll contract; FlexColor was closed.")
                    : permitNaturalImageStream
                        ? "Natural-boundary 996-row Preview observation did " +
                            "not complete its exact transfer/retry/poll " +
                            "contract; FlexColor was closed."
                    : permitImageShortRetryStream
                        ? "Bounded 256-row short-retry Preview observation " +
                            "did not complete its exact transfer/retry/poll " +
                            "contract; FlexColor was closed."
                        : permitImageStream
                        ? "Bounded 256-row Preview observation did not " +
                            "complete its exact transfer/poll contract; " +
                            "FlexColor was closed."
                        : permitImageBurst
                        ? "Bounded eight-row Preview observation did not " +
                            "complete its exact transfer contract; FlexColor " +
                            "was closed."
                        : permitFirstImageRead
                        ? "Bounded first-image-read observation did not " +
                            "complete its exact one-transfer contract; " +
                            "FlexColor was closed."
                        : "Bounded Preview observation did not reach a " +
                            "terminal safety boundary; FlexColor was closed.");
                return 4;
            }
            if (manifestRejected)
            {
                Console.WriteLine(
                    "Bounded Preview observation captured a metadata-only " +
                    "manifest mismatch and closed FlexColor; no SET WINDOW " +
                    "reached USB.");
            }
            else if (transportRejected)
            {
                Console.Error.WriteLine(
                    "Bounded Preview observation validated the candidate but " +
                    "the transport failed before completion; inspect the " +
                    "specific transport error before any retry.");
                return 4;
            }
            else if (postWindowRetryLimitReached)
            {
                Console.Error.WriteLine(
                    "Bounded Preview observation completed SET WINDOW but " +
                    "the scanner did not become ready within the bounded " +
                    "readiness phase. No image READ was sent; inspect " +
                    "the two-byte ScannerReady metadata before any retry.");
                return 4;
            }
            else
            {
                Console.WriteLine(
                    permitPoweredCancellation
                        ? "Powered Preview cancellation completed after 32 " +
                            "to 96 full ordered image rows and exactly two " +
                            "successful hash-pinned cancellation cleanup " +
                            "SET WINDOW commands; row 97 and every other " +
                            "successor remained blocked."
                    : permitPoweredNaturalImageStream
                        ? (permitPoweredCleanup
                            ? "Powered-natural Preview cleanup test completed " +
                                "one exact initial SET WINDOW, 911 full " +
                                "ordered image rows with bounded exact " +
                                "10-byte shorts mapped to ASPI target BUSY, " +
                                "and exactly two successful hash-pinned " +
                                "cleanup SET WINDOW commands; row 912 and " +
                                "every other successor remained blocked."
                            : "Powered-natural Preview observation completed " +
                                "one exact SET WINDOW and 911 full ordered " +
                                "image rows with only bounded exact 10-byte " +
                                "short completions mapped to ASPI target " +
                                "BUSY without copying; row 912 and cleanup " +
                                "were blocked.")
                    : permitNaturalImageStream
                        ? "Natural-boundary Preview observation completed " +
                            "one exact SET WINDOW and 996 full ordered image " +
                            "rows with only bounded exact 10-byte short " +
                            "completions mapped to ASPI target BUSY without " +
                            "copying; row 997 and cleanup were blocked."
                    : permitImageShortRetryStream
                        ? "Bounded Preview observation completed one exact " +
                            "SET WINDOW and 256 full ordered image rows with " +
                            "only bounded exact 10-byte short completions " +
                            "mapped to ASPI target BUSY without copying; row " +
                            "257 and cleanup were blocked."
                        : permitImageStream
                        ? "Bounded Preview observation completed one exact " +
                            "SET WINDOW, 256 ordered 3,996-byte image READs, " +
                            "and bounded in-stream ScannerReady polling; row " +
                            "257 and cleanup were blocked by closing FlexColor."
                        : permitImageBurst
                        ? "Bounded Preview observation completed one exact " +
                            "SET WINDOW and eight ordered 3,996-byte image " +
                            "READs; row 9 and cleanup were blocked by closing " +
                            "FlexColor."
                        : permitFirstImageRead
                        ? "Bounded Preview observation completed one exact " +
                            "SET WINDOW and one exact 3,996-byte image READ; " +
                            "all later commands were blocked by closing " +
                            "FlexColor."
                        : "Bounded Preview observation completed one exact " +
                            "SET WINDOW and closed FlexColor after blocking " +
                            "the first image READ before USB.");
            }
            return 0;
        }

        private static bool ClickPreviewIfPresent(Process process)
        {
            return ClickNormalButtonIfPresent(process, "Preview");
        }

        private static bool ClickNormalButtonIfPresent(Process process,
            string buttonText)
        {
            process.Refresh();
            IntPtr parent = process.MainWindowHandle;
            if (parent == IntPtr.Zero)
            {
                return false;
            }

            var matches = new List<IntPtr>();
            NativeMethods.EnumChildWindows(parent,
                delegate(IntPtr window, IntPtr parameter)
                {
                    uint processId;
                    NativeMethods.GetWindowThreadProcessId(window,
                        out processId);
                    if (processId != checked((uint)process.Id) ||
                        !NativeMethods.IsWindowVisible(window) ||
                        !NativeMethods.IsWindowEnabled(window))
                    {
                        return true;
                    }
                    var text = new StringBuilder(64);
                    var className = new StringBuilder(64);
                    NativeMethods.GetWindowText(window, text, text.Capacity);
                    NativeMethods.GetClassName(window, className,
                        className.Capacity);
                    NativeMethods.WindowRectangle rectangle;
                    if (text.ToString() != buttonText ||
                        className.ToString() != "Button" ||
                        !NativeMethods.GetWindowRect(window, out rectangle))
                    {
                        return true;
                    }
                    int width = rectangle.Right - rectangle.Left;
                    int height = rectangle.Bottom - rectangle.Top;
                    if (width > 0 && width <= 300 &&
                        height > 0 && height <= 100)
                    {
                        matches.Add(window);
                    }
                    return true;
                }, IntPtr.Zero);
            return matches.Count == 1 && NativeMethods.PostMessage(matches[0],
                0x00F5, IntPtr.Zero, IntPtr.Zero);
        }

        private static bool SubmitDefaultSaveIfPresent(Process process,
            string outputPath)
        {
            string outputDirectory = Path.GetDirectoryName(outputPath);
            string expectedName = Path.GetFileName(outputPath);
            if (File.Exists(outputPath) ||
                File.Exists(Path.ChangeExtension(outputPath, ".tiff")) ||
                (!string.Equals(expectedName, "Untitled0.tif",
                    StringComparison.Ordinal) &&
                 !string.Equals(expectedName, "Untitled1.tif",
                    StringComparison.Ordinal)) ||
                string.IsNullOrEmpty(outputDirectory))
            {
                throw new InvalidOperationException(
                    "The bounded default Save target is not exact or already " +
                    "exists.");
            }
            IntPtr dialog = FindExactTopLevelWindow(process, "Save");
            if (dialog == IntPtr.Zero)
            {
                return false;
            }
            var filenameEdits = new List<IntPtr>();
            var saveButtons = new List<IntPtr>();
            var exactAddresses = new List<IntPtr>();
            NativeMethods.EnumChildWindows(dialog,
                delegate(IntPtr window, IntPtr parameter)
                {
                    uint processId;
                    NativeMethods.GetWindowThreadProcessId(window,
                        out processId);
                    if (processId != checked((uint)process.Id) ||
                        !NativeMethods.IsWindowVisible(window) ||
                        !NativeMethods.IsWindowEnabled(window))
                    {
                        return true;
                    }
                    var className = new StringBuilder(64);
                    var text = new StringBuilder(512);
                    NativeMethods.GetClassName(window, className,
                        className.Capacity);
                    NativeMethods.GetWindowText(window, text, text.Capacity);
                    int controlId = NativeMethods.GetDlgCtrlID(window);
                    if (className.ToString() == "Edit" && controlId == 1001)
                    {
                        filenameEdits.Add(window);
                    }
                    else if (className.ToString() == "Button" &&
                        controlId == 1 && text.ToString() == "&Save")
                    {
                        saveButtons.Add(window);
                    }
                    else if (className.ToString() == "ToolbarWindow32" &&
                        text.ToString() == "Address: " + outputDirectory)
                    {
                        exactAddresses.Add(window);
                    }
                    return true;
                }, IntPtr.Zero);
            if (filenameEdits.Count != 1 || saveButtons.Count != 1 ||
                exactAddresses.Count != 1)
            {
                throw new InvalidOperationException(
                    "The exact FlexColor Save dialog did not expose one " +
                    "enabled filename edit, Save button, and private output " +
                    "address.");
            }
            uint dialogProcessId;
            if (NativeMethods.GetWindowThreadProcessId(dialog,
                    out dialogProcessId) == 0 ||
                dialogProcessId != checked((uint)process.Id))
            {
                throw new InvalidOperationException(
                    "The exact FlexColor Save dialog thread identity changed.");
            }
            var initialName = new StringBuilder(64);
            NativeMethods.GetWindowText(filenameEdits[0], initialName,
                initialName.Capacity);
            if (initialName.Length != 0 &&
                initialName.ToString() != expectedName)
            {
                throw new InvalidOperationException(
                    "The exact FlexColor Save filename default changed.");
            }
            return NativeMethods.PostMessage(saveButtons[0], 0x00F5,
                IntPtr.Zero, IntPtr.Zero);
        }

        private static bool HasExactDialog(Process process, string title)
        {
            return FindExactTopLevelWindow(process, title) != IntPtr.Zero;
        }

        private static IntPtr FindExactTopLevelWindow(Process process,
            string title)
        {
            IntPtr match = IntPtr.Zero;
            NativeMethods.EnumWindows(
                delegate(IntPtr window, IntPtr parameter)
                {
                    uint processId;
                    NativeMethods.GetWindowThreadProcessId(window,
                        out processId);
                    if (processId != checked((uint)process.Id) ||
                        !NativeMethods.IsWindowVisible(window))
                    {
                        return true;
                    }
                    var text = new StringBuilder(128);
                    NativeMethods.GetWindowText(window, text, text.Capacity);
                    if (text.ToString() == title)
                    {
                        if (match != IntPtr.Zero)
                        {
                            throw new InvalidOperationException(
                                "More than one exact FlexColor dialog matched " +
                                title + ".");
                        }
                        match = window;
                    }
                    return true;
                }, IntPtr.Zero);
            return match;
        }

        private static string FindLocalReplayIdentifier()
        {
            string appData = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            string devices = Path.Combine(appData, "FlexColor", "Devices");
            if (!Directory.Exists(devices))
            {
                return null;
            }

            string match = null;
            foreach (string path in Directory.GetFileSystemEntries(devices))
            {
                string name = Path.GetFileName(path);
                if (name.Length != 12 || name[0] != 'S' || name[1] != ' ')
                {
                    continue;
                }
                string candidate = name.Substring(2);
                if (!IsReplayIdentifier(candidate))
                {
                    continue;
                }
                if (string.Equals(candidate, "FP00000000",
                        StringComparison.Ordinal))
                {
                    // Earlier fictional replay runs can leave their own
                    // directory marker. It must never displace a real local
                    // scanner profile when one is available.
                    continue;
                }
                if (match != null && !string.Equals(match, candidate,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "More than one compatible local FlexColor device " +
                        "marker exists; offline replay will not guess which " +
                        "scanner profile to use.");
                }
                match = candidate;
            }
            return match;
        }

        private static bool IsReplayIdentifier(string value)
        {
            if (value == null || value.Length != 10 ||
                value[0] != 'F' || value[1] != 'P')
            {
                return false;
            }
            for (int index = 2; index < value.Length; ++index)
            {
                char character = value[index];
                if (!((character >= 'A' && character <= 'Z') ||
                      (character >= '0' && character <= '9')))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool DeleteCrashNotificationIfPresent(
            Process process)
        {
            return ClickExactDialogButton(process,
                "FlexColor bomblog notification", "Delete Log");
        }

        private static bool DeferRegistrationIfPresent(Process process)
        {
            return ClickExactDialogButton(process, "Registration",
                "Register Later");
        }

        private static bool ClickExactDialogButton(Process process,
            string dialogTitle, string buttonText)
        {
            IntPtr dialog = NativeMethods.FindWindow(null, dialogTitle);
            if (dialog == IntPtr.Zero)
            {
                return false;
            }
            uint processId;
            NativeMethods.GetWindowThreadProcessId(dialog, out processId);
            if (processId != checked((uint)process.Id))
            {
                return false;
            }
            IntPtr button = NativeMethods.FindWindowEx(dialog, IntPtr.Zero,
                "Button", buttonText);
            if (button == IntPtr.Zero)
            {
                return false;
            }
            if (!NativeMethods.IsWindowVisible(button) ||
                !NativeMethods.IsWindowEnabled(button))
            {
                return false;
            }
            int controlId = NativeMethods.GetDlgCtrlID(button);
            return controlId >= 0 && NativeMethods.PostMessage(dialog, 0x0111,
                new IntPtr(controlId), button);
        }

        private static string FindPayloadSha256After(string text,
            string contextMarker)
        {
            const string marker = "payload-sha256=";
            int contextOffset = text.IndexOf(contextMarker,
                StringComparison.Ordinal);
            if (contextOffset < 0)
            {
                return null;
            }
            int lineEnd = text.IndexOfAny(new char[] { '\r', '\n' },
                contextOffset);
            if (lineEnd < 0)
            {
                lineEnd = text.Length;
            }
            int offset = text.IndexOf(marker, contextOffset,
                StringComparison.Ordinal);
            if (offset < 0 || offset >= lineEnd)
            {
                return null;
            }
            offset += marker.Length;
            if (text.Length - offset < 64)
            {
                return null;
            }
            string value = text.Substring(offset, 64);
            for (int index = 0; index < value.Length; ++index)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'A' && character <= 'F') ||
                      (character >= 'a' && character <= 'f')))
                {
                    return null;
                }
            }
            return value.ToUpperInvariant();
        }

        private static string ReadSharedText(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static void CloseTestProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(5000))
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                }
            }
            finally
            {
                process.Dispose();
            }
        }

        private static void PrintPreflight(PreflightResult result)
        {
            Console.WriteLine("Private root:       {0}", result.Root);
            Console.WriteLine("FlexColor.exe:      {0}", result.ExecutableHash);
            Console.WriteLine("FlexColor.dll:      {0}",
                result.Patch.Sha256);
            Console.WriteLine("Patch/backup:       {0}/{1}",
                result.Patch.State, result.Patch.BackupState);
            Console.WriteLine("wnaspi32.dll:       {0}",
                result.DependencyHashes["wnaspi32.dll"]);
            Console.WriteLine("ASPI managed shim:  {0}",
                result.DependencyHashes[
                    "Usb2Xchange.AspiShim.Managed.dll"]);
            Console.WriteLine("ASPI WinUSB:        {0}",
                result.DependencyHashes["Usb2Xchange.WinUsb.dll"]);
            Console.WriteLine("ASPI protocol:      {0}",
                result.DependencyHashes["Usb2Xchange.Protocol.dll"]);
            Console.WriteLine("ASPI provider cfg:  {0}",
                result.DependencyHashes["wnaspi32.dll.config"]);
            Console.WriteLine("FlexColor CLR cfg:  {0}",
                result.DependencyHashes["FlexColor.exe.config"]);
            Console.WriteLine("Provider PE/exports: PE32 I386, both exact exports");
            Console.WriteLine("Preflight passed; no process or hardware was opened.");
        }

        private static void PrintLoaderPreflight(PreflightResult result)
        {
            Console.WriteLine("MICROCOD.3XX:      {0}",
                result.LoaderFirmwareHash);
            Console.WriteLine(
                "Loader preflight passed; scanner data-out remains disabled.");
        }

        private static void RequireExport(PeImageInfo pe, string name)
        {
            if (!pe.Exports.Contains(name))
            {
                throw new InvalidOperationException(
                    "wnaspi32.dll is missing export " + name + ".");
            }
        }

        private static void RequireFile(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                throw new InvalidOperationException(
                    "Required file is missing or empty: " + path);
            }
        }

        private static string RequireExactHash(string path,
            string expectedHash)
        {
            RequireFile(path);
            string actualHash = Sha256(path);
            if (!string.Equals(actualHash, expectedHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(string.Format(
                    "ASPI staged file hash mismatch for {0}: expected {1}, " +
                    "found {2}.", Path.GetFileName(path), expectedHash,
                    actualHash));
            }
            return actualHash;
        }

        private static string Sha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).
                    Replace("-", string.Empty);
            }
        }

        private static void RequirePrivatePath(string path)
        {
            var roots = new List<string>();
            roots.Add(Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles));
            roots.Add(Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86));
            roots.Add(Environment.GetFolderPath(
                Environment.SpecialFolder.Windows));
            foreach (string rootValue in roots)
            {
                if (string.IsNullOrWhiteSpace(rootValue))
                {
                    continue;
                }
                string root = Path.GetFullPath(rootValue).TrimEnd('\\');
                if (path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(root + "\\",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Private root cannot be under an installed/system path.");
                }
            }
        }

        private static void RejectReparsePoints(string path)
        {
            string current = path;
            while (!string.IsNullOrEmpty(current))
            {
                if ((File.GetAttributes(current) &
                        FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException(
                        "Private root contains a reparse point: " + current);
                }
                DirectoryInfo parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine(
                "Usage: flexcolor-aspi-launcher preflight <private-root>");
            Console.WriteLine(
                "       flexcolor-aspi-launcher launch-replay <private-root> " +
                ReplayApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher smoke-replay <private-root> " +
                SmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher smoke-operational-replay " +
                "<private-root> " + OperationalReplaySmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-operational-replay-frame <private-root> " +
                "<24x36|60x60|60x70|4x5> " +
                OperationalReplaySmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher smoke-offline-full-scan " +
                "<private-root> 60x60 " + FullScanReplaySmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-offline-operator-repeat <private-root> 60x60 " +
                OfflineOperatorRepeatSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-operator-repeat-60x60 <private-root> 60x60 " +
                LiveOperatorRepeatSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher launch-live-read-only " +
                "<private-root> " + LiveApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher smoke-live-read-only " +
                "<private-root> " + LiveSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "launch-trusted-flexcolor-pass-through <private-root> " +
                TrustedPassThroughLaunchApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher loader-preflight " +
                "<private-root>");
            Console.WriteLine(
                "       flexcolor-aspi-launcher launch-live-loader " +
                "<private-root> " + LoaderLaunchApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-preview-fingerprint <private-root> " +
                "<24x36|60x60|60x70|4x5> " +
                PreviewFingerprintSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher smoke-live-preview-observe " +
                "<private-root> 60x60 " +
                PreviewSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-preview-set-window-24x36-observe " +
                "<private-root> 24x36 " +
                Preview24x36SetWindowSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-preview-first-read-24x36 <private-root> 24x36 " +
                Preview24x36FirstReadSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-preview-first-read-4x5 <private-root> 4x5 " +
                Preview4x5FirstReadSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-preview-first-read <private-root> 60x60 " +
                PreviewFirstReadSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-preview-burst-8 <private-root> 60x60 " +
                PreviewBurstSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-preview-stream-256 <private-root> 60x60 " +
                PreviewStreamSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-preview-stream-256-short-retry <private-root> " +
                "60x60 " + PreviewShortRetrySmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-preview-natural-996-short-retry " +
                "<private-root> 60x60 " + PreviewNaturalSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-preview-natural-996-per-row-8 " +
                "<private-root> 60x60 " +
                PreviewNaturalPerRowSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-preview-powered-natural-911-observe " +
                "<private-root> 60x60 " +
                PreviewPoweredNaturalSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-preview-powered-natural-911-cleanup-2 " +
                "<private-root> 60x60 " +
                PreviewPoweredCleanupSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-preview-powered-cancel-32-96-cleanup-2 " +
                "<private-root> 60x60 " +
                PreviewPoweredCancellationSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-full-scan-startup-fingerprint " +
                "<private-root> 60x60 " +
                FullScanStartupFingerprintSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-full-scan-set-window-first-read " +
                "<private-root> 60x60 " +
                FullScanSetWindowSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-full-scan-complete-60x60 " +
                "<private-root> 60x60 " + FullScanCompleteSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-full-scan-terminal-probe-60x60 " +
                "<private-root> 60x60 " +
                FullScanTerminalProbeSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-full-scan-natural-996-complete-60x60 " +
                "<private-root> 60x60 " +
                FullScanNaturalCompleteSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-full-scan-row997-complete-60x60 " +
                "<private-root> 60x60 " +
                FullScanRow997CompleteSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-live-full-scan-progress-complete-60x60 " +
                "<private-root> 60x60 " +
                FullScanProgressCompleteSmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher " +
                "smoke-offline-preview-cancel <private-root> 60x60 " +
                OfflineCancelReplaySmokeApproval);
            Console.WriteLine(
                "       flexcolor-aspi-launcher launch-live-preview-observe " +
                "<private-root> " + PreviewLaunchApproval);
        }

        private sealed class PreflightResult
        {
            internal PreflightResult(string root, string executable,
                string provider, string executableHash,
                Dictionary<string, string> dependencyHashes,
                PatchInspection patch)
            {
                Root = root;
                Executable = executable;
                Provider = provider;
                ExecutableHash = executableHash;
                DependencyHashes = dependencyHashes;
                Patch = patch;
            }

            internal string Root { get; private set; }
            internal string Executable { get; private set; }
            internal string Provider { get; private set; }
            internal string ExecutableHash { get; private set; }
            internal Dictionary<string, string> DependencyHashes
            {
                get;
                private set;
            }
            internal PatchInspection Patch { get; private set; }
            internal string LoaderFirmwareHash { get; set; }
        }

        private sealed class LaunchResult
        {
            internal LaunchResult(Process process, string logPath)
            {
                Process = process;
                LogPath = logPath;
            }

            internal Process Process { get; private set; }
            internal string LogPath { get; private set; }
        }

        private static class NativeMethods
        {
            internal const uint CreateUnicodeEnvironment = 0x00000400;
            internal const uint CreateNoWindow = 0x08000000;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct StartupInfo
            {
                internal int Size;
                internal string Reserved;
                internal string Desktop;
                internal string Title;
                internal int X;
                internal int Y;
                internal int XSize;
                internal int YSize;
                internal int XCountChars;
                internal int YCountChars;
                internal int FillAttribute;
                internal int Flags;
                internal short ShowWindow;
                internal short Reserved2Size;
                internal IntPtr Reserved2;
                internal IntPtr StandardInput;
                internal IntPtr StandardOutput;
                internal IntPtr StandardError;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct ProcessInformation
            {
                internal IntPtr ProcessHandle;
                internal IntPtr ThreadHandle;
                internal uint ProcessId;
                internal uint ThreadId;
            }

            internal delegate bool EnumChildProcedure(IntPtr window,
                IntPtr parameter);

            [StructLayout(LayoutKind.Sequential)]
            internal struct WindowRectangle
            {
                internal int Left;
                internal int Top;
                internal int Right;
                internal int Bottom;
            }

            [DllImport("user32.dll")]
            internal static extern bool EnumChildWindows(IntPtr parent,
                EnumChildProcedure callback, IntPtr parameter);

            [DllImport("user32.dll")]
            internal static extern bool EnumWindows(
                EnumChildProcedure callback, IntPtr parameter);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CreateProcess(string applicationName,
                StringBuilder commandLine, IntPtr processAttributes,
                IntPtr threadAttributes,
                [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
                uint creationFlags, IntPtr environment,
                string currentDirectory, ref StartupInfo startupInfo,
                out ProcessInformation processInformation);

            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle(IntPtr handle);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern int GetWindowText(IntPtr window,
                StringBuilder text, int maximumCount);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern int GetClassName(IntPtr window,
                StringBuilder className, int maximumCount);

            [DllImport("user32.dll")]
            internal static extern bool GetWindowRect(IntPtr window,
                out WindowRectangle rectangle);

            [DllImport("user32.dll")]
            internal static extern bool IsWindowVisible(IntPtr window);

            [DllImport("user32.dll")]
            internal static extern bool IsWindowEnabled(IntPtr window);

            [DllImport("user32.dll", SetLastError = true)]
            internal static extern int GetDlgCtrlID(IntPtr window);

            [DllImport("user32.dll", SetLastError = true)]
            internal static extern bool PostMessage(IntPtr window,
                uint message, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Unicode,
                SetLastError = true)]
            internal static extern IntPtr FindWindow(string className,
                string windowName);

            [DllImport("user32.dll", CharSet = CharSet.Unicode,
                SetLastError = true)]
            internal static extern IntPtr FindWindowEx(IntPtr parent,
                IntPtr childAfter, string className, string windowName);

            [DllImport("user32.dll")]
            internal static extern uint GetWindowThreadProcessId(
                IntPtr window, out uint processId);

        }
    }
}
