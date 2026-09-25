// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.IO;
using System.Security.Cryptography;

namespace Usb2Xchange.FlexColorAspiLauncher
{
    internal static class TiffOutputInspector
    {
        private const int MaximumOutputBytes = 64 * 1024 * 1024;

        internal static TiffOutputMetadata Inspect(string path,
            uint expectedWidth, uint expectedHeight, uint expectedPpi)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < 32 ||
                info.Length > MaximumOutputBytes)
            {
                throw new InvalidOperationException(
                    "Offline TIFF is missing or outside its bounded size.");
            }
            return Inspect(File.ReadAllBytes(path), expectedWidth,
                expectedHeight, expectedPpi);
        }

        internal static TiffOutputMetadata Inspect(byte[] data,
            uint expectedWidth, uint expectedHeight, uint expectedPpi)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            if (data.Length < 32 || data.Length > MaximumOutputBytes ||
                data[0] != (byte)'M' || data[1] != (byte)'M' ||
                ReadUInt16(data, 2) != 42)
            {
                throw new InvalidOperationException(
                    "Offline output is not a bounded big-endian TIFF.");
            }

            uint ifdOffset = ReadUInt32(data, 4);
            EnsureRange(data, ifdOffset, 2);
            ushort entryCount = ReadUInt16(data, checked((int)ifdOffset));
            if (entryCount == 0 || entryCount > 64)
            {
                throw new InvalidOperationException(
                    "Offline TIFF has an invalid IFD entry count.");
            }

            uint width = 0;
            uint height = 0;
            uint stripOffset = 0;
            uint stripBytes = 0;
            uint rowsPerStrip = 0;
            ushort compression = 0;
            ushort photometric = 0;
            ushort samplesPerPixel = 0;
            ushort resolutionUnit = 0;
            ushort[] bitsPerSample = null;
            uint xPpi = 0;
            uint yPpi = 0;

            for (int index = 0; index < entryCount; ++index)
            {
                int offset = checked((int)ifdOffset + 2 + index * 12);
                EnsureRange(data, checked((uint)offset), 12);
                ushort tag = ReadUInt16(data, offset);
                ushort type = ReadUInt16(data, offset + 2);
                uint count = ReadUInt32(data, offset + 4);
                uint value = ReadUInt32(data, offset + 8);
                switch (tag)
                {
                    case 0x0100:
                        width = ReadSingleUnsigned(data, offset, type, count);
                        break;
                    case 0x0101:
                        height = ReadSingleUnsigned(data, offset, type, count);
                        break;
                    case 0x0102:
                        bitsPerSample = ReadUShortArray(data, offset, type,
                            count);
                        break;
                    case 0x0103:
                        compression = checked((ushort)ReadSingleUnsigned(
                            data, offset, type, count));
                        break;
                    case 0x0106:
                        photometric = checked((ushort)ReadSingleUnsigned(
                            data, offset, type, count));
                        break;
                    case 0x0111:
                        stripOffset = ReadSingleUnsigned(data, offset, type,
                            count);
                        break;
                    case 0x0115:
                        samplesPerPixel = checked((ushort)ReadSingleUnsigned(
                            data, offset, type, count));
                        break;
                    case 0x0116:
                        rowsPerStrip = ReadSingleUnsigned(data, offset, type,
                            count);
                        break;
                    case 0x0117:
                        stripBytes = ReadSingleUnsigned(data, offset, type,
                            count);
                        break;
                    case 0x011A:
                        xPpi = ReadRational(data, type, count, value);
                        break;
                    case 0x011B:
                        yPpi = ReadRational(data, type, count, value);
                        break;
                    case 0x0128:
                        resolutionUnit = checked((ushort)ReadSingleUnsigned(
                            data, offset, type, count));
                        break;
                }
            }

            uint expectedBytes = checked(expectedWidth * expectedHeight * 3);
            if (width != expectedWidth || height != expectedHeight ||
                bitsPerSample == null || bitsPerSample.Length != 3 ||
                bitsPerSample[0] != 8 || bitsPerSample[1] != 8 ||
                bitsPerSample[2] != 8 || compression != 1 ||
                photometric != 2 || samplesPerPixel != 3 ||
                rowsPerStrip != expectedHeight || stripBytes != expectedBytes ||
                xPpi != expectedPpi || yPpi != expectedPpi ||
                resolutionUnit != 2 || stripOffset < 8 ||
                checked((ulong)stripOffset + stripBytes) !=
                    checked((ulong)data.Length))
            {
                throw new InvalidOperationException(
                    "Offline TIFF does not match the exact RGB8, uncompressed, " +
                    "single-strip output contract.");
            }

            string hash;
            using (SHA256 sha = SHA256.Create())
            {
                hash = BitConverter.ToString(sha.ComputeHash(data)).
                    Replace("-", string.Empty);
            }
            return new TiffOutputMetadata(width, height, xPpi, stripBytes,
                hash);
        }

        private static uint ReadSingleUnsigned(byte[] data, int entryOffset,
            ushort type, uint count)
        {
            if (count != 1)
            {
                throw new InvalidOperationException(
                    "Offline TIFF contains a multi-value scalar tag.");
            }
            if (type == 3)
            {
                return ReadUInt16(data, entryOffset + 8);
            }
            if (type == 4)
            {
                return ReadUInt32(data, entryOffset + 8);
            }
            throw new InvalidOperationException(
                "Offline TIFF scalar tag has an unsupported type.");
        }

        private static ushort[] ReadUShortArray(byte[] data, int entryOffset,
            ushort type, uint count)
        {
            if (type != 3 || count == 0 || count > 16)
            {
                throw new InvalidOperationException(
                    "Offline TIFF SHORT array is invalid.");
            }
            uint arrayOffset = count <= 2
                ? checked((uint)(entryOffset + 8))
                : ReadUInt32(data, entryOffset + 8);
            EnsureRange(data, arrayOffset, checked((int)count * 2));
            var values = new ushort[checked((int)count)];
            for (int index = 0; index < values.Length; ++index)
            {
                values[index] = ReadUInt16(data,
                    checked((int)arrayOffset + index * 2));
            }
            return values;
        }

        private static uint ReadRational(byte[] data, ushort type, uint count,
            uint valueOffset)
        {
            if (type != 5 || count != 1)
            {
                throw new InvalidOperationException(
                    "Offline TIFF resolution is not one RATIONAL.");
            }
            EnsureRange(data, valueOffset, 8);
            uint numerator = ReadUInt32(data, checked((int)valueOffset));
            uint denominator = ReadUInt32(data,
                checked((int)valueOffset + 4));
            if (denominator == 0 || numerator % denominator != 0)
            {
                throw new InvalidOperationException(
                    "Offline TIFF resolution is not an integral value.");
            }
            return numerator / denominator;
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            EnsureRange(data, checked((uint)offset), 2);
            return checked((ushort)((data[offset] << 8) | data[offset + 1]));
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            EnsureRange(data, checked((uint)offset), 4);
            return ((uint)data[offset] << 24) |
                ((uint)data[offset + 1] << 16) |
                ((uint)data[offset + 2] << 8) |
                data[offset + 3];
        }

        private static void EnsureRange(byte[] data, uint offset, int length)
        {
            if (length < 0 || offset > data.Length ||
                checked((ulong)offset + (uint)length) >
                    checked((ulong)data.Length))
            {
                throw new InvalidOperationException(
                    "Offline TIFF contains an out-of-range field.");
            }
        }
    }

    internal sealed class TiffOutputMetadata
    {
        internal TiffOutputMetadata(uint width, uint height, uint ppi,
            uint pixelBytes, string sha256)
        {
            Width = width;
            Height = height;
            Ppi = ppi;
            PixelBytes = pixelBytes;
            Sha256 = sha256;
        }

        internal uint Width { get; private set; }
        internal uint Height { get; private set; }
        internal uint Ppi { get; private set; }
        internal uint PixelBytes { get; private set; }
        internal string Sha256 { get; private set; }
    }
}
