// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Usb2Xchange.FlexColorAspiLauncher
{
    internal static class FullScanTerminalProbeLogInspector
    {
        internal const string Marker =
            "LIVE ASPI exact full-scan terminal probe observed and " +
            "quarantined:";
        private static readonly Regex Pattern = new Regex(
            Marker + " row=([0-9]+), status=0x([0-9A-F]{2}), " +
            "requested=([0-9]+), actual=([0-9]+), residue=([0-9]+), " +
            "width=([0-9]+), selector=0x([0-9A-F]{2}), " +
            "submission=([0-9]+), CDB=" +
            "([0-9A-F]{2}(?: [0-9A-F]{2}){9}), " +
            "payload-sha256=([0-9A-F]{64})[.] No probe payload bytes were " +
            "copied to FlexColor; every successor is blocked before USB[.]",
            RegexOptions.CultureInvariant);

        internal static FullScanTerminalProbeLogMetadata Inspect(string text)
        {
            text = text ?? string.Empty;
            MatchCollection matches = Pattern.Matches(text);
            if (matches.Count != 1 || CountOccurrences(text, Marker) != 1)
            {
                return null;
            }
            Match match = matches[0];
            int requested = Parse(match, 3);
            int actual = Parse(match, 4);
            int residue = Parse(match, 5);
            return new FullScanTerminalProbeLogMetadata(
                Parse(match, 1), Convert.ToInt32(match.Groups[2].Value, 16),
                requested, actual, residue, Parse(match, 6),
                Convert.ToInt32(match.Groups[7].Value, 16), Parse(match, 8),
                match.Groups[9].Value, match.Groups[10].Value,
                requested >= 0 && actual >= 0 && residue >= 0 &&
                    actual <= requested && residue <= requested &&
                    actual == requested - residue);
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
            return int.Parse(match.Groups[group].Value,
                CultureInfo.InvariantCulture);
        }
    }

    internal sealed class FullScanTerminalProbeLogMetadata
    {
        internal FullScanTerminalProbeLogMetadata(int row, int status,
            int requested, int actual, int residue, int width, int selector,
            int submission, string cdb, string payloadSha256,
            bool lengthConsistent)
        {
            Row = row;
            Status = status;
            Requested = requested;
            Actual = actual;
            Residue = residue;
            Width = width;
            Selector = selector;
            Submission = submission;
            Cdb = cdb;
            PayloadSha256 = payloadSha256;
            LengthConsistent = lengthConsistent;
        }

        internal int Row { get; private set; }
        internal int Status { get; private set; }
        internal int Requested { get; private set; }
        internal int Actual { get; private set; }
        internal int Residue { get; private set; }
        internal int Width { get; private set; }
        internal int Selector { get; private set; }
        internal int Submission { get; private set; }
        internal string Cdb { get; private set; }
        internal string PayloadSha256 { get; private set; }
        internal bool LengthConsistent { get; private set; }
    }
}
