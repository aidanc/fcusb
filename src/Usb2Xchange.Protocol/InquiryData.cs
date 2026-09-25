// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Text;

namespace Usb2Xchange.Protocol
{
    public sealed class InquiryData
    {
        private InquiryData()
        {
        }

        public byte PeripheralQualifier { get; private set; }
        public byte PeripheralDeviceType { get; private set; }
        public bool Removable { get; private set; }
        public string Vendor { get; private set; }
        public string Product { get; private set; }
        public string Revision { get; private set; }

        public static InquiryData Parse(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }
            if (bytes.Length < 36)
            {
                throw new ProtocolException(string.Format(
                    "INQUIRY returned {0} bytes; 36 are required.", bytes.Length));
            }

            InquiryData result = new InquiryData();
            result.PeripheralQualifier = (byte)((bytes[0] >> 5) & 0x07);
            result.PeripheralDeviceType = (byte)(bytes[0] & 0x1F);
            result.Removable = (bytes[1] & 0x80) != 0;
            result.Vendor = ReadAscii(bytes, 8, 8);
            result.Product = ReadAscii(bytes, 16, 16);
            result.Revision = ReadAscii(bytes, 32, 4);
            return result;
        }

        private static string ReadAscii(byte[] bytes, int offset, int length)
        {
            return Encoding.ASCII.GetString(bytes, offset, length).TrimEnd(' ', '\0');
        }
    }
}
