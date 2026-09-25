// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Usb2Xchange.Protocol
{
    public sealed class FirmwareRecord
    {
        private readonly byte[] data;

        public FirmwareRecord(uint address, byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            Address = address;
            this.data = (byte[])data.Clone();
        }

        public uint Address { get; private set; }
        public int DataLength { get { return data.Length; } }
        public byte[] Data { get { return (byte[])data.Clone(); } }
    }

    public sealed class FirmwareImage
    {
        private readonly byte[] rawBytes;

        public const int ExternalRecordSize = 28;
        public const int MaximumRecordDataLength = 16;
        public const int KnownUsb2XchangeFileLength = 16492;
        public const string KnownUsb2XchangeSha256 =
            "d0967ef81e71e9293d0499c91d687e2409f8ca13b14ffe2c4f35f07685d25fbd";

        private FirmwareImage(byte[] rawBytes, IList<FirmwareRecord> records,
            uint terminatorType, string sha256)
        {
            this.rawBytes = (byte[])rawBytes.Clone();
            Records = new List<FirmwareRecord>(records).AsReadOnly();
            TerminatorType = terminatorType;
            Sha256 = sha256;
        }

        public byte[] RawBytes { get { return (byte[])rawBytes.Clone(); } }
        public int FileLength { get { return rawBytes.Length; } }
        public IList<FirmwareRecord> Records { get; private set; }
        public uint TerminatorType { get; private set; }
        public string Sha256 { get; private set; }

        public int PayloadLength
        {
            get
            {
                int total = 0;
                foreach (FirmwareRecord record in Records)
                {
                    total = checked(total + record.DataLength);
                }
                return total;
            }
        }

        public bool IsKnownUsb2XchangeImage
        {
            get
            {
                return rawBytes.Length == KnownUsb2XchangeFileLength &&
                    string.Equals(Sha256, KnownUsb2XchangeSha256,
                        StringComparison.OrdinalIgnoreCase);
            }
        }

        public static FirmwareImage Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A firmware path is required.", "path");
            }

            return Parse(File.ReadAllBytes(path));
        }

        public static FirmwareImage Parse(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }
            if (bytes.Length == 0 || bytes.Length % ExternalRecordSize != 0)
            {
                throw new ProtocolException(
                    "Firmware length must be a nonzero multiple of 28 bytes.");
            }

            List<FirmwareRecord> records = new List<FirmwareRecord>();
            bool sawTerminator = false;
            uint terminatorType = 0;

            for (int offset = 0; offset < bytes.Length; offset += ExternalRecordSize)
            {
                uint length = ReadUInt32LittleEndian(bytes, offset);
                uint address = ReadUInt32LittleEndian(bytes, offset + 4);
                uint type = ReadUInt32LittleEndian(bytes, offset + 8);

                if (length > MaximumRecordDataLength)
                {
                    throw new ProtocolException(string.Format(
                        "Firmware record {0} has invalid length {1}.",
                        offset / ExternalRecordSize, length));
                }

                if (type != 0)
                {
                    if (offset + ExternalRecordSize != bytes.Length)
                    {
                        throw new ProtocolException(
                            "Firmware terminator must be the final record.");
                    }
                    sawTerminator = true;
                    terminatorType = type;
                    break;
                }

                if (address > UInt16.MaxValue || address + length > 0x10000U)
                {
                    throw new ProtocolException(string.Format(
                        "Firmware record {0} exceeds the 16-bit device address space.",
                        offset / ExternalRecordSize));
                }

                byte[] data = new byte[length];
                Buffer.BlockCopy(bytes, offset + 12, data, 0, (int)length);
                records.Add(new FirmwareRecord(address, data));
            }

            if (!sawTerminator)
            {
                throw new ProtocolException("Firmware has no terminating record.");
            }
            if (records.Count == 0)
            {
                throw new ProtocolException("Firmware has no data records.");
            }

            return new FirmwareImage((byte[])bytes.Clone(), records,
                terminatorType, ComputeSha256(bytes));
        }

        public void RequireKnownUsb2XchangeImage()
        {
            if (!IsKnownUsb2XchangeImage)
            {
                throw new ProtocolException(string.Format(
                    "Refusing unknown firmware: length={0}, SHA-256={1}. " +
                    "Expected length={2}, SHA-256={3}.",
                    rawBytes.Length, Sha256, KnownUsb2XchangeFileLength,
                    KnownUsb2XchangeSha256));
            }
            if (Records.Count != 588 || PayloadLength != 9408)
            {
                throw new ProtocolException(
                    "Known firmware hash matched but record statistics did not.");
            }
        }

        private static uint ReadUInt32LittleEndian(byte[] bytes, int offset)
        {
            return (uint)bytes[offset] |
                ((uint)bytes[offset + 1] << 8) |
                ((uint)bytes[offset + 2] << 16) |
                ((uint)bytes[offset + 3] << 24);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes);
                return BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
