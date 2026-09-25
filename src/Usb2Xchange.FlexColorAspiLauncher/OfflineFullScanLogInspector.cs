// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;

namespace Usb2Xchange.FlexColorAspiLauncher
{
    internal static class OfflineFullScanLogInspector
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
        private const string ImageReadMarker =
            "length=4488, CDB=28 00 00 00 28 00 00 11 88 00";
        private const string SetWindowHashMarker =
            "Preview SET WINDOW candidate validated for transport; " +
            "payload-sha256=";

        internal static OfflineFullScanLogMetadata Inspect(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException("text");
            }
            int repeat = RequireSingle(text, RepeatMarker);
            int firstCompletion = RequireSingle(text, FirstCompletionMarker);
            int secondCompletion = RequireSingle(text, SecondCompletionMarker);
            if (firstCompletion >= repeat || repeat >= secondCompletion)
            {
                throw new InvalidOperationException(
                    "Offline full-scan log markers are out of order.");
            }

            string second = text.Substring(repeat,
                secondCompletion + SecondCompletionMarker.Length - repeat);
            if (second.IndexOf(
                    "cycles=6, next=PreviewSetWindowReady",
                    StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException(
                    "Offline full scan did not complete its sixth bounded " +
                    "initialization cycle.");
            }
            if (second.IndexOf(" WARN ", StringComparison.Ordinal) >= 0 ||
                second.IndexOf("failed [", StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException(
                    "Offline full-scan segment contains a failure warning.");
            }

            int rows = Count(second, ImageReadMarker);
            if (rows != 996)
            {
                throw new InvalidOperationException(string.Format(
                    "Offline 60x60 full scan returned {0} rows instead of " +
                    "the exact observed 996.", rows));
            }

            int hashOffset = second.IndexOf(SetWindowHashMarker,
                StringComparison.Ordinal);
            if (hashOffset < 0)
            {
                throw new InvalidOperationException(
                    "Offline full-scan SET WINDOW fingerprint is missing.");
            }
            hashOffset += SetWindowHashMarker.Length;
            if (second.Length - hashOffset < 64)
            {
                throw new InvalidOperationException(
                    "Offline full-scan SET WINDOW fingerprint is truncated.");
            }
            string hash = second.Substring(hashOffset, 64).ToUpperInvariant();
            for (int index = 0; index < hash.Length; ++index)
            {
                char value = hash[index];
                if (!((value >= '0' && value <= '9') ||
                      (value >= 'A' && value <= 'F')))
                {
                    throw new InvalidOperationException(
                        "Offline full-scan SET WINDOW fingerprint is invalid.");
                }
            }
            return new OfflineFullScanLogMetadata(rows, hash);
        }

        private static int RequireSingle(string text, string marker)
        {
            int first = text.IndexOf(marker, StringComparison.Ordinal);
            if (first < 0 || text.IndexOf(marker, first + marker.Length,
                    StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException(
                    "Offline full-scan log requires exactly one marker: " +
                    marker);
            }
            return first;
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

    internal sealed class OfflineFullScanLogMetadata
    {
        internal OfflineFullScanLogMetadata(int rowCount,
            string setWindowSha256)
        {
            RowCount = rowCount;
            SetWindowSha256 = setWindowSha256;
        }

        internal int RowCount { get; private set; }
        internal string SetWindowSha256 { get; private set; }
    }
}
