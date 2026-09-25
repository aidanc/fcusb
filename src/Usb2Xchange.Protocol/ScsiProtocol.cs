// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;

namespace Usb2Xchange.Protocol
{
    public enum DataDirection
    {
        None,
        In,
        Out
    }

    public enum AdapterStatus : byte
    {
        Success = 0x00,
        CheckCondition = 0x02,
        Busy = 0x08,
        SelectionTimeout = 0x8A
    }

    public sealed class CommandStatusWrapper
    {
        internal CommandStatusWrapper(uint tag, uint residue, byte rawStatus,
            uint requestedLength)
        {
            Tag = tag;
            Residue = residue;
            RawStatus = rawStatus;
            RequestedLength = requestedLength;
        }

        public uint Tag { get; private set; }
        public uint Residue { get; private set; }
        public byte RawStatus { get; private set; }
        public uint RequestedLength { get; private set; }
        public uint ActualLength { get { return RequestedLength - Residue; } }
        public AdapterStatus Status { get { return (AdapterStatus)RawStatus; } }
        public bool IsKnownStatus
        {
            get
            {
                return RawStatus == (byte)AdapterStatus.Success ||
                    RawStatus == (byte)AdapterStatus.CheckCondition ||
                    RawStatus == (byte)AdapterStatus.Busy ||
                    RawStatus == (byte)AdapterStatus.SelectionTimeout;
            }
        }
    }

    public static class ScsiFraming
    {
        public const uint CommandSignature = 0x43425355U;
        public const uint StatusSignature = 0x53425355U;
        public const int CommandWrapperLength = 31;
        public const int StatusWrapperLength = 13;
        public const uint PrecisionTwoLoaderBufferD8Length = 66;
        public const uint PrecisionTwoScannerReadyLength = 2;
        public const uint PrecisionTwoFaultPixelBufferLength = 22;
        public const uint PrecisionTwoFaultPixelDataLength = 58;
        public const uint PrecisionTwoCalibrationBufferLength = 1024;
        public const uint PrecisionTwoDynamicConfigurationLength = 1024;
        public const uint PrecisionTwoD8Offset55Length = 10;
        public const uint PrecisionTwoStartupWriteBuffer1082Length = 1082;
        public const uint PrecisionTwoStartupReadBufferE01072Length = 1072;
        public const uint PrecisionTwoPreviewSetWindowLength = 84;
        public const uint PrecisionTwoPreviewBytesPerPixel = 6;
        public const uint PrecisionTwoLoaderFirstRecordLength = 4106;

        public static byte[] BuildCommandWrapper(uint tag, uint transferLength,
            DataDirection direction, byte target, byte lun, byte[] cdb)
        {
            if (target > 6)
            {
                throw new ArgumentOutOfRangeException("target",
                    "The verified legacy scan range is target 0 through 6.");
            }
            if (lun > 7)
            {
                throw new ArgumentOutOfRangeException("lun");
            }
            if (cdb == null)
            {
                throw new ArgumentNullException("cdb");
            }
            if (cdb.Length < 2 || cdb.Length > 16)
            {
                throw new ArgumentException("CDB length must be 2 through 16 bytes.", "cdb");
            }
            if (transferLength == 0 && direction != DataDirection.None)
            {
                throw new ArgumentException(
                    "A zero-length transfer must use DataDirection.None.", "direction");
            }
            if (transferLength != 0 && direction == DataDirection.None)
            {
                throw new ArgumentException(
                    "A nonzero transfer must specify DataDirection.In or Out.", "direction");
            }

            byte[] encodedCdb = (byte[])cdb.Clone();
            encodedCdb[1] = (byte)((encodedCdb[1] & 0x1F) | ((lun & 0x07) << 5));

            byte[] wrapper = new byte[CommandWrapperLength];
            WriteUInt32LittleEndian(wrapper, 0, CommandSignature);
            WriteUInt32LittleEndian(wrapper, 4, tag);
            WriteUInt32LittleEndian(wrapper, 8, transferLength);
            wrapper[12] = direction == DataDirection.In ? (byte)0x80 : (byte)0x00;
            wrapper[13] = target;
            wrapper[14] = (byte)encodedCdb.Length;
            Buffer.BlockCopy(encodedCdb, 0, wrapper, 15, encodedCdb.Length);
            return wrapper;
        }

        public static CommandStatusWrapper ParseStatusWrapper(byte[] bytes,
            uint expectedTag, uint requestedLength)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }
            if (bytes.Length != StatusWrapperLength)
            {
                throw new ProtocolException("CSW must contain exactly 13 bytes.");
            }

            uint signature = ReadUInt32LittleEndian(bytes, 0);
            if (signature != StatusSignature)
            {
                throw new ProtocolException(string.Format(
                    "Invalid CSW signature 0x{0:X8}.", signature));
            }

            uint tag = ReadUInt32LittleEndian(bytes, 4);
            if (tag != expectedTag)
            {
                throw new ProtocolException(string.Format(
                    "CSW tag 0x{0:X8} does not match CBW tag 0x{1:X8}.",
                    tag, expectedTag));
            }

            uint residue = ReadUInt32LittleEndian(bytes, 8);
            if (residue > requestedLength)
            {
                throw new ProtocolException(string.Format(
                    "CSW residue {0} exceeds requested length {1}.",
                    residue, requestedLength));
            }

