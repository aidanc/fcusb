// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Text.RegularExpressions;

namespace Usb2Xchange.FlexColorAspiLauncher
{
    internal static class FullScanCompletionLogInspector
    {
        private static readonly Regex StreamPattern = new Regex(
            "LIVE ASPI exact bounded full-scan image stream completed: " +
            "rows=([0-9]+)/([0-9]+), submissions=([0-9]+)/([0-9]+), " +
            "short-retries=([0-9]+)/([0-9]+)\\.",
            RegexOptions.CultureInvariant);
        private static readonly Regex CleanupPattern = new Regex(
            "LIVE ASPI exact full-scan cleanup SET WINDOW completed: " +
            "ordinal=([12])/2, status=0x([0-9A-F]{2}), actual=([0-9]+), " +
            "residue=([0-9]+), payload-sha256=([0-9A-F]{64})\\.",
            RegexOptions.CultureInvariant);
        private const string RevalidationMarker =
            "LIVE ASPI full-scan in-stream operational INQUIRY " +
            "revalidation completed:";
        private static readonly Regex RevalidationPattern = new Regex(
            RevalidationMarker + " ordinal=([0-9]+)/([0-9]+), " +
            "after-rows=([0-9]+)/([0-9]+), status=0x([0-9A-F]{2}), " +
            "actual=([0-9]+), residue=([0-9]+), identity=M333\\.",
            RegexOptions.CultureInvariant);

        internal static FullScanCompletionLogMetadata Inspect(string text)
        {
            return Inspect(text, false);
        }

        internal static FullScanCompletionLogMetadata Inspect(string text,
            bool naturalCompletion)
        {
            return Inspect(text, naturalCompletion ? 996 : 762);
        }

        internal static FullScanCompletionLogMetadata Inspect(string text,
            int expectedRows)
        {
            if (expectedRows != 762 && expectedRows != 996 &&
                expectedRows != 997)
            {
                throw new ArgumentOutOfRangeException("expectedRows");
            }
            return InspectCore(text, expectedRows, expectedRows);
        }

        internal static FullScanCompletionLogMetadata InspectProgressBounded(
            string text, int minimumRows, int maximumRows)
        {
            if (minimumRows != 998 || maximumRows != 998)
            {
                throw new ArgumentOutOfRangeException("minimumRows");
            }
            return InspectCore(text, minimumRows, maximumRows);
        }

        private static FullScanCompletionLogMetadata InspectCore(string text,
            int minimumRows, int maximumRows)
        {
            text = text ?? string.Empty;
            Match stream = StreamPattern.Match(text);
            int rows = Parse(stream, 1);
            int rowLimit = Parse(stream, 2);
            int submissions = Parse(stream, 3);
            int maximumSubmissions = Parse(stream, 4);
            int shortRetries = Parse(stream, 5);
            int maximumShortRetries = Parse(stream, 6);
            int expectedMaximumRevalidations = (maximumRows + 29) / 30;
            string transportCompletionMarker =
                "LIVE ASPI exact full-scan transport completed after " +
                rows.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                " rows and two cleanup SET WINDOW completions.";
            bool firstCleanup = false;
            bool secondCleanup = false;
            bool cleanupExact = true;
            foreach (Match cleanup in CleanupPattern.Matches(text))
            {
                int ordinal = Parse(cleanup, 1);
                firstCleanup |= ordinal == 1;
                secondCleanup |= ordinal == 2;
                cleanupExact &= cleanup.Groups[2].Value == "00" &&
                    Parse(cleanup, 3) == 84 && Parse(cleanup, 4) == 0 &&
                    string.Equals(cleanup.Groups[5].Value,
                        "23B3C62B62096A50A58E6BC035D20C6C04E38F5D10A30BF27C6348D0C37EFB15",
                        StringComparison.Ordinal);
            }
            int revalidations = 0;
            int lastRevalidationRow = 0;
            bool revalidationsExact = true;
            foreach (Match revalidation in
                    RevalidationPattern.Matches(text))
            {
                ++revalidations;
                int row = Parse(revalidation, 3);
                revalidationsExact &= Parse(revalidation, 1) ==
                        revalidations && Parse(revalidation, 2) ==
                            expectedMaximumRevalidations &&
                    row > lastRevalidationRow && row > 0 &&
                    row < rows &&
                    Parse(revalidation, 4) == maximumRows &&
                    revalidation.Groups[5].Value == "00" &&
                    Parse(revalidation, 6) == 96 &&
                    Parse(revalidation, 7) == 0;
                lastRevalidationRow = row;
            }
            revalidationsExact &= revalidations <=
                    expectedMaximumRevalidations &&
                revalidations == CountOccurrences(text,
                    RevalidationMarker);
            int completionOffset = text.IndexOf(transportCompletionMarker,
                StringComparison.Ordinal);
            int failureOffset = text.IndexOf(
                "LIVE ASPI bounded full-scan image stream failed closed:",
                StringComparison.Ordinal);
            bool rowsInRange = rows >= minimumRows && rows <= maximumRows;
            return new FullScanCompletionLogMetadata(rows, rowLimit,
                submissions, maximumSubmissions, shortRetries,
                maximumShortRetries, firstCleanup, secondCleanup,
                cleanupExact, revalidations, revalidationsExact,
                completionOffset >= 0 && rowsInRange,
                failureOffset >= 0 &&
                    (completionOffset < 0 || failureOffset < completionOffset));
        }

        private static int CountOccurrences(string text, string marker)
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

        private static int Parse(Match match, int group)
        {
            if (match == null || !match.Success)
            {
                return 0;
            }
            return int.Parse(match.Groups[group].Value,
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    internal sealed class FullScanCompletionLogMetadata
    {
        internal FullScanCompletionLogMetadata(int rows, int rowLimit,
            int submissions, int maximumSubmissions, int shortRetries,
            int maximumShortRetries, bool firstCleanup, bool secondCleanup,
            bool cleanupExact, int inStreamInquiryRevalidations,
            bool inStreamInquiryRevalidationsExact, bool transportCompleted,
            bool failureBeforeCompletion)
        {
            Rows = rows;
            RowLimit = rowLimit;
            Submissions = submissions;
            MaximumSubmissions = maximumSubmissions;
            ShortRetries = shortRetries;
            MaximumShortRetries = maximumShortRetries;
            FirstCleanup = firstCleanup;
            SecondCleanup = secondCleanup;
            CleanupExact = cleanupExact;
            InStreamInquiryRevalidations = inStreamInquiryRevalidations;
            InStreamInquiryRevalidationsExact =
                inStreamInquiryRevalidationsExact;
            TransportCompleted = transportCompleted;
            FailureBeforeCompletion = failureBeforeCompletion;
        }

        internal int Rows { get; private set; }
        internal int RowLimit { get; private set; }
        internal int Submissions { get; private set; }
        internal int MaximumSubmissions { get; private set; }
        internal int ShortRetries { get; private set; }
        internal int MaximumShortRetries { get; private set; }
        internal bool FirstCleanup { get; private set; }
        internal bool SecondCleanup { get; private set; }
        internal bool CleanupExact { get; private set; }
        internal int InStreamInquiryRevalidations { get; private set; }
        internal bool InStreamInquiryRevalidationsExact { get; private set; }
        internal bool TransportCompleted { get; private set; }
        internal bool FailureBeforeCompletion { get; private set; }
    }
}
