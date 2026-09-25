// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Security.Cryptography;

namespace Usb2Xchange.Protocol
{
    public static class PrecisionTwoPreviewCommandManifest
    {
        public const string SetWindowSha256 =
            "F45F2A91958286CF750AB56588E2C20A83E95E9B5973C046B6068C6C2FEC0EF6";
        public const string AspiLiveSetWindowSha256 =
            "96EB9049828909D06A5E8AB32861124D1FE59B67005D3BF0EBDA235584D0C6FB";
        public const string AspiLive24x36SetWindowSha256 =
            "78D93BC3FBA82976A49AA369301DFB065DD392CA381EB56CC504E79A643DAC63";
        public const string AspiLive4x5SetWindowSha256 =
            "A44969F96CACE039220A55E5FCC7F8EFB4EE054526039D806B9391C4D9097DAD";
        public const string AspiLiveCleanupSetWindowSha256 =
            "3092FA15361A422D558EF968164EE6CB00B854413630CC0C07A4F62877515A9D";
        public const string AspiLiveFullScanSetWindowSha256 =
            "210F3499FB1BE7F689F44A26C68D8D079E6FB01E17AE19B320B5011E6F8A9EDA";
        public const string AspiLiveFullScanCleanupSetWindowSha256 =
            "23B3C62B62096A50A58E6BC035D20C6C04E38F5D10A30BF27C6348D0C37EFB15";
        public const int AspiLivePreviewCleanupSetWindowCount = 2;
        public const uint AspiLiveFirstImageWidth = 666;
        public const uint AspiLiveFirstImageLength =
            AspiLiveFirstImageWidth *
                ScsiFraming.PrecisionTwoPreviewBytesPerPixel;
        public const int AspiLivePreviewBurstRows = 8;
        public const int AspiLivePreviewStreamRows = 256;
        public const int AspiLivePreviewStreamMaximumScannerReadyPolls = 256;
        public const int AspiLivePreviewObservedShortLength = 10;
        public const int AspiLivePreviewStreamMaximumShortRetries = 64;
        public const int
            AspiLivePreviewStreamMaximumConsecutiveShortRetries = 8;
        public const int AspiLivePreviewStreamMaximumReadSubmissions =
            AspiLivePreviewStreamRows +
                AspiLivePreviewStreamMaximumShortRetries;
        public const int AspiLivePreviewNaturalRows = 996;
        public const int AspiLivePreviewNaturalMaximumScannerReadyPolls =
            1024;
        public const int AspiLivePreviewNaturalMaximumShortRetries = 512;
        public const int
            AspiLivePreviewNaturalMaximumConsecutiveShortRetries = 8;
        public const int AspiLivePreviewNaturalMaximumReadSubmissions =
            AspiLivePreviewNaturalRows +
                AspiLivePreviewNaturalMaximumShortRetries;
        public const int
            AspiLivePreviewNaturalPerRowMaximumShortRetries =
                AspiLivePreviewNaturalRows *
                    AspiLivePreviewNaturalMaximumConsecutiveShortRetries;
        public const int
            AspiLivePreviewNaturalPerRowMaximumReadSubmissions =
                AspiLivePreviewNaturalRows +
                    AspiLivePreviewNaturalPerRowMaximumShortRetries;
        public const int AspiLivePreviewPoweredNaturalRows = 911;
        public const int
            AspiLivePreviewPoweredMaximumConsecutiveShortRetries = 32;
        public const int AspiLiveImageShortRetryBackoffMilliseconds = 50;
        public const int AspiLivePreviewCancellationTriggerRows = 32;
        public const int AspiLivePreviewCancellationMaximumRows = 96;
        public const int
            AspiLivePreviewPoweredNaturalMaximumShortRetries =
                AspiLivePreviewPoweredNaturalRows *
                    AspiLivePreviewNaturalMaximumConsecutiveShortRetries;
        public const int
            AspiLivePreviewPoweredNaturalMaximumReadSubmissions =
                AspiLivePreviewPoweredNaturalRows +
                    AspiLivePreviewPoweredNaturalMaximumShortRetries;
        public const long AspiLivePreviewNaturalMaximumMilliseconds = 300000;
        public const uint AspiLiveFullScanWidth = 749;
        public const uint AspiLiveFullScanLength =
            AspiLiveFullScanWidth *
                ScsiFraming.PrecisionTwoPreviewBytesPerPixel;
        public const int AspiLiveFullScanRows = 762;
        public const int AspiLiveFullScanMaximumConsecutiveShortRetries =
            AspiLivePreviewPoweredMaximumConsecutiveShortRetries;
        public const int AspiLiveFullScanMaximumShortRetries =
            AspiLiveFullScanRows *
                AspiLivePreviewNaturalMaximumConsecutiveShortRetries;
        public const int AspiLiveFullScanMaximumReadSubmissions =
            AspiLiveFullScanRows + AspiLiveFullScanMaximumShortRetries;
        public const int AspiLiveFullScanMaximumScannerReadyPolls = 1024;
        public const long AspiLiveFullScanMaximumMilliseconds = 1800000;
        public const int AspiLiveFullScanProcessingMarginRows = 234;
        public const int AspiLiveFullScanNaturalRows =
            AspiLiveFullScanRows + AspiLiveFullScanProcessingMarginRows;
        public const int AspiLiveFullScanNaturalMaximumShortRetries =
            AspiLiveFullScanNaturalRows *
                AspiLivePreviewNaturalMaximumConsecutiveShortRetries;
        public const int AspiLiveFullScanNaturalMaximumReadSubmissions =
            AspiLiveFullScanNaturalRows +
                AspiLiveFullScanNaturalMaximumShortRetries;
        public const int AspiLiveFullScanRow997CompletionRows =
            AspiLiveFullScanNaturalRows + 1;
        public const int
            AspiLiveFullScanRow997CompletionMaximumShortRetries =
                AspiLiveFullScanRow997CompletionRows *
                    AspiLivePreviewNaturalMaximumConsecutiveShortRetries;
        public const int
            AspiLiveFullScanRow997CompletionMaximumReadSubmissions =
                AspiLiveFullScanRow997CompletionRows +
                    AspiLiveFullScanRow997CompletionMaximumShortRetries;
        // Hardware completed request 998 and FlexColor then issued both
        // normal cleanup windows.  Progress UI was phase-local (18%), so the
        // command successor, not that percentage, establishes this boundary.
        public const int AspiLiveFullScanProgressMinimumCleanupRows =
            998;
        public const int AspiLiveFullScanProgressMaximumRows = 998;
        public const int AspiLiveFullScanProgressMaximumShortRetries =
            AspiLiveFullScanProgressMaximumRows *
                AspiLivePreviewNaturalMaximumConsecutiveShortRetries;
        public const int AspiLiveFullScanProgressMaximumReadSubmissions =
            AspiLiveFullScanProgressMaximumRows +
                AspiLiveFullScanProgressMaximumShortRetries;