            return new CommandStatusWrapper(tag, residue, bytes[12], requestedLength);
        }

        public static byte[] BuildInquiryCdb(byte allocationLength)
        {
            if (allocationLength < 36)
            {
                throw new ArgumentOutOfRangeException("allocationLength",
                    "At least 36 bytes are required for vendor/product/revision fields.");
            }
            return new byte[] { 0x12, 0x00, 0x00, 0x00, allocationLength, 0x00 };
        }

        public static byte[] BuildRequestSenseCdb(byte allocationLength)
        {
            if (allocationLength == 0)
            {
                throw new ArgumentOutOfRangeException("allocationLength");
            }
            return new byte[] { 0x03, 0x00, 0x00, 0x00, allocationLength, 0x00 };
        }

        public static byte[] BuildPrecisionTwoLoaderReadBufferD8Cdb()
        {
            return new byte[]
            {
                0x3C, 0x00, 0xD8, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x42, 0x00
            };
        }

        public static byte[] BuildPrecisionTwoScannerReadyCdb()
        {
            return new byte[] { 0xDF, 0x00, 0x00, 0x00, 0x02, 0x00 };
        }

        public static byte[] BuildPrecisionTwoFaultPixelReadBufferCdb()
        {
            return new byte[]
            {
                0x3C, 0x00, 0xE0, 0x00, 0x04,
                0x00, 0x00, 0x00, 0x16, 0x00
            };
        }

        public static byte[] BuildPrecisionTwoFaultPixelDataReadBufferCdb()
        {
            return new byte[]
            {
                0x3C, 0x00, 0xE0, 0x00, 0x04,
                0x00, 0x00, 0x00, 0x3A, 0x00
            };
        }

        public static byte[] BuildPrecisionTwoCalibrationReadBufferCdb()
        {
            return new byte[]
            {
                0x3C, 0x00, 0xE0, 0x00, 0x00,
                0x00, 0x00, 0x04, 0x00, 0x00
            };
        }

        public static byte[] BuildPrecisionTwoD8Offset55ReadBufferCdb()
        {
            return new byte[]
            {
                0x3C, 0x00, 0xD8, 0x00, 0x00,
                0x37, 0x00, 0x00, 0x0A, 0x00
            };
        }

        public static byte[]
            BuildPrecisionTwoDynamicConfigurationD8ReadBufferCdb()
        {
            return new byte[]
            {
                0x3C, 0x00, 0xD8, 0x00, 0x00,
                0x00, 0x00, 0x04, 0x00, 0x00
            };
        }

        public static byte[] BuildPrecisionTwoStartupWriteBuffer1082Cdb()
        {
            return new byte[]
            {
                0x3B, 0x01, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x04, 0x3A, 0x00
            };
        }

        public static byte[] BuildPrecisionTwoStartupReadBufferE01072Cdb()
        {
            return new byte[]
            {
                0x3C, 0x00, 0xE0, 0x00, 0x00,
                0x00, 0x00, 0x04, 0x30, 0x00
            };
        }

        public static byte[] BuildPrecisionTwoPreviewSetWindowCdb()
        {
            return new byte[]
            {
                0x24, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x54, 0x00
            };
        }

        public static uint GetPrecisionTwoPreviewImageReadLength(
            uint scanWidth)
        {
            if (scanWidth == 0 ||
                scanWidth > 0xFFFFFFU / PrecisionTwoPreviewBytesPerPixel)
            {
                throw new ArgumentOutOfRangeException("scanWidth");
            }
            return scanWidth * PrecisionTwoPreviewBytesPerPixel;
        }

        public static byte[] BuildPrecisionTwoPreviewImageReadCdb(
            uint scanWidth)
        {
            uint transferLength =
                GetPrecisionTwoPreviewImageReadLength(scanWidth);
            return new byte[]
            {
                0x28, 0x00, 0x00, 0x00, 0x28,
                0x00, (byte)((transferLength >> 16) & 0xFF),
                (byte)((transferLength >> 8) & 0xFF),
                (byte)(transferLength & 0xFF), 0x00
            };
        }

        public static byte[] BuildPrecisionTwoLoaderWriteBufferModeOneZeroCdb()
        {
            return new byte[]
            {
                0x3B, 0x01, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00
            };
        }

        public static byte[] BuildPrecisionTwoLoaderFirstRecordCdb()
        {
            return new byte[]
            {
                0x3B, 0x01, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x10, 0x0A, 0x00
            };
        }

        public static byte[] BuildPrecisionTwoLoaderTerminalRecordCdb()
        {
            return new byte[]
            {
                0x3B, 0x01, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x0A, 0x00
            };
        }

        public static string ToHex(byte[] bytes)
        {
            if (bytes == null)
            {
                return "<null>";
            }
            return BitConverter.ToString(bytes).Replace('-', ' ');
        }

        internal static uint ReadUInt32LittleEndian(byte[] bytes, int offset)
        {
            return (uint)bytes[offset] |
                ((uint)bytes[offset + 1] << 8) |
                ((uint)bytes[offset + 2] << 16) |
                ((uint)bytes[offset + 3] << 24);
        }

        internal static void WriteUInt32LittleEndian(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)(value & 0xFF);
            bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
            bytes[offset + 2] = (byte)((value >> 16) & 0xFF);
            bytes[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
