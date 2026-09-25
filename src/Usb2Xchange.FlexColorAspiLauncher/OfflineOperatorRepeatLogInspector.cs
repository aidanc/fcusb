// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;

namespace Usb2Xchange.FlexColorAspiLauncher
{
    internal static class OfflineOperatorRepeatLogInspector
    {
        private const string RepeatMarker =
            "OFFLINE REPLAY armed exactly one repeat scan after the " +
            "completed Preview and exact INQUIRY prefix.";
        private const string FirstCompletionMarker =
            "completed two post-image SET WINDOW cleanup requests without " +
            "a WinUSB call; streams=1.";
        private const string SecondCompletionMarker =
            "completed two post-image SET WINDOW cleanup requests without " +
            "a WinUSB call; streams=2.";
        private const string WarmSeedMarker =
            "OFFLINE REPLAY seeded one bounded warm operator cycle with " +
            "exactly three remaining bounded initialization and " +
            "image-stream phases; no WinUSB call.";
        private const string ImageReadMarker =
            "length=4488, CDB=28 00 00 00 28 00 00 11 88 00";
        private const string SetWindowHashMarker =
            "Preview SET WINDOW candidate validated for transport; " +
            "payload-sha256=";

        internal static OfflineOperatorRepeatLogMetadata Inspect(
            string text, int expectedTransactions)
        {
            if (text == null)
            {
                throw new ArgumentNullException("text");
            }
            if (expectedTransactions < 2 || expectedTransactions > 8)
            {
                throw new ArgumentOutOfRangeException(
                    "expectedTransactions");
            }

            var rowCounts = new int[expectedTransactions];
            var hashes = new string[expectedTransactions];
            int offset = 0;
            for (int transaction = 0; transaction < expectedTransactions;
                ++transaction)
            {
                int firstSearchOffset = offset;
                if (transaction != 0)
                {
                    int warmSeed = RequireAfter(text, WarmSeedMarker, offset,
                        transaction + 1);
                    RejectMarkerBefore(text, FirstCompletionMarker, offset,
                        warmSeed, transaction + 1);
                    RejectMarkerBefore(text, RepeatMarker, offset, warmSeed,
                        transaction + 1);
                    RejectMarkerBefore(text, SecondCompletionMarker, offset,
                        warmSeed, transaction + 1);
                    int warmRepeat = RequireAfter(text, RepeatMarker,
                        warmSeed + WarmSeedMarker.Length, transaction + 1);
                    RejectMarkerBefore(text, FirstCompletionMarker,
                        warmSeed + WarmSeedMarker.Length, warmRepeat,
                        transaction + 1);
                    RejectMarkerBefore(text, SecondCompletionMarker,
                        warmSeed + WarmSeedMarker.Length, warmRepeat,
                        transaction + 1);
                    int warmFirst = RequireAfter(text, FirstCompletionMarker,
                        warmRepeat + RepeatMarker.Length, transaction + 1);
                    RejectDuplicateBefore(text, RepeatMarker,
                        warmRepeat + RepeatMarker.Length, warmFirst,
                        transaction + 1);
                    RejectMarkerBefore(text, SecondCompletionMarker,
                        warmRepeat + RepeatMarker.Length, warmFirst,
                        transaction + 1);
                    string warmPreview = text.Substring(warmRepeat,
                        warmFirst + FirstCompletionMarker.Length -
                        warmRepeat);
                    if (warmPreview.IndexOf(
                            "cycles=4, next=PreviewSetWindowReady",
                            StringComparison.Ordinal) < 0 ||
                        warmPreview.IndexOf(
                            "cycles=5, next=PreviewSetWindowReady",
                            StringComparison.Ordinal) < 0 ||
                        HasFailure(warmPreview))
                    {
                        throw new InvalidOperationException(string.Format(
                            "Offline operator transaction {0} did not " +
                            "complete its exact warm Preview prelude.",
                            transaction + 1));
                    }
                    firstSearchOffset = warmRepeat + RepeatMarker.Length;
                }

                int first = RequireAfter(text, FirstCompletionMarker,
                    firstSearchOffset,
                    transaction + 1);
                if (transaction == 0)
                {
                    RejectMarkerBefore(text, RepeatMarker, offset, first,
                        transaction + 1);
                    RejectMarkerBefore(text, SecondCompletionMarker, offset,
                        first, transaction + 1);
                }
                int repeat = RequireAfter(text, RepeatMarker,
                    first + FirstCompletionMarker.Length, transaction + 1);
                RejectMarkerBefore(text, SecondCompletionMarker,
                    first + FirstCompletionMarker.Length, repeat,
                    transaction + 1);
                int second = RequireAfter(text, SecondCompletionMarker,
                    repeat + RepeatMarker.Length, transaction + 1);
                RejectDuplicateBefore(text, FirstCompletionMarker,
                    first + FirstCompletionMarker.Length, second,
                    transaction + 1);
                RejectDuplicateBefore(text, RepeatMarker,
                    repeat + RepeatMarker.Length, second, transaction + 1);

                string segment = text.Substring(repeat,
                    second + SecondCompletionMarker.Length - repeat);
                if (segment.IndexOf(
                        "cycles=6, next=PreviewSetWindowReady",
                        StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException(string.Format(
                        "Offline operator transaction {0} did not complete " +
                        "its sixth bounded initialization cycle.",
                        transaction + 1));
                }
                if (HasFailure(segment))
                {
                    throw new InvalidOperationException(string.Format(
                        "Offline operator transaction {0} contains a " +
                        "failure warning.", transaction + 1));
                }

                rowCounts[transaction] = Count(segment, ImageReadMarker);
                if (rowCounts[transaction] != 996)
                {
                    throw new InvalidOperationException(string.Format(
                        "Offline operator transaction {0} returned {1} " +
                        "rows instead of the exact observed 996.",
                        transaction + 1, rowCounts[transaction]));
                }
                hashes[transaction] = ExtractHash(segment, transaction + 1);
                offset = second + SecondCompletionMarker.Length;
            }

            if (text.IndexOf(FirstCompletionMarker, offset,
                    StringComparison.Ordinal) >= 0 ||
                text.IndexOf(RepeatMarker, offset,
                    StringComparison.Ordinal) >= 0 ||
                text.IndexOf(SecondCompletionMarker, offset,
                    StringComparison.Ordinal) >= 0 ||
                text.IndexOf(WarmSeedMarker, offset,
                    StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException(
                    "Offline operator log contains an unexpected extra " +
                    "transaction marker.");
            }
            return new OfflineOperatorRepeatLogMetadata(rowCounts, hashes);
        }

        private static int RequireAfter(string text, string marker,
            int offset, int transaction)
        {
            int result = text.IndexOf(marker, offset,
                StringComparison.Ordinal);
            if (result < 0)
            {
                throw new InvalidOperationException(string.Format(
                    "Offline operator transaction {0} is missing marker: " +
                    "{1}", transaction, marker));
            }
            return result;
        }

        private static void RejectDuplicateBefore(string text, string marker,
            int offset, int boundary, int transaction)
        {
            int duplicate = text.IndexOf(marker, offset,
                StringComparison.Ordinal);
            if (duplicate >= 0 && duplicate < boundary)
            {
                throw new InvalidOperationException(string.Format(
                    "Offline operator transaction {0} contains a duplicate " +
                    "marker: {1}", transaction, marker));
            }
        }

        private static void RejectMarkerBefore(string text, string marker,
            int offset, int boundary, int transaction)
        {
            int unexpected = text.IndexOf(marker, offset,
                StringComparison.Ordinal);
            if (unexpected >= 0 && unexpected < boundary)
            {
                throw new InvalidOperationException(string.Format(
                    "Offline operator transaction {0} has an out-of-order " +
                    "marker: {1}", transaction, marker));
            }
        }

        private static string ExtractHash(string segment, int transaction)
        {
            int offset = segment.IndexOf(SetWindowHashMarker,
                StringComparison.Ordinal);
            if (offset < 0)
            {
                throw new InvalidOperationException(string.Format(
                    "Offline operator transaction {0} has no SET WINDOW " +
                    "fingerprint.", transaction));
            }
            offset += SetWindowHashMarker.Length;
            if (segment.Length - offset < 64)
            {
                throw new InvalidOperationException(string.Format(
                    "Offline operator transaction {0} has a truncated " +
                    "SET WINDOW fingerprint.", transaction));
            }
            string hash = segment.Substring(offset, 64).ToUpperInvariant();
            for (int index = 0; index < hash.Length; ++index)
            {
                char value = hash[index];
                if (!((value >= '0' && value <= '9') ||
                      (value >= 'A' && value <= 'F')))
                {
                    throw new InvalidOperationException(string.Format(
                        "Offline operator transaction {0} has an invalid " +
                        "SET WINDOW fingerprint.", transaction));
                }
            }
            return hash;
        }

        private static int Count(string text, string marker)
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

        private static bool HasFailure(string text)
        {
            return text.IndexOf(" WARN ", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("failed [", StringComparison.Ordinal) >= 0;
        }
    }

    internal sealed class OfflineOperatorRepeatLogMetadata
    {
        internal OfflineOperatorRepeatLogMetadata(int[] rowCounts,
            string[] setWindowSha256)
        {
            RowCounts = rowCounts;
            SetWindowSha256 = setWindowSha256;
        }

        internal int[] RowCounts { get; private set; }
        internal string[] SetWindowSha256 { get; private set; }
    }
}
