// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Globalization;

namespace Usb2Xchange.FlexColorAspiLauncher
{
    internal static class PreviewReplayInspector
    {
        internal static PreviewReplayMetadata Inspect(string text)
        {
            const string windowMarker =
                "OFFLINE REPLAY simulated structurally valid Preview SET " +
                "WINDOW success without a WinUSB call: payload-sha256=";
            string setWindowSha256 = FindSha256After(text, windowMarker);
            if (setWindowSha256 == null)
            {
                throw new InvalidOperationException(
                    "The initial offline SET WINDOW fingerprint is missing.");
            }

            const string requestMarker =
                "OFFLINE REPLAY request: target=5, lun=0, length=";
            int rowCount = 0;
            uint rowBytes = 0;
            string imageReadCdb = null;
            string[] lines = text.Split(new char[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                int requestOffset = line.IndexOf(requestMarker,
                    StringComparison.Ordinal);
                if (requestOffset < 0)
                {
                    continue;
                }
                int lengthOffset = requestOffset + requestMarker.Length;
                int cdbOffset = line.IndexOf(", CDB=", lengthOffset,
                    StringComparison.Ordinal);
                if (cdbOffset < 0)
                {
                    continue;
                }
                int cdbStart = cdbOffset + 6;
                int cdbEnd = line.IndexOf(';', cdbStart);
                if (cdbEnd < 0)
                {
                    cdbEnd = line.Length;
                }
                string cdb = line.Substring(cdbStart,
                    cdbEnd - cdbStart).Trim();
                if (!cdb.StartsWith("28 ", StringComparison.Ordinal))
                {
                    continue;
                }

                uint length;
                if (!uint.TryParse(line.Substring(lengthOffset,
                        cdbOffset - lengthOffset), NumberStyles.None,
                        CultureInfo.InvariantCulture, out length) ||
                    length == 0 || length % 6 != 0)
                {
                    throw new InvalidOperationException(
                        "The offline image READ length is invalid.");
                }
                if (rowCount == 0)
                {
                    rowBytes = length;
                    imageReadCdb = cdb;
                }
                else if (rowBytes != length || imageReadCdb != cdb)
                {
                    throw new InvalidOperationException(
                        "The offline Preview changed image READ geometry.");
                }
                ++rowCount;
            }
            if (rowCount == 0)
            {
                throw new InvalidOperationException(
                    "No offline Preview image READ rows were observed.");
            }
            return new PreviewReplayMetadata(setWindowSha256, imageReadCdb,
                rowBytes / 6, rowBytes, rowCount);
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

    internal sealed class PreviewReplayMetadata
    {
        internal PreviewReplayMetadata(string setWindowSha256,
            string imageReadCdb, uint scanWidth, uint rowBytes, int rowCount)
        {
            SetWindowSha256 = setWindowSha256;
            ImageReadCdb = imageReadCdb;
            ScanWidth = scanWidth;
            RowBytes = rowBytes;
            RowCount = rowCount;
        }

        internal string SetWindowSha256 { get; private set; }
        internal string ImageReadCdb { get; private set; }
        internal uint ScanWidth { get; private set; }
        internal uint RowBytes { get; private set; }
        internal int RowCount { get; private set; }
    }
}
