// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Text.RegularExpressions;

namespace Usb2Xchange.FlexColorAspiLauncher
{
    internal static class LiveOperatorRepeatLogInspector
    {
        private const string PreviewCleanupOne =
            "LIVE ASPI exact post-image cleanup SET WINDOW completed: " +
            "ordinal=1/2, status=0x00, actual=84, residue=0, " +
            "payload-sha256=" +
            "3092FA15361A422D558EF968164EE6CB00B854413630CC0C07A4F62877515A9D.";
        private const string PreviewCleanupTwo =
            "LIVE ASPI exact post-image cleanup SET WINDOW completed: " +
            "ordinal=2/2, status=0x00, actual=84, residue=0, " +
            "payload-sha256=" +
            "3092FA15361A422D558EF968164EE6CB00B854413630CC0C07A4F62877515A9D.";
        private const string FullScanCleanupOne =
            "LIVE ASPI exact full-scan cleanup SET WINDOW completed: " +
            "ordinal=1/2, status=0x00, actual=84, residue=0, " +
            "payload-sha256=" +
            "23B3C62B62096A50A58E6BC035D20C6C04E38F5D10A30BF27C6348D0C37EFB15.";
        private const string FullScanCleanupTwo =
            "LIVE ASPI exact full-scan cleanup SET WINDOW completed: " +
            "ordinal=2/2, status=0x00, actual=84, residue=0, " +
            "payload-sha256=" +
            "23B3C62B62096A50A58E6BC035D20C6C04E38F5D10A30BF27C6348D0C37EFB15.";
        private const string FullScanCompletion =
            "LIVE ASPI exact full-scan transport completed after 998 rows " +
            "and two cleanup SET WINDOW completions.";
        private const string WarmSeed =
            "LIVE ASPI warm operator cycle seeded;";
        private const string WarmInquiry =
            "LIVE ASPI warm operator exact operational INQUIRY prefix " +
            "completed;";
        private const string WarmPreview =
            "LIVE ASPI warm operator Preview initialization armed after " +
            "exact M333 INQUIRY;";
        private const string WarmStabilizationOne =
            "LIVE ASPI warm operator stabilization M333 INQUIRY " +
            "completed: ordinal=1/2, counter=3, status=0x00, actual=96, " +
            "residue=0.";
        private const string WarmStabilizationTwo =
            "LIVE ASPI warm operator stabilization M333 INQUIRY " +
            "completed: ordinal=2/2, counter=4, status=0x00, actual=96, " +
            "residue=0.";
        private static readonly Regex FullScanStream = new Regex(
            "LIVE ASPI exact bounded full-scan image stream completed: " +
            "rows=998/998, submissions=([0-9]+)/8982, " +
            "short-retries=([0-9]+)/7984\\.",
            RegexOptions.CultureInvariant);

