// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Text.RegularExpressions;

namespace Usb2Xchange.FlexColorAspiLauncher
{
    internal static class FullScanStartupFingerprintLogInspector
    {
        private const string ArmedMarker =
            "LIVE ASPI full-scan successor observer armed after two exact " +
            "Preview cleanup completions";
        private const string InquiryMarker =
            "LIVE ASPI full-scan successor observed its exact operational " +
            "INQUIRY prefix.";
        private const string StartupMarker =
            "startup WRITE BUFFER fingerprint captured and blocked before " +
            "USB; payload-sha256=";
        private const string SetWindowMarker =
            "LIVE ASPI full-scan SET WINDOW fingerprint captured and " +
            "blocked before USB; payload-sha256=";
        private const string FailureMarker = "EXEC_SCSI_CMD failed";
        private const string SetWindowCompletionMarker =
            "LIVE ASPI full-scan SET WINDOW completed once: status=0x00, " +
            "actual=84, residue=0, payload-sha256=";
        private const string FirstImageReadMarker =
            "LIVE ASPI full-scan first image READ fingerprint captured and " +
            "blocked before USB:";
        private const string TerminalProbeMarker =
            "LIVE ASPI exact full-scan terminal probe observed and " +
            "quarantined:";
        private static readonly Regex InitialReadyPattern = new Regex(
            "LIVE ASPI full-scan initial ScannerReady response: attempt=" +
            "([0-9]+)/256, phase-elapsed-ms=([0-9]+)/60000, status=0x" +
            "([0-9A-F]{2}), actual=([0-9]+), residue=([0-9]+), first=0x" +
            "([0-9A-F]{2}), second=0x([0-9A-F]{2}), " +
            "ready=(True|False)\\.", RegexOptions.CultureInvariant);
        private static readonly Regex ExtraReadyPattern = new Regex(
            "LIVE ASPI full-scan extra ScannerReady response: attempt=" +
            "([0-9]+)/256, elapsed-ms=([0-9]+)/60000, first=0x" +
            "([0-9A-F]{2}), second=0x([0-9A-F]{2}), ready=(True|False)\\.",
            RegexOptions.CultureInvariant);
        private static readonly Regex FirstImageReadPattern = new Regex(
            "LIVE ASPI full-scan first image READ fingerprint captured and " +
            "blocked before USB: width=([0-9]+), length=([0-9]+), " +
            "selector=0x([0-9A-F]{2}), CDB=" +
            "([0-9A-F]{2}(?: [0-9A-F]{2}){9})\\.",
            RegexOptions.CultureInvariant);

        internal static FullScanStartupFingerprintLogMetadata Inspect(
            string text)
        {
            text = text ?? string.Empty;
            int initialAttempts = 0;
            int initialNotReady = 0;
            int previousInitialElapsed = -1;
            bool initialExact = true;
            bool initialReady = false;
            foreach (Match match in InitialReadyPattern.Matches(text))
            {
                ++initialAttempts;
                int ordinal = int.Parse(match.Groups[1].Value,
                    System.Globalization.CultureInfo.InvariantCulture);
                int elapsed = int.Parse(match.Groups[2].Value,
                    System.Globalization.CultureInfo.InvariantCulture);
                int status = Convert.ToInt32(match.Groups[3].Value, 16);
                int actual = int.Parse(match.Groups[4].Value,
                    System.Globalization.CultureInfo.InvariantCulture);
                int residue = int.Parse(match.Groups[5].Value,
                    System.Globalization.CultureInfo.InvariantCulture);
                int first = Convert.ToInt32(match.Groups[6].Value, 16);
                int second = Convert.ToInt32(match.Groups[7].Value, 16);
                bool ready = string.Equals(match.Groups[8].Value, "True",
                    StringComparison.Ordinal);
                if (ordinal != initialAttempts || initialAttempts > 256 ||
                    elapsed < previousInitialElapsed || elapsed >= 60000 ||
                    status != 0 || actual != 2 || residue != 0 ||
                    second != 0 || first != (ready ? 0 : 8) || initialReady)
                {
                    initialExact = false;
                }
                previousInitialElapsed = elapsed;
                if (ready)
                {
                    initialReady = true;
                }
                else
                {
                    ++initialNotReady;
                }
            }

            int attempts = 0;
            int notReady = 0;
            bool extraExact = true;
            bool extraReady = false;
            foreach (Match match in ExtraReadyPattern.Matches(text))
            {
                ++attempts;
                int ordinal = int.Parse(match.Groups[1].Value,
                    System.Globalization.CultureInfo.InvariantCulture);
                int first = Convert.ToInt32(match.Groups[3].Value, 16);
                int second = Convert.ToInt32(match.Groups[4].Value, 16);
                bool ready = string.Equals(match.Groups[5].Value, "True",
                    StringComparison.Ordinal);
                if (ordinal != attempts || second != 0 ||
                    first != (ready ? 0 : 8) || extraReady)
                {
                    extraExact = false;
                }
                if (ready)
                {
                    extraReady = true;
                }
                else
                {
                    ++notReady;
                }
            }

            string startupHash = FindHash(text, StartupMarker);
            string setWindowHash = FindHash(text, SetWindowMarker);
            string completedSetWindowHash = FindHash(text,
                SetWindowCompletionMarker);
            int firstImageWidth = 0;
            int firstImageLength = 0;
            int firstImageSelector = -1;
            string firstImageCdb = null;
            Match firstImage = FirstImageReadPattern.Match(text);
            if (firstImage.Success)
            {
                firstImageWidth = int.Parse(firstImage.Groups[1].Value,
                    System.Globalization.CultureInfo.InvariantCulture);
                firstImageLength = int.Parse(firstImage.Groups[2].Value,
                    System.Globalization.CultureInfo.InvariantCulture);
                firstImageSelector = Convert.ToInt32(
                    firstImage.Groups[3].Value, 16);
                firstImageCdb = firstImage.Groups[4].Value;
            }
            int armedOffset = text.IndexOf(ArmedMarker,
                StringComparison.Ordinal);
            int terminalOffset = FirstPresentOffset(text, StartupMarker,
                SetWindowMarker, FirstImageReadMarker, TerminalProbeMarker);
            int failureOffset = armedOffset < 0 ? -1 : text.IndexOf(
                FailureMarker, armedOffset, StringComparison.Ordinal);
            bool failureBeforeTerminal = failureOffset >= 0 &&
                (terminalOffset < 0 || failureOffset < terminalOffset);
            return new FullScanStartupFingerprintLogMetadata(
                Contains(text, ArmedMarker), Contains(text, InquiryMarker),
                initialAttempts, initialNotReady, initialExact, initialReady,
                attempts, notReady, extraExact, extraReady, startupHash,
                setWindowHash, completedSetWindowHash, firstImageWidth,
                firstImageLength, firstImageSelector, firstImageCdb,
                failureBeforeTerminal);
        }

