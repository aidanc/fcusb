// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Usb2Xchange.FlexColorAspiLauncher
{
    internal static class PreviewObserveLogInspector
    {
        internal const int PreviewBurstRows = 8;
        internal const int PreviewStreamRows = 256;
        internal const int PreviewShortRetryMaximum = 64;
        internal const int PreviewShortRetryConsecutiveMaximum = 8;
        internal const int PreviewShortRetryMaximumSubmissions = 320;
        internal const int PreviewNaturalRows = 996;
        internal const int PreviewNaturalShortRetryMaximum = 512;
        internal const int PreviewNaturalScannerReadyMaximum = 1024;
        internal const int PreviewNaturalMaximumSubmissions = 1508;
        internal const int PreviewNaturalPerRowShortRetryMaximum = 7968;
        internal const int PreviewNaturalPerRowMaximumSubmissions = 8964;
        internal const int PreviewPoweredNaturalRows = 911;
        internal const int PreviewPoweredNaturalShortRetryMaximum = 7288;
        internal const int PreviewPoweredConsecutiveShortRetryMaximum = 32;
        internal const int PreviewPoweredNaturalMaximumSubmissions = 8199;
        private const int PreviewRowBytes = 3996;
        private const int PreviewRowWidth = 666;
        private const string CleanupSetWindowSha256 =
            "3092FA15361A422D558EF968164EE6CB00B854413630CC0C07A4F62877515A9D";
        private static readonly Regex CleanupCandidatePattern = new Regex(
            "LIVE ASPI exact post-image cleanup SET WINDOW successor " +
            "observed and blocked before USB: ordinal=([0-9]+)/2, " +
            "payload-sha256=([0-9A-Fa-f]{64})\\. Cleanup permission is " +
            "not enabled\\.", RegexOptions.CultureInvariant);
        private static readonly Regex CleanupCompletionPattern = new Regex(
            "LIVE ASPI exact (post-image|cancellation) cleanup SET WINDOW " +
            "completed: " +
            "ordinal=([0-9]+)/2, status=0x([0-9A-Fa-f]{2}), " +
            "actual=([0-9]+), residue=([0-9]+), " +
            "payload-sha256=([0-9A-Fa-f]{64})\\.",
            RegexOptions.CultureInvariant);
        private static readonly Regex PostWindowResponsePattern = new Regex(
            "LIVE ASPI operational ScannerReady response: origin=Await" +
            "(First|Second)PostWindowScannerReady, attempt=([0-9]+)/256, " +
            "phase-elapsed-ms=([0-9]+)/60000, first=0x([0-9A-Fa-f]{2}), " +
            "second=0x([0-9A-Fa-f]{2})\\.",
            RegexOptions.CultureInvariant);
        private static readonly Regex FirstImageReadPattern = new Regex(
            "LIVE ASPI exact first Preview image READ completed once: " +
            "status=0x([0-9A-Fa-f]{2}), requested=([0-9]+), " +
            "actual=([0-9]+), residue=([0-9]+), width=([0-9]+), " +
            "selector=0x([0-9A-Fa-f]{2}), " +
            "payload-sha256=([0-9A-Fa-f]{64})\\.",
            RegexOptions.CultureInvariant);
        private static readonly Regex BurstImageReadPattern = new Regex(
            "LIVE ASPI exact bounded Preview image READ completed: " +
            "row=([0-9]+)/([0-9]+), status=0x([0-9A-Fa-f]{2}), " +
            "requested=([0-9]+), actual=([0-9]+), residue=([0-9]+), " +
            "width=([0-9]+), selector=0x([0-9A-Fa-f]{2}), " +
            "payload-sha256=([0-9A-Fa-f]{64})\\.",
            RegexOptions.CultureInvariant);
        private static readonly Regex StreamImageReadPattern = new Regex(
            "LIVE ASPI exact bounded Preview stream image READ completed: " +
            "row=([0-9]+)/([0-9]+), status=0x([0-9A-Fa-f]{2}), " +
            "requested=([0-9]+), actual=([0-9]+), residue=([0-9]+), " +
            "width=([0-9]+), selector=0x([0-9A-Fa-f]{2}), " +
            "payload-sha256=([0-9A-Fa-f]{64})\\.",
            RegexOptions.CultureInvariant);
        private static readonly Regex InStreamScannerReadyPattern = new Regex(
            "LIVE ASPI in-stream ScannerReady response: " +
            "after-rows=([0-9]+), poll=([0-9]+)/([0-9]+), " +
            "status=0x([0-9A-Fa-f]{2}), actual=([0-9]+), " +
            "residue=([0-9]+), first=0x([0-9A-Fa-f]{2}), " +
            "second=0x([0-9A-Fa-f]{2})\\.",
            RegexOptions.CultureInvariant);
        private static readonly Regex ShortRetryImageReadPattern = new Regex(
            "LIVE ASPI exact bounded Preview short-retry stream image READ " +
            "completed: row=([0-9]+)/([0-9]+), " +
            "status=0x([0-9A-Fa-f]{2}), " +
            "requested=([0-9]+), actual=([0-9]+), residue=([0-9]+), " +
            "width=([0-9]+), selector=0x([0-9A-Fa-f]{2}), " +
            "submission=([0-9]+)/([0-9]+), " +
            "short-retries=([0-9]+)/([0-9]+), " +
            "payload-sha256=([0-9A-Fa-f]{64})\\.",
            RegexOptions.CultureInvariant);
        private static readonly Regex ExactShortRetryPattern = new Regex(
            "LIVE ASPI exact Preview short completion converted to bounded " +
            "target BUSY: row=([0-9]+)/([0-9]+), " +
            "short-retry=([0-9]+)/([0-9]+), " +
            "consecutive=([0-9]+)/([0-9]+), " +
            "submission=([0-9]+)/([0-9]+), " +
            "status=0x([0-9A-Fa-f]{2}), requested=([0-9]+), " +
            "actual=([0-9]+), residue=([0-9]+), width=([0-9]+), " +
            "selector=0x([0-9A-Fa-f]{2}), " +
            "payload-sha256=([0-9A-Fa-f]{64})\\. No short payload bytes " +
            "were copied to FlexColor\\.",
            RegexOptions.CultureInvariant);
        private static readonly Regex ShortRetryCompletedPattern = new Regex(
            "LIVE ASPI exact bounded Preview short-retry stream completed: " +
            "rows=([0-9]+)/([0-9]+), " +
            "submissions=([0-9]+)/([0-9]+), " +
            "short-retries=([0-9]+)/([0-9]+)\\.",
            RegexOptions.CultureInvariant);
        internal const string SetWindowCompletedMarker =
            "LIVE ASPI exact Preview SET WINDOW completed once:";
        internal const string PredictedReadBlockedMarker =
            "LIVE ASPI observed the predicted first Preview image READ";
        internal const string FirstImageReadCompletedMarker =
            "LIVE ASPI exact first Preview image READ completed once:";
        internal const string BurstImageReadCompletedMarker =
            "LIVE ASPI exact bounded Preview image burst completed: rows=8/8.";
        internal const string BurstImageReadFailedMarker =
            "LIVE ASPI bounded Preview image burst failed closed:";
        internal const string StreamImageReadCompletedMarker =
            "LIVE ASPI exact bounded Preview stream completed: " +
            "rows=256/256.";
        internal const string StreamImageReadFailedMarker =
            "LIVE ASPI bounded Preview stream failed closed:";
        internal const string ShortRetryStreamImageReadCompletedMarker =
            "LIVE ASPI exact bounded Preview short-retry stream completed:";
        internal const string ShortRetryStreamImageReadFailedMarker =
            "LIVE ASPI bounded Preview short-retry stream failed closed:";
        internal const string ManifestRejectedMarker =
            "Preview SET WINDOW payload is not the pinned capture; " +
            "payload-sha256=";
        internal const string TransportRejectedMarker =
            "Preview SET WINDOW transport failed after candidate " +
            "validation; payload-sha256=";
        internal const string CleanupTransportFailedMarker =
            "Post-image cleanup SET WINDOW transport failed after candidate " +
            "validation; payload-sha256=";
        internal const string CandidateValidatedMarker =
            "Preview SET WINDOW candidate validated for transport; " +
            "payload-sha256=";
        internal const string StructuredMetadataMarker =
            "Preview SET WINDOW structured metadata captured without " +
            "retaining the payload:";
        internal const string PostWindowNotReadyMarker =
            "LIVE ASPI post-window ScannerReady was not ready;";
        internal const string PostWindowRetryLimitMarker =
            "LIVE ASPI post-window ScannerReady retry limit reached";

        internal static PreviewObserveLogMetadata Inspect(string text)
        {
            text = text ?? string.Empty;
            bool setWindowCompleted = Contains(text,
                SetWindowCompletedMarker);
            int cleanupCandidateCount = 0;
            bool cleanupCandidatesExact = true;
            foreach (Match cleanup in CleanupCandidatePattern.Matches(text))
            {
                ++cleanupCandidateCount;
                int ordinal = int.Parse(cleanup.Groups[1].Value,
                    CultureInfo.InvariantCulture);
                string hash = cleanup.Groups[2].Value.ToUpperInvariant();
                if (ordinal != cleanupCandidateCount ||
                    cleanupCandidateCount > 2 ||
                    !string.Equals(hash, CleanupSetWindowSha256,
                        StringComparison.Ordinal))
                {
                    cleanupCandidatesExact = false;
                }
            }
            int cleanupCompletionCount = 0;
            int cancellationCleanupCompletionCount = 0;
            bool cleanupCompletionsExact = true;
            foreach (Match cleanup in
                    CleanupCompletionPattern.Matches(text))
            {
                ++cleanupCompletionCount;
                if (string.Equals(cleanup.Groups[1].Value, "cancellation",
                        StringComparison.Ordinal))
                {
                    ++cancellationCleanupCompletionCount;
                }
                int ordinal = int.Parse(cleanup.Groups[2].Value,
                    CultureInfo.InvariantCulture);
                int status = Convert.ToInt32(cleanup.Groups[3].Value, 16);
                int actual = int.Parse(cleanup.Groups[4].Value,
                    CultureInfo.InvariantCulture);
                int residue = int.Parse(cleanup.Groups[5].Value,
                    CultureInfo.InvariantCulture);
                string hash = cleanup.Groups[6].Value.ToUpperInvariant();
                if (ordinal != cleanupCompletionCount ||
                    cleanupCompletionCount > 2 || status != 0 ||
                    actual != 84 || residue != 0 ||
                    !string.Equals(hash, CleanupSetWindowSha256,
                        StringComparison.Ordinal))
                {
                    cleanupCompletionsExact = false;
                }
            }

            // A later cleanup descriptor may intentionally fail the pinned
            // initial-command manifest. Once the initial SET WINDOW completed,
            // that later warning cannot retroactively classify it as rejected.
            bool manifestRejected = !setWindowCompleted && Contains(text,
                ManifestRejectedMarker);
            bool transportRejected = !setWindowCompleted && Contains(text,
                TransportRejectedMarker);
            bool cleanupTransportFailed = Contains(text,
                CleanupTransportFailedMarker);
            string payloadSha256 = FindSha256After(text,
                CandidateValidatedMarker) ??
                (manifestRejected
                    ? FindSha256After(text, ManifestRejectedMarker)
                    : null) ??
                (transportRejected
                    ? FindSha256After(text, TransportRejectedMarker)
                    : null);

            int firstAttempts = 0;
            int firstNotReady = 0;
            int firstElapsedMilliseconds = 0;
            int firstByte = -1;
            int firstSecondByte = -1;
            int secondAttempts = 0;
            int secondNotReady = 0;
            int secondElapsedMilliseconds = 0;
            int secondByte = -1;
            int secondSecondByte = -1;
            MatchCollection responses = PostWindowResponsePattern.Matches(text);
            foreach (Match response in responses)
            {
                bool firstPhase = string.Equals(response.Groups[1].Value,
                    "First", StringComparison.Ordinal);
                int attempt = int.Parse(response.Groups[2].Value,
                    CultureInfo.InvariantCulture);
                int elapsed = int.Parse(response.Groups[3].Value,
                    CultureInfo.InvariantCulture);
                int responseFirstByte = Convert.ToInt32(
                    response.Groups[4].Value, 16);
                int responseSecondByte = Convert.ToInt32(
                    response.Groups[5].Value, 16);
                if (firstPhase)
                {
                    firstAttempts = attempt;
                    firstElapsedMilliseconds = elapsed;
                    firstByte = responseFirstByte;
                    firstSecondByte = responseSecondByte;
                    if (responseFirstByte != 0)
                    {
                        ++firstNotReady;
                    }
                }
                else
                {
                    secondAttempts = attempt;
                    secondElapsedMilliseconds = elapsed;
                    secondByte = responseFirstByte;
                    secondSecondByte = responseSecondByte;
                    if (responseFirstByte != 0)
                    {
                        ++secondNotReady;
                    }
                }
            }

            bool firstImageReadCompleted = false;
            int firstImageReadStatus = -1;
            int firstImageReadRequested = 0;
            int firstImageReadActual = 0;
            int firstImageReadResidue = 0;
            int firstImageReadWidth = 0;
            int firstImageReadSelector = -1;
            string firstImageReadSha256 = null;
            Match imageRead = FirstImageReadPattern.Match(text);
            if (imageRead.Success)
            {
                firstImageReadCompleted = true;
                firstImageReadStatus = Convert.ToInt32(
                    imageRead.Groups[1].Value, 16);
                firstImageReadRequested = int.Parse(
                    imageRead.Groups[2].Value, CultureInfo.InvariantCulture);
                firstImageReadActual = int.Parse(imageRead.Groups[3].Value,
                    CultureInfo.InvariantCulture);
                firstImageReadResidue = int.Parse(imageRead.Groups[4].Value,
                    CultureInfo.InvariantCulture);
                firstImageReadWidth = int.Parse(imageRead.Groups[5].Value,
                    CultureInfo.InvariantCulture);
                firstImageReadSelector = Convert.ToInt32(
                    imageRead.Groups[6].Value, 16);
                firstImageReadSha256 =
                    imageRead.Groups[7].Value.ToUpperInvariant();
            }

            int burstImageReadCount = 0;
            int burstImageReadLimit = 0;
            bool burstImageReadsExact = true;
            MatchCollection burstReads = BurstImageReadPattern.Matches(text);
            foreach (Match burstRead in burstReads)
            {
                int row = int.Parse(burstRead.Groups[1].Value,
                    CultureInfo.InvariantCulture);
                int limit = int.Parse(burstRead.Groups[2].Value,
                    CultureInfo.InvariantCulture);
                int status = Convert.ToInt32(burstRead.Groups[3].Value, 16);
                int requested = int.Parse(burstRead.Groups[4].Value,
                    CultureInfo.InvariantCulture);
                int actual = int.Parse(burstRead.Groups[5].Value,
                    CultureInfo.InvariantCulture);
                int residue = int.Parse(burstRead.Groups[6].Value,
                    CultureInfo.InvariantCulture);
                int width = int.Parse(burstRead.Groups[7].Value,
                    CultureInfo.InvariantCulture);
                int selector = Convert.ToInt32(
                    burstRead.Groups[8].Value, 16);
                ++burstImageReadCount;
                if (row != burstImageReadCount ||
                    limit != PreviewBurstRows || status != 0 ||
                    requested != PreviewRowBytes || actual != requested ||
                    residue != 0 || width != PreviewRowWidth ||
                    selector != 0x28)
                {
                    burstImageReadsExact = false;
                }
                burstImageReadLimit = limit;
            }
            bool burstImageReadCompleted = burstImageReadsExact &&
                burstImageReadCount == PreviewBurstRows &&
                Contains(text, BurstImageReadCompletedMarker);
            bool burstImageReadFailed = Contains(text,
                BurstImageReadFailedMarker);

            int streamImageReadCount = 0;
            int streamImageReadLimit = 0;
            bool streamImageReadsExact = true;
            MatchCollection streamReads = StreamImageReadPattern.Matches(text);
            foreach (Match streamRead in streamReads)
            {
                int row = int.Parse(streamRead.Groups[1].Value,
                    CultureInfo.InvariantCulture);
                int limit = int.Parse(streamRead.Groups[2].Value,
                    CultureInfo.InvariantCulture);
                int status = Convert.ToInt32(streamRead.Groups[3].Value, 16);
                int requested = int.Parse(streamRead.Groups[4].Value,
                    CultureInfo.InvariantCulture);
                int actual = int.Parse(streamRead.Groups[5].Value,
                    CultureInfo.InvariantCulture);
                int residue = int.Parse(streamRead.Groups[6].Value,
                    CultureInfo.InvariantCulture);
                int width = int.Parse(streamRead.Groups[7].Value,
                    CultureInfo.InvariantCulture);
                int selector = Convert.ToInt32(
                    streamRead.Groups[8].Value, 16);
                ++streamImageReadCount;
                if (row != streamImageReadCount ||
                    limit != PreviewStreamRows || status != 0 ||
                    requested != PreviewRowBytes || actual != requested ||
                    residue != 0 || width != PreviewRowWidth ||
                    selector != 0x28)
                {
                    streamImageReadsExact = false;
                }
                streamImageReadLimit = limit;
            }

            int inStreamScannerReadyPollCount = 0;
            int inStreamScannerReadyMaximum = 0;
            int lastInStreamScannerReadyRow = 0;
            bool inStreamScannerReadyPollsExact = true;
            MatchCollection inStreamPolls =
                InStreamScannerReadyPattern.Matches(text);
            foreach (Match poll in inStreamPolls)
            {
                int afterRows = int.Parse(poll.Groups[1].Value,
                    CultureInfo.InvariantCulture);
                int pollIndex = int.Parse(poll.Groups[2].Value,
                    CultureInfo.InvariantCulture);
                int pollMaximum = int.Parse(poll.Groups[3].Value,
                    CultureInfo.InvariantCulture);
                int status = Convert.ToInt32(poll.Groups[4].Value, 16);
                int actual = int.Parse(poll.Groups[5].Value,
                    CultureInfo.InvariantCulture);
                int residue = int.Parse(poll.Groups[6].Value,
                    CultureInfo.InvariantCulture);
                int first = Convert.ToInt32(poll.Groups[7].Value, 16);
                int second = Convert.ToInt32(poll.Groups[8].Value, 16);
                ++inStreamScannerReadyPollCount;
                if (pollIndex != inStreamScannerReadyPollCount ||
                    (pollMaximum != PreviewStreamRows &&
                     pollMaximum != PreviewNaturalScannerReadyMaximum) ||
                    (inStreamScannerReadyMaximum != 0 &&
                     pollMaximum != inStreamScannerReadyMaximum) ||
                    afterRows <= 0 ||
                    afterRows >= (pollMaximum == PreviewStreamRows
                        ? PreviewStreamRows : PreviewNaturalRows) ||
                    afterRows < lastInStreamScannerReadyRow || status != 0 ||
                    actual != 2 || residue != 0 ||
                    (first != 0 && first != 8) || second != 0)
                {
                    inStreamScannerReadyPollsExact = false;
                }
                inStreamScannerReadyMaximum = pollMaximum;
                lastInStreamScannerReadyRow = afterRows;
            }
            inStreamScannerReadyPollsExact =
                inStreamScannerReadyPollsExact &&
                inStreamScannerReadyPollCount > 0;
            bool streamImageReadCompleted = streamImageReadsExact &&
                streamImageReadCount == PreviewStreamRows &&
                Contains(text, StreamImageReadCompletedMarker);
            bool streamImageReadFailed = Contains(text,
                StreamImageReadFailedMarker);

            int shortRetryStreamImageReadCount = 0;
            int shortRetryStreamImageReadLimit = 0;
            int shortRetryStreamMaximumSubmissions = 0;
            int shortRetryMaximum = 0;
            int shortRetryStreamLastSubmission = 0;
            int shortRetryStreamLastReportedRetryCount = 0;
            bool shortRetryStreamImageReadsExact = true;
            MatchCollection shortRetryStreamReads =
                ShortRetryImageReadPattern.Matches(text);
            foreach (Match streamRead in shortRetryStreamReads)
            {
                int row = int.Parse(streamRead.Groups[1].Value,
                    CultureInfo.InvariantCulture);
                int rowLimit = int.Parse(streamRead.Groups[2].Value,
                    CultureInfo.InvariantCulture);
                int status = Convert.ToInt32(streamRead.Groups[3].Value, 16);
                int requested = int.Parse(streamRead.Groups[4].Value,
                    CultureInfo.InvariantCulture);
                int actual = int.Parse(streamRead.Groups[5].Value,
                    CultureInfo.InvariantCulture);
                int residue = int.Parse(streamRead.Groups[6].Value,
                    CultureInfo.InvariantCulture);
                int width = int.Parse(streamRead.Groups[7].Value,
                    CultureInfo.InvariantCulture);
                int selector = Convert.ToInt32(
                    streamRead.Groups[8].Value, 16);
                int submission = int.Parse(streamRead.Groups[9].Value,
                    CultureInfo.InvariantCulture);
                int submissionMaximum = int.Parse(
                    streamRead.Groups[10].Value,
                    CultureInfo.InvariantCulture);
                int retryCount = int.Parse(streamRead.Groups[11].Value,
                    CultureInfo.InvariantCulture);
                int retryMaximum = int.Parse(streamRead.Groups[12].Value,
                    CultureInfo.InvariantCulture);
                ++shortRetryStreamImageReadCount;
                int consecutiveMaximum = rowLimit ==
                        PreviewPoweredNaturalRows
                    ? PreviewPoweredConsecutiveShortRetryMaximum
                    : PreviewShortRetryConsecutiveMaximum;
                if (!IsSupportedShortRetryPolicy(rowLimit, retryMaximum,
                        consecutiveMaximum, submissionMaximum) ||
                    (shortRetryStreamImageReadLimit != 0 &&
                     (rowLimit != shortRetryStreamImageReadLimit ||
                      retryMaximum != shortRetryMaximum ||
                      submissionMaximum !=
                        shortRetryStreamMaximumSubmissions)) ||
                    row != shortRetryStreamImageReadCount ||
                    row > rowLimit || status != 0 ||
                    requested != PreviewRowBytes || actual != requested ||
                    residue != 0 || width != PreviewRowWidth ||
                    selector != 0x28 || submission != row + retryCount ||
                    submission <= shortRetryStreamLastSubmission ||
                    retryCount < shortRetryStreamLastReportedRetryCount ||
                    retryCount > retryMaximum ||
                    submission > submissionMaximum)
                {
                    shortRetryStreamImageReadsExact = false;
                }
                shortRetryStreamImageReadLimit = rowLimit;
                shortRetryStreamMaximumSubmissions = submissionMaximum;
                shortRetryMaximum = retryMaximum;
                shortRetryStreamLastSubmission = submission;
                shortRetryStreamLastReportedRetryCount = retryCount;
            }

            int exactShortRetryCount = 0;
            int lastExactShortRetryRow = 0;
            int lastExactShortRetryConsecutive = 0;
            int lastExactShortRetrySubmission = 0;
            bool exactShortRetriesValid = true;
            MatchCollection exactShortRetries =
                ExactShortRetryPattern.Matches(text);
            foreach (Match shortRetry in exactShortRetries)
            {
                int row = int.Parse(shortRetry.Groups[1].Value,
                    CultureInfo.InvariantCulture);
                int rowLimit = int.Parse(shortRetry.Groups[2].Value,
                    CultureInfo.InvariantCulture);
                int retryIndex = int.Parse(shortRetry.Groups[3].Value,
                    CultureInfo.InvariantCulture);
                int retryMaximum = int.Parse(shortRetry.Groups[4].Value,
                    CultureInfo.InvariantCulture);
                int consecutive = int.Parse(shortRetry.Groups[5].Value,
                    CultureInfo.InvariantCulture);
                int consecutiveMaximum = int.Parse(
                    shortRetry.Groups[6].Value,
                    CultureInfo.InvariantCulture);
                int submission = int.Parse(shortRetry.Groups[7].Value,
                    CultureInfo.InvariantCulture);
                int submissionMaximum = int.Parse(
                    shortRetry.Groups[8].Value,
                    CultureInfo.InvariantCulture);
                int status = Convert.ToInt32(shortRetry.Groups[9].Value, 16);
                int requested = int.Parse(shortRetry.Groups[10].Value,
                    CultureInfo.InvariantCulture);
                int actual = int.Parse(shortRetry.Groups[11].Value,
                    CultureInfo.InvariantCulture);
                int residue = int.Parse(shortRetry.Groups[12].Value,
                    CultureInfo.InvariantCulture);
                int width = int.Parse(shortRetry.Groups[13].Value,
                    CultureInfo.InvariantCulture);
                int selector = Convert.ToInt32(
                    shortRetry.Groups[14].Value, 16);
                ++exactShortRetryCount;
                int expectedConsecutive = row == lastExactShortRetryRow
                    ? lastExactShortRetryConsecutive + 1
                    : 1;
                if (!IsSupportedShortRetryPolicy(rowLimit, retryMaximum,
                        consecutiveMaximum, submissionMaximum) ||
                    (shortRetryStreamImageReadLimit != 0 &&
                     (rowLimit != shortRetryStreamImageReadLimit ||
                      retryMaximum != shortRetryMaximum ||
                      submissionMaximum !=
                        shortRetryStreamMaximumSubmissions)) ||
                    retryIndex != exactShortRetryCount || row <= 0 ||
                    row > rowLimit || consecutive <= 0 ||
                    consecutive > consecutiveMaximum ||
                    row < lastExactShortRetryRow ||
                    consecutive != expectedConsecutive ||
                    submission != (row - 1) + retryIndex || status != 0 ||
                    requested != PreviewRowBytes || actual != 10 ||
                    residue != PreviewRowBytes - 10 ||
                    width != PreviewRowWidth || selector != 0x28)
                {
                    exactShortRetriesValid = false;
                }
                shortRetryStreamImageReadLimit = rowLimit;
                shortRetryStreamMaximumSubmissions = submissionMaximum;
                shortRetryMaximum = retryMaximum;
                lastExactShortRetryRow = row;
                lastExactShortRetryConsecutive = consecutive;
                lastExactShortRetrySubmission = submission;
            }
            if (exactShortRetryCount > shortRetryMaximum)
            {
                exactShortRetriesValid = false;
            }

            Match shortRetryCompleted = ShortRetryCompletedPattern.Match(text);
            int shortRetryStreamReportedRows = shortRetryCompleted.Success
                ? int.Parse(shortRetryCompleted.Groups[1].Value,
                    CultureInfo.InvariantCulture)
                : 0;
            int shortRetryStreamReportedRowLimit = shortRetryCompleted.Success
                ? int.Parse(shortRetryCompleted.Groups[2].Value,
                    CultureInfo.InvariantCulture)
                : 0;
            int shortRetryStreamReportedSubmissionCount =
                shortRetryCompleted.Success
                ? int.Parse(shortRetryCompleted.Groups[3].Value,
                    CultureInfo.InvariantCulture)
                : 0;
            int shortRetryStreamReportedSubmissionMaximum =
                shortRetryCompleted.Success
                ? int.Parse(shortRetryCompleted.Groups[4].Value,
                    CultureInfo.InvariantCulture)
                : 0;
            int shortRetryStreamSubmissionCount = Math.Max(
                shortRetryStreamLastSubmission,
                lastExactShortRetrySubmission);
            int shortRetryStreamCompletionRetryCount =
                shortRetryCompleted.Success
                ? int.Parse(shortRetryCompleted.Groups[5].Value,
                    CultureInfo.InvariantCulture)
                : 0;
            int shortRetryStreamReportedRetryMaximum =
                shortRetryCompleted.Success
                ? int.Parse(shortRetryCompleted.Groups[6].Value,
                    CultureInfo.InvariantCulture)
                : 0;
            int reportedConsecutiveMaximum =
                shortRetryStreamReportedRowLimit ==
                    PreviewPoweredNaturalRows
                ? PreviewPoweredConsecutiveShortRetryMaximum
                : PreviewShortRetryConsecutiveMaximum;
            bool shortRetryStreamImageReadCompleted =
                shortRetryCompleted.Success &&
                IsSupportedShortRetryPolicy(
                    shortRetryStreamReportedRowLimit,
                    shortRetryStreamReportedRetryMaximum,
                    reportedConsecutiveMaximum,
                    shortRetryStreamReportedSubmissionMaximum) &&
                shortRetryStreamReportedRows ==
                    shortRetryStreamReportedRowLimit &&
                shortRetryStreamReportedRowLimit ==
                    shortRetryStreamImageReadLimit &&
                shortRetryStreamReportedRetryMaximum == shortRetryMaximum &&
                shortRetryStreamReportedSubmissionMaximum ==
                    shortRetryStreamMaximumSubmissions &&
                shortRetryStreamImageReadsExact && exactShortRetriesValid &&
                shortRetryStreamImageReadCount ==
                    shortRetryStreamImageReadLimit &&
                exactShortRetryCount ==
                    shortRetryStreamCompletionRetryCount &&
                shortRetryStreamReportedSubmissionCount ==
                    shortRetryStreamSubmissionCount &&
                shortRetryStreamSubmissionCount ==
                    shortRetryStreamImageReadLimit +
                    exactShortRetryCount &&
                shortRetryStreamSubmissionCount <=
                    shortRetryStreamMaximumSubmissions;
            int expectedInStreamScannerReadyMaximum =
                shortRetryStreamImageReadLimit == PreviewStreamRows
                    ? PreviewStreamRows
                    : PreviewNaturalScannerReadyMaximum;
            if (inStreamScannerReadyPollCount != 0 &&
                shortRetryStreamImageReadLimit != 0 &&
                (inStreamScannerReadyMaximum !=
                    expectedInStreamScannerReadyMaximum ||
                 (shortRetryStreamImageReadLimit != 0 &&
                  lastInStreamScannerReadyRow >=
                    shortRetryStreamImageReadLimit)))
            {
                inStreamScannerReadyPollsExact = false;
            }
            bool shortRetryStreamImageReadFailed = Contains(text,
                ShortRetryStreamImageReadFailedMarker);

            return new PreviewObserveLogMetadata(
                Contains(text, "GetASPI32SupportInfo"),
                Contains(text, "LIVE ASPI target-5 identity:"),
                setWindowCompleted,
                Contains(text, PredictedReadBlockedMarker),
                manifestRejected,
                transportRejected,
                Contains(text, CandidateValidatedMarker),
                Contains(text, StructuredMetadataMarker),
                Contains(text, PostWindowNotReadyMarker),
                Contains(text, PostWindowRetryLimitMarker),
                firstAttempts, firstNotReady,
                firstElapsedMilliseconds, firstByte, firstSecondByte,
                secondAttempts, secondNotReady,
                secondElapsedMilliseconds, secondByte, secondSecondByte,
                payloadSha256, firstImageReadCompleted,
                firstImageReadStatus, firstImageReadRequested,
                firstImageReadActual, firstImageReadResidue,
                firstImageReadWidth, firstImageReadSelector,
                firstImageReadSha256, burstImageReadCompleted,
                burstImageReadCount, burstImageReadLimit,
                burstImageReadsExact, burstImageReadFailed,
                streamImageReadCompleted, streamImageReadCount,
                streamImageReadLimit, streamImageReadsExact,
                streamImageReadFailed, inStreamScannerReadyPollCount,
                inStreamScannerReadyPollsExact,
                shortRetryStreamImageReadCompleted,
                shortRetryStreamImageReadCount,
                shortRetryStreamImageReadsExact,
                shortRetryStreamImageReadFailed, exactShortRetryCount,
                exactShortRetriesValid, shortRetryStreamSubmissionCount,
                shortRetryStreamImageReadLimit, shortRetryMaximum,
                shortRetryStreamMaximumSubmissions, cleanupCandidateCount,
                cleanupCandidatesExact, cleanupCompletionCount,
                cleanupCompletionsExact, cancellationCleanupCompletionCount,
                cleanupTransportFailed);
        }

        private static bool IsSupportedShortRetryPolicy(int rows,
            int shortRetries, int consecutiveShortRetries, int submissions)
        {
            return (consecutiveShortRetries ==
                    PreviewShortRetryConsecutiveMaximum &&
                 ((rows == PreviewStreamRows &&
                  shortRetries == PreviewShortRetryMaximum &&
                  submissions == PreviewShortRetryMaximumSubmissions) ||
                 (rows == PreviewNaturalRows &&
                  shortRetries == PreviewNaturalShortRetryMaximum &&
                  submissions == PreviewNaturalMaximumSubmissions) ||
                 (rows == PreviewNaturalRows &&
                  shortRetries ==
                    PreviewNaturalPerRowShortRetryMaximum &&
                  submissions ==
                    PreviewNaturalPerRowMaximumSubmissions))) ||
                (consecutiveShortRetries ==
                    PreviewPoweredConsecutiveShortRetryMaximum &&
                 rows == PreviewPoweredNaturalRows &&
                 shortRetries == PreviewPoweredNaturalShortRetryMaximum &&
                 submissions == PreviewPoweredNaturalMaximumSubmissions);
        }

        private static bool Contains(string text, string marker)
        {
            return text.IndexOf(marker, StringComparison.Ordinal) >= 0;
        }

        private static string FindSha256After(string text, string marker)
        {
            int offset = text.IndexOf(marker, StringComparison.Ordinal);
            if (offset < 0)
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
    }

    internal sealed class PreviewObserveLogMetadata
    {
        internal PreviewObserveLogMetadata(bool support, bool identity,
            bool setWindowCompleted, bool predictedReadBlocked,
            bool manifestRejected, bool transportRejected,
            bool candidateValidated, bool structuredMetadataCaptured,
            bool postWindowNotReadyObserved,
            bool postWindowRetryLimitReached,
            int firstPostWindowAttempts, int firstPostWindowNotReadyCount,
            int firstPostWindowElapsedMilliseconds,
            int firstPostWindowFinalFirstByte,
            int firstPostWindowFinalSecondByte,
            int secondPostWindowAttempts, int secondPostWindowNotReadyCount,
            int secondPostWindowElapsedMilliseconds,
            int secondPostWindowFinalFirstByte,
            int secondPostWindowFinalSecondByte, string payloadSha256,
            bool firstImageReadCompleted, int firstImageReadStatus,
            int firstImageReadRequested, int firstImageReadActual,
            int firstImageReadResidue, int firstImageReadWidth,
            int firstImageReadSelector, string firstImageReadSha256,
            bool burstImageReadCompleted, int burstImageReadCount,
            int burstImageReadLimit, bool burstImageReadsExact,
            bool burstImageReadFailed, bool streamImageReadCompleted,
            int streamImageReadCount, int streamImageReadLimit,
            bool streamImageReadsExact, bool streamImageReadFailed,
            int inStreamScannerReadyPollCount,
            bool inStreamScannerReadyPollsExact,
            bool shortRetryStreamImageReadCompleted,
            int shortRetryStreamImageReadCount,
            bool shortRetryStreamImageReadsExact,
            bool shortRetryStreamImageReadFailed,
            int shortRetryCount, bool shortRetriesExact,
            int shortRetryStreamSubmissionCount,
            int shortRetryStreamImageReadLimit, int shortRetryMaximum,
            int shortRetryStreamMaximumSubmissions,
            int cleanupCandidateCount, bool cleanupCandidatesExact,
            int cleanupCompletionCount, bool cleanupCompletionsExact,
            int cancellationCleanupCompletionCount,
            bool cleanupTransportFailed)
        {
            Support = support;
            Identity = identity;
            SetWindowCompleted = setWindowCompleted;
            PredictedReadBlocked = predictedReadBlocked;
            ManifestRejected = manifestRejected;
            TransportRejected = transportRejected;
            CandidateValidated = candidateValidated;
            StructuredMetadataCaptured = structuredMetadataCaptured;
            PostWindowNotReadyObserved = postWindowNotReadyObserved;
            PostWindowRetryLimitReached = postWindowRetryLimitReached;
            FirstPostWindowAttempts = firstPostWindowAttempts;
            FirstPostWindowNotReadyCount = firstPostWindowNotReadyCount;
            FirstPostWindowElapsedMilliseconds =
                firstPostWindowElapsedMilliseconds;
            FirstPostWindowFinalFirstByte = firstPostWindowFinalFirstByte;
            FirstPostWindowFinalSecondByte = firstPostWindowFinalSecondByte;
            SecondPostWindowAttempts = secondPostWindowAttempts;
            SecondPostWindowNotReadyCount = secondPostWindowNotReadyCount;
            SecondPostWindowElapsedMilliseconds =
                secondPostWindowElapsedMilliseconds;
            SecondPostWindowFinalFirstByte = secondPostWindowFinalFirstByte;
            SecondPostWindowFinalSecondByte = secondPostWindowFinalSecondByte;
            PayloadSha256 = payloadSha256;
            FirstImageReadCompleted = firstImageReadCompleted;
            FirstImageReadStatus = firstImageReadStatus;
            FirstImageReadRequested = firstImageReadRequested;
            FirstImageReadActual = firstImageReadActual;
            FirstImageReadResidue = firstImageReadResidue;
            FirstImageReadWidth = firstImageReadWidth;
            FirstImageReadSelector = firstImageReadSelector;
            FirstImageReadSha256 = firstImageReadSha256;
            BurstImageReadCompleted = burstImageReadCompleted;
            BurstImageReadCount = burstImageReadCount;
            BurstImageReadLimit = burstImageReadLimit;
            BurstImageReadsExact = burstImageReadsExact;
            BurstImageReadFailed = burstImageReadFailed;
            StreamImageReadCompleted = streamImageReadCompleted;
            StreamImageReadCount = streamImageReadCount;
            StreamImageReadLimit = streamImageReadLimit;
            StreamImageReadsExact = streamImageReadsExact;
            StreamImageReadFailed = streamImageReadFailed;
            InStreamScannerReadyPollCount = inStreamScannerReadyPollCount;
            InStreamScannerReadyPollsExact =
                inStreamScannerReadyPollsExact;
            ShortRetryStreamImageReadCompleted =
                shortRetryStreamImageReadCompleted;
            ShortRetryStreamImageReadCount =
                shortRetryStreamImageReadCount;
            ShortRetryStreamImageReadsExact =
                shortRetryStreamImageReadsExact;
            ShortRetryStreamImageReadFailed =
                shortRetryStreamImageReadFailed;
            ShortRetryCount = shortRetryCount;
            ShortRetriesExact = shortRetriesExact;
            ShortRetryStreamSubmissionCount =
                shortRetryStreamSubmissionCount;
            ShortRetryStreamImageReadLimit =
                shortRetryStreamImageReadLimit;
            ShortRetryMaximum = shortRetryMaximum;
            ShortRetryStreamMaximumSubmissions =
                shortRetryStreamMaximumSubmissions;
            CleanupCandidateCount = cleanupCandidateCount;
            CleanupCandidatesExact = cleanupCandidatesExact;
            CleanupCompletionCount = cleanupCompletionCount;
            CleanupCompletionsExact = cleanupCompletionsExact;
            CancellationCleanupCompletionCount =
                cancellationCleanupCompletionCount;
            CleanupTransportFailed = cleanupTransportFailed;
        }

        internal bool Support { get; private set; }
        internal bool Identity { get; private set; }
        internal bool SetWindowCompleted { get; private set; }
        internal bool PredictedReadBlocked { get; private set; }
        internal bool ManifestRejected { get; private set; }
        internal bool TransportRejected { get; private set; }
        internal bool CandidateValidated { get; private set; }
        internal bool StructuredMetadataCaptured { get; private set; }
        internal bool PostWindowNotReadyObserved { get; private set; }
        internal bool PostWindowRetryLimitReached { get; private set; }
        internal int FirstPostWindowAttempts { get; private set; }
        internal int FirstPostWindowNotReadyCount { get; private set; }
        internal int FirstPostWindowElapsedMilliseconds { get; private set; }
        internal int FirstPostWindowFinalFirstByte { get; private set; }
        internal int FirstPostWindowFinalSecondByte { get; private set; }
        internal int SecondPostWindowAttempts { get; private set; }
        internal int SecondPostWindowNotReadyCount { get; private set; }
        internal int SecondPostWindowElapsedMilliseconds { get; private set; }
        internal int SecondPostWindowFinalFirstByte { get; private set; }
        internal int SecondPostWindowFinalSecondByte { get; private set; }
        internal string PayloadSha256 { get; private set; }
        internal bool FirstImageReadCompleted { get; private set; }
        internal int FirstImageReadStatus { get; private set; }
        internal int FirstImageReadRequested { get; private set; }
        internal int FirstImageReadActual { get; private set; }
        internal int FirstImageReadResidue { get; private set; }
        internal int FirstImageReadWidth { get; private set; }
        internal int FirstImageReadSelector { get; private set; }
        internal string FirstImageReadSha256 { get; private set; }
        internal bool BurstImageReadCompleted { get; private set; }
        internal int BurstImageReadCount { get; private set; }
        internal int BurstImageReadLimit { get; private set; }
        internal bool BurstImageReadsExact { get; private set; }
        internal bool BurstImageReadFailed { get; private set; }
        internal bool StreamImageReadCompleted { get; private set; }
        internal int StreamImageReadCount { get; private set; }
        internal int StreamImageReadLimit { get; private set; }
        internal bool StreamImageReadsExact { get; private set; }
        internal bool StreamImageReadFailed { get; private set; }
        internal int InStreamScannerReadyPollCount { get; private set; }
        internal bool InStreamScannerReadyPollsExact { get; private set; }
        internal bool ShortRetryStreamImageReadCompleted { get; private set; }
        internal int ShortRetryStreamImageReadCount { get; private set; }
        internal bool ShortRetryStreamImageReadsExact { get; private set; }
        internal bool ShortRetryStreamImageReadFailed { get; private set; }
        internal int ShortRetryCount { get; private set; }
        internal bool ShortRetriesExact { get; private set; }
        internal int ShortRetryStreamSubmissionCount { get; private set; }
        internal int ShortRetryStreamImageReadLimit { get; private set; }
        internal int ShortRetryMaximum { get; private set; }
        internal int ShortRetryStreamMaximumSubmissions { get; private set; }
        internal int CleanupCandidateCount { get; private set; }
        internal bool CleanupCandidatesExact { get; private set; }
        internal int CleanupCompletionCount { get; private set; }
        internal bool CleanupCompletionsExact { get; private set; }
        internal int CancellationCleanupCompletionCount { get; private set; }
        internal bool CleanupTransportFailed { get; private set; }

        internal bool ReachedTerminalBoundary
        {
            get
            {
                return PredictedReadBlocked || FirstImageReadCompleted ||
                    BurstImageReadCompleted ||
                    BurstImageReadFailed ||
                    StreamImageReadCompleted ||
                    StreamImageReadFailed ||
                    ShortRetryStreamImageReadCompleted ||
                    ShortRetryStreamImageReadFailed ||
                    CleanupCompletionCount == 2 ||
                    CleanupTransportFailed ||
                    ManifestRejected ||
                    TransportRejected || PostWindowRetryLimitReached;
            }
        }
    }
}