        public static void ValidateSetWindow(byte[] cdb, byte[] data)
        {
            ValidateSetWindow(cdb, data, SetWindowSha256);
        }

        public static void ValidateSetWindow(byte[] cdb, byte[] data,
            string expectedSha256)
        {
            ValidateSetWindowEnvelope(cdb, data);
            if (expectedSha256 == null || expectedSha256.Length != 64 ||
                !FixedTimeHexEquals(FingerprintSetWindow(cdb, data),
                    expectedSha256))
            {
                throw new ProtocolException(
                    "Preview SET WINDOW does not match the captured CDB, " +
                    "length, header, and SHA-256.");
            }
        }

        public static string FingerprintSetWindow(byte[] cdb, byte[] data)
        {
            ValidateSetWindowEnvelope(cdb, data);
            return Sha256Hex(data);
        }

        public static void ValidateCleanupSetWindow(byte[] cdb, byte[] data,
            string expectedSha256)
        {
            ValidateSetWindowEnvelope(cdb, data);
            for (int index = 10; index < 30; ++index)
            {
                if (data[index] != 0)
                {
                    throw new ProtocolException(
                        "Preview cleanup SET WINDOW retains nonzero primary " +
                        "resolution or geometry fields.");
                }
            }
            if (expectedSha256 == null || expectedSha256.Length != 64 ||
                !FixedTimeHexEquals(FingerprintSetWindow(cdb, data),
                    expectedSha256))
            {
                throw new ProtocolException(
                    "Preview cleanup SET WINDOW does not match its exact " +
                    "84-byte payload SHA-256.");
            }
        }

        public static string DescribeSetWindow(byte[] cdb, byte[] data)
        {
            ValidateSetWindowEnvelope(cdb, data);
            return "regions-sha256=" +
                "00-09:" + Sha256Hex(data, 0, 10) + "," +
                "10-29:" + Sha256Hex(data, 10, 20) + "," +
                "30-47:" + Sha256Hex(data, 30, 18) + "," +
                "48-55:" + Sha256Hex(data, 48, 8) + "," +
                "56-71:" + Sha256Hex(data, 56, 16) + "," +
                "72-83:" + Sha256Hex(data, 72, 12) +
                "; u32be=" +
                "10:" + ReadUInt32BigEndian(data, 10).ToString("X8") + "," +
                "14:" + ReadUInt32BigEndian(data, 14).ToString("X8") + "," +
                "18:" + ReadUInt32BigEndian(data, 18).ToString("X8") + "," +
                "22:" + ReadUInt32BigEndian(data, 22).ToString("X8") + "," +
                "26:" + ReadUInt32BigEndian(data, 26).ToString("X8") + "," +
                "56:" + ReadUInt32BigEndian(data, 56).ToString("X8") + "," +
                "60:" + ReadUInt32BigEndian(data, 60).ToString("X8") + "," +
                "64:" + ReadUInt32BigEndian(data, 64).ToString("X8") +
                "; u16be=" +
                "70:" + ReadUInt16BigEndian(data, 70).ToString("X4") + "," +
                "82:" + ReadUInt16BigEndian(data, 82).ToString("X4");
        }