        private static int FirstPresentOffset(string text, params string[] markers)
        {
            int result = -1;
            foreach (string marker in markers)
            {
                int offset = text.IndexOf(marker, StringComparison.Ordinal);
                if (offset >= 0 && (result < 0 || offset < result))
                {
                    result = offset;
                }
            }
            return result;
        }

        private static string FindHash(string text, string marker)
        {
            int markerOffset = text.IndexOf(marker, StringComparison.Ordinal);
            if (markerOffset < 0)
            {
                return null;
            }
            int hashOffset = markerOffset + marker.Length;
            if (text.Length - hashOffset < 64)
            {
                return null;
            }
            string hash = text.Substring(hashOffset, 64).ToUpperInvariant();
            for (int index = 0; index < hash.Length; ++index)
            {
                char value = hash[index];
                if (!((value >= '0' && value <= '9') ||
                      (value >= 'A' && value <= 'F')))
                {
                    return null;
                }
            }
            return hash;
        }

        private static bool Contains(string text, string marker)
        {
            return text.IndexOf(marker, StringComparison.Ordinal) >= 0;
        }
    }

    internal sealed class FullScanStartupFingerprintLogMetadata
    {
        internal FullScanStartupFingerprintLogMetadata(bool armed,
            bool inquiryObserved, int initialScannerReadyAttempts,
            int initialScannerReadyNotReadyCount,
            bool initialScannerReadyExact,
            bool initialScannerReadyCompleted,
            int extraScannerReadyAttempts,
            int extraScannerReadyNotReadyCount,
            bool extraScannerReadyExact, bool extraScannerReadyCompleted,
            string startupWriteSha256, string setWindowSha256,
            string completedSetWindowSha256, int firstImageWidth,
            int firstImageLength, int firstImageSelector,
            string firstImageCdb,
            bool failureBeforeTerminalFingerprint)
        {
            Armed = armed;
            InquiryObserved = inquiryObserved;
            InitialScannerReadyAttempts = initialScannerReadyAttempts;
            InitialScannerReadyNotReadyCount =
                initialScannerReadyNotReadyCount;
            InitialScannerReadyExact = initialScannerReadyExact;
            InitialScannerReadyCompleted = initialScannerReadyCompleted;
            ExtraScannerReadyAttempts = extraScannerReadyAttempts;
            ExtraScannerReadyNotReadyCount =
                extraScannerReadyNotReadyCount;
            ExtraScannerReadyExact = extraScannerReadyExact;
            ExtraScannerReadyCompleted = extraScannerReadyCompleted;
            StartupWriteSha256 = startupWriteSha256;
            SetWindowSha256 = setWindowSha256;
            CompletedSetWindowSha256 = completedSetWindowSha256;
            FirstImageWidth = firstImageWidth;
            FirstImageLength = firstImageLength;
            FirstImageSelector = firstImageSelector;
            FirstImageCdb = firstImageCdb;
            FailureBeforeTerminalFingerprint =
                failureBeforeTerminalFingerprint;
        }

        internal bool Armed { get; private set; }
        internal bool InquiryObserved { get; private set; }
        internal int InitialScannerReadyAttempts { get; private set; }
        internal int InitialScannerReadyNotReadyCount { get; private set; }
        internal bool InitialScannerReadyExact { get; private set; }
        internal bool InitialScannerReadyCompleted { get; private set; }
        internal int ExtraScannerReadyAttempts { get; private set; }
        internal int ExtraScannerReadyNotReadyCount { get; private set; }
        internal bool ExtraScannerReadyExact { get; private set; }
        internal bool ExtraScannerReadyCompleted { get; private set; }
        internal string StartupWriteSha256 { get; private set; }
        internal string SetWindowSha256 { get; private set; }
        internal string CompletedSetWindowSha256 { get; private set; }
        internal int FirstImageWidth { get; private set; }
        internal int FirstImageLength { get; private set; }
        internal int FirstImageSelector { get; private set; }
        internal string FirstImageCdb { get; private set; }
        internal bool FailureBeforeTerminalFingerprint { get; private set; }
        internal bool StartupWriteObserved
        {
            get { return StartupWriteSha256 != null; }
        }
        internal bool SetWindowObserved
        {
            get { return SetWindowSha256 != null; }
        }
        internal bool FullScanSetWindowCompleted
        {
            get { return CompletedSetWindowSha256 != null; }
        }
        internal bool FirstImageReadObserved
        {
            get { return FirstImageCdb != null; }
        }
        internal bool HasExclusiveTerminalFingerprint
        {
            get { return StartupWriteObserved != SetWindowObserved; }
        }
    }
}