        internal static LiveOperatorRepeatLogMetadata Inspect(string text,
            int expectedTransactions)
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
            RequireCount(text, PreviewCleanupOne, expectedTransactions);
            RequireCount(text, PreviewCleanupTwo, expectedTransactions);
            RequireCount(text, FullScanCleanupOne, expectedTransactions);
            RequireCount(text, FullScanCleanupTwo, expectedTransactions);
            RequireCount(text, FullScanCompletion, expectedTransactions);
            RequireCount(text, WarmSeed, expectedTransactions - 1);
            RequireCount(text, WarmInquiry, expectedTransactions - 1);
            RequireCount(text, WarmPreview, expectedTransactions - 1);
            RequireCount(text, WarmStabilizationOne,
                expectedTransactions - 1);
            RequireCount(text, WarmStabilizationTwo,
                expectedTransactions - 1);
            if (FullScanStream.Matches(text).Count != expectedTransactions)
            {
                throw new InvalidOperationException(
                    "Live operator log does not contain the exact number " +
                    "of bounded 998-row full-scan stream completions.");
            }
            if (text.IndexOf(
                    "LIVE ASPI bounded Preview stream failed closed:",
                    StringComparison.Ordinal) >= 0 ||
                text.IndexOf(
                    "LIVE ASPI bounded full-scan image stream failed closed:",
                    StringComparison.Ordinal) >= 0 ||
                text.IndexOf("EXEC_SCSI_CMD failed [",
                    StringComparison.Ordinal) >= 0 ||
                text.IndexOf("Unhandled ASPI command failure:",
                    StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException(
                    "Live operator log contains a terminal failure marker.");
            }

            int offset = 0;
            for (int index = 0; index < expectedTransactions; ++index)
            {
                if (index != 0)
                {
                    int warmSeed = RequireAfter(text, WarmSeed, offset);
                    int warmInquiry = RequireAfter(text, WarmInquiry,
                        warmSeed + WarmSeed.Length);
                    int warmPreview = RequireAfter(text, WarmPreview,
                        warmInquiry + WarmInquiry.Length);
                    int warmStabilizationOne = RequireAfter(text,
                        WarmStabilizationOne,
                        warmPreview + WarmPreview.Length);
                    int warmStabilizationTwo = RequireAfter(text,
                        WarmStabilizationTwo,
                        warmStabilizationOne +
                            WarmStabilizationOne.Length);
                    offset = warmStabilizationTwo +
                        WarmStabilizationTwo.Length;
                }

                int previewOne = RequireAfter(text, PreviewCleanupOne,
                    offset);
                int previewTwo = RequireAfter(text, PreviewCleanupTwo,
                    previewOne + PreviewCleanupOne.Length);
                Match stream = FullScanStream.Match(text,
                    previewTwo + PreviewCleanupTwo.Length);
                if (!stream.Success)
                {
                    throw new InvalidOperationException(
                        "Live operator transaction is missing its ordered " +
                        "bounded full-scan completion.");
                }
                int submissions = Convert.ToInt32(
                    stream.Groups[1].Value);
                int shortRetries = Convert.ToInt32(
                    stream.Groups[2].Value);
                if (shortRetries < 0 || shortRetries > 7984 ||
                    submissions != 998 + shortRetries ||
                    submissions > 8982)
                {
                    throw new InvalidOperationException(
                        "Live operator full-scan submission accounting " +
                        "changed or exceeded its exact bound.");
                }
                int fullOne = RequireAfter(text, FullScanCleanupOne,
                    stream.Index + stream.Length);
                int fullTwo = RequireAfter(text, FullScanCleanupTwo,
                    fullOne + FullScanCleanupOne.Length);
                int completion = RequireAfter(text, FullScanCompletion,
                    fullTwo + FullScanCleanupTwo.Length);
                offset = completion + FullScanCompletion.Length;
            }

            var rows = new int[expectedTransactions];
            for (int index = 0; index < rows.Length; ++index)
            {
                rows[index] = 998;
            }
            return new LiveOperatorRepeatLogMetadata(rows);
        }

        private static void RequireCount(string text, string marker,
            int expected)
        {
            int actual = Count(text, marker);
            if (actual != expected)
            {
                throw new InvalidOperationException(string.Format(
                    "Live operator marker count changed: expected={0}, " +
                    "actual={1}, marker={2}", expected, actual, marker));
            }
        }

        private static int RequireAfter(string text, string marker,
            int offset)
        {
            int result = text.IndexOf(marker, offset,
                StringComparison.Ordinal);
            if (result < 0)
            {
                throw new InvalidOperationException(
                    "Live operator log is missing an ordered marker: " +
                    marker);
            }
            return result;
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
    }

    internal sealed class LiveOperatorRepeatLogMetadata
    {
        internal LiveOperatorRepeatLogMetadata(int[] rowCounts)
        {
            RowCounts = rowCounts;
        }

        internal int[] RowCounts { get; private set; }
    }
}