        public static void ValidateSetWindowEnvelope(byte[] cdb, byte[] data)
        {
            if (!Matches(cdb,
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb()) ||
                data == null || data.Length !=
                    ScsiFraming.PrecisionTwoPreviewSetWindowLength ||
                data[0] != 0 || data[1] != 0 || data[2] != 0 ||
                data[3] != 0 || data[4] != 0 || data[5] != 0 ||
                data[6] != 0 || data[7] != 0x4C)
            {
                throw new ProtocolException(
                    "SET WINDOW does not match the 84-byte Precision II " +
                    "CDB and descriptor envelope.");
            }
        }

        public static uint ValidatePredictedImageRead(byte[] cdb,
            uint requestedLength)
        {
            uint scanWidth = ValidateImageReadEnvelope(cdb,
                requestedLength);
            if (cdb[4] != 0x28)
            {
                throw new ProtocolException(
                    "Predicted Precision II Preview image READ does not use " +
                    "the observed selector 0x28.");
            }
            return scanWidth;
        }

        public static uint ValidateAspiLiveFirstImageRead(byte[] cdb,
            uint requestedLength)
        {
            uint scanWidth = ValidatePredictedImageRead(cdb,
                requestedLength);
            if (scanWidth != AspiLiveFirstImageWidth ||
                requestedLength != AspiLiveFirstImageLength)
            {
                throw new ProtocolException(
                    "Live ASPI first Preview image READ does not match the " +
                    "observed 666-pixel, 3,996-byte boundary.");
            }
            return scanWidth;
        }

        public static uint ValidateAspiLiveFullScanImageRead(byte[] cdb,
            uint requestedLength)
        {
            uint scanWidth = ValidatePredictedImageRead(cdb,
                requestedLength);
            if (scanWidth != AspiLiveFullScanWidth ||
                requestedLength != AspiLiveFullScanLength)
            {
                throw new ProtocolException(
                    "Live ASPI full-scan image READ does not match the " +
                    "observed 749-pixel, 4,494-byte boundary.");
            }
            return scanWidth;
        }

        public static uint ValidateOfflineImageRead(byte[] cdb,
            uint requestedLength)
        {
            uint scanWidth = ValidateImageReadEnvelope(cdb,
                requestedLength);
            if (cdb[4] != 0x28 && cdb[4] != 0x51)
            {
                throw new ProtocolException(
                    "Offline Preview image READ selector is neither the " +
                    "observed 0x28 nor statically predicted 0x51.");
            }
            return scanWidth;
        }

        private static uint ValidateImageReadEnvelope(byte[] cdb,
            uint requestedLength)
        {
            if (cdb == null || cdb.Length != 10 || cdb[0] != 0x28 ||
                cdb[1] != 0 || cdb[2] != 0 || cdb[3] != 0 ||
                cdb[5] != 0 || cdb[9] != 0 ||
                requestedLength == 0 ||
                requestedLength %
                    ScsiFraming.PrecisionTwoPreviewBytesPerPixel != 0)
            {
                throw new ProtocolException(
                    "Preview image READ does not match the target-5 CDB " +
                    "envelope and six-byte pixel stride.");
            }

            uint encodedLength = ((uint)cdb[6] << 16) |
                ((uint)cdb[7] << 8) | cdb[8];
            if (encodedLength != requestedLength)
            {
                throw new ProtocolException(
                    "Predicted Preview image READ CDB length does not match " +
                    "the requested transfer length.");
            }
            return requestedLength /
                ScsiFraming.PrecisionTwoPreviewBytesPerPixel;
        }

        private static bool Matches(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }
            int difference = 0;
            for (int index = 0; index < left.Length; ++index)
            {
                difference |= left[index] ^ right[index];
            }
            return difference == 0;
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(bytes)).
                    Replace("-", string.Empty);
            }
        }

        private static string Sha256Hex(byte[] bytes, int offset, int count)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(
                    sha256.ComputeHash(bytes, offset, count)).
                    Replace("-", string.Empty);
            }
        }

        private static uint ReadUInt32BigEndian(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) |
                ((uint)data[offset + 1] << 16) |
                ((uint)data[offset + 2] << 8) |
                data[offset + 3];
        }

        private static ushort ReadUInt16BigEndian(byte[] data, int offset)
        {
            return (ushort)(((uint)data[offset] << 8) | data[offset + 1]);
        }

        private static bool FixedTimeHexEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }
            int difference = 0;
            for (int index = 0; index < left.Length; ++index)
            {
                difference |= left[index] ^ right[index];
            }
            return difference == 0;
        }
    }
}
