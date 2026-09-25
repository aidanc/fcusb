// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Security.Cryptography;

namespace Usb2Xchange.Protocol
{
    public sealed class PrecisionTwoLoaderRecordManifest
    {
        public const int FirmwareLength = 65536;
        public const int RecordCount = 16;
        public const int RecordHeaderLength = 10;
        public const int RecordPayloadLength = 4096;
        public const int RecordLength = RecordHeaderLength + RecordPayloadLength;
        public const int TerminalLength = 10;

        public const string KnownFirmwareSha256 =
            "D8D7188574C52B255CF7940BEF7CC9192F744693AABC1F735758B7FECDEC65F3";

        private static readonly string[] KnownRecordSha256 = new string[]
        {
            "851841B23FCA4D22E4C3C2DB966323BC9192AF3CECE430AAB5D7D8BFBF950729",
            "76B6D086666727C0E6DBD9AAE67134FCBBBE24DA0932705E68A2F1D5A4271A46",
            "18CF662935349330B01E73F0B23672C0A969A38EA391F3D7556280734EE781B9",
            "6AD16D088197C7C84C07C7F69DE977794EF35FC936F83350AEA08D17F52B45BD",
            "C191E5F7E02825163C1DF8FD57F20AC76B575B8C272B216C47EE7BD46FC23B28",
            "F071559D4DB49591F8B366A2A308EDE12875EF311E1CB9C2C6C4D5D5E0FDA42C",
            "3CDCFC4F5A6150E2A4428C0F2EB587AA199896CA144D12B225A8C9A695D3C7FC",
            "06233C39AA16FF112B879AD52FB7D93A0CA8AE97EA870D802952C2E57719355E",
            "52DAA6702DABE141F6F0E81A7F6AC31954148FA026CD69E4BD78FEF259BD5537",
            "2EAD7C79A4B409019BC0371B4AAA0E1638A3EBD40E1BAE45B403EA4359A0E29B",
            "91A66555F012FD389737061A477F3731579A3B037507F1E402779C6DFA18E23F",
            "EA7EB8845C6B86E49DDD445E08FC19CCAF6DE77DFF792BDC3AB7F99B895594DC",
            "C29A2A91BD553304F90DCB5559FC158855E853218100882C53D4A1C3AF4232E4",
            "DEB4D4A46C9CEA07C45D4B253B8A2F20B465CC1A71903F0721542336F7AC1FE6",
            "FEFA54D1C15F7313703ADC1433ABBB5A356794662CAC1209868F9F161408558A",
            "5B30D224B9784DD96499A72F277D3954B6A863EDBEB433F103F2758EECC6E2E6"
        };

        private const string KnownTerminalSha256 =
            "7C3D21A44BEBF711C21E354134850F317A3CC9F65922176F5E014B97C0E7094E";

        private readonly string firmwareSha256;
        private readonly string[] recordSha256;
        private readonly string terminalSha256;

        private PrecisionTwoLoaderRecordManifest(string firmwareSha256,
            string[] recordSha256, string terminalSha256)
        {
            this.firmwareSha256 = firmwareSha256;
            this.recordSha256 = (string[])recordSha256.Clone();
            this.terminalSha256 = terminalSha256;
        }

        public string FirmwareSha256
        {
            get { return firmwareSha256; }
        }

        public string TerminalSha256
        {
            get { return terminalSha256; }
        }

        public static PrecisionTwoLoaderRecordManifest CreateKnown()
        {
            return new PrecisionTwoLoaderRecordManifest(KnownFirmwareSha256,
                KnownRecordSha256, KnownTerminalSha256);
        }

        public static PrecisionTwoLoaderRecordManifest Create(
            byte[] firmware, string expectedFirmwareSha256)
        {
            if (firmware == null)
            {
                throw new ArgumentNullException("firmware");
            }
            if (firmware.Length != FirmwareLength)
            {
                throw new ProtocolException(string.Format(
                    "Precision II firmware must contain exactly {0} bytes.",
                    FirmwareLength));
            }
            string actualFirmwareSha256 = Sha256Hex(firmware);
            if (!FixedTimeHexEquals(actualFirmwareSha256,
                    expectedFirmwareSha256))
            {
                throw new ProtocolException(string.Format(
                    "Precision II firmware SHA-256 {0} does not match {1}.",
                    actualFirmwareSha256, expectedFirmwareSha256));
            }

            string[] hashes = new string[RecordCount];
            for (int index = 0; index < RecordCount; ++index)
            {
                byte[] record = BuildRecord(firmware, index);
                try
                {
                    hashes[index] = Sha256Hex(record);
                }
                finally
                {
                    Array.Clear(record, 0, record.Length);
                }
            }
            byte[] terminal = BuildTerminal();
            return new PrecisionTwoLoaderRecordManifest(
                actualFirmwareSha256, hashes, Sha256Hex(terminal));
        }

        public string GetRecordSha256(int recordIndex)
        {
            RequireRecordIndex(recordIndex);
            return recordSha256[recordIndex];
        }

        public void ValidateRecord(int recordIndex, byte[] cdb, byte[] data)
        {
            RequireRecordIndex(recordIndex);
            ushort address = checked((ushort)(0x3000 + recordIndex * 0x100));
            if (data == null || data.Length != RecordLength ||
                !HasExactWriteBufferCdb(cdb, RecordLength) ||
                data[0] != 0x01 || data[1] != (byte)(address >> 8) ||
                data[2] != (byte)(address & 0xFF) || data[3] != 0 ||
                data[4] != 0 || data[5] != 0 || data[6] != 0 ||
                data[7] != 0 || data[8] != 0 || data[9] != 0 ||
                !FixedTimeHexEquals(Sha256Hex(data),
                    recordSha256[recordIndex]))
            {
                throw new ProtocolException(string.Format(
                    "Loader record {0} does not match the exact CDB, length, " +
                    "header, address, and SHA-256 manifest.", recordIndex));
            }
        }

        public void ValidateTerminal(byte[] cdb, byte[] data)
        {
            byte[] expected = BuildTerminal();
            if (data == null || data.Length != TerminalLength ||
                !HasExactWriteBufferCdb(cdb, TerminalLength) ||
                !FixedTimeEquals(data, expected) ||
                !FixedTimeHexEquals(Sha256Hex(data), terminalSha256))
            {
                throw new ProtocolException(
                    "Loader terminal does not match the exact CDB, bytes, and " +
                    "SHA-256 manifest.");
            }
        }

        public static byte[] BuildRecord(byte[] firmware, int recordIndex)
        {
            if (firmware == null)
            {
                throw new ArgumentNullException("firmware");
            }
            if (firmware.Length != FirmwareLength)
            {
                throw new ArgumentException(
                    "The firmware fixture must contain exactly 65,536 bytes.",
                    "firmware");
            }
            RequireRecordIndex(recordIndex);
            ushort address = checked((ushort)(0x3000 + recordIndex * 0x100));
            byte[] record = new byte[RecordLength];
            record[0] = 0x01;
            record[1] = (byte)(address >> 8);
            record[2] = (byte)(address & 0xFF);
            Buffer.BlockCopy(firmware, recordIndex * RecordPayloadLength,
                record, RecordHeaderLength, RecordPayloadLength);
            return record;
        }

        public static byte[] BuildTerminal()
        {
            return new byte[]
            {
                0x00, 0x30, 0x01, 0x00, 0x0E,
                0x00, 0x00, 0x00, 0x00, 0x00
            };
        }

        private static bool HasExactWriteBufferCdb(byte[] cdb, int length)
        {
            return cdb != null && cdb.Length == 10 && cdb[0] == 0x3B &&
                cdb[1] == 0x01 && cdb[2] == 0 && cdb[3] == 0 &&
                cdb[4] == 0 && cdb[5] == 0 &&
                cdb[6] == (byte)((length >> 16) & 0xFF) &&
                cdb[7] == (byte)((length >> 8) & 0xFF) &&
                cdb[8] == (byte)(length & 0xFF) && cdb[9] == 0;
        }

        private static void RequireRecordIndex(int recordIndex)
        {
            if (recordIndex < 0 || recordIndex >= RecordCount)
            {
                throw new ArgumentOutOfRangeException("recordIndex");
            }
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).
                    Replace("-", string.Empty);
            }
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

        private static bool FixedTimeEquals(byte[] left, byte[] right)
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
