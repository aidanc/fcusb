// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Text;

namespace Usb2Xchange.BrokerProtocol
{
    public enum MiniportReturnCode : uint
    {
        Success = 0,
        InvalidMessage = 1,
        StaleGeneration = 2,
        NoPendingRequest = 3,
        Busy = 4,
        NotReady = 5
    }

    public sealed class MiniportResponse
    {
        private readonly byte[] payload;

        internal MiniportResponse(MiniportReturnCode returnCode,
            byte[] payload)
        {
            ReturnCode = returnCode;
            this.payload = (byte[])payload.Clone();
        }

        public MiniportReturnCode ReturnCode { get; private set; }
        public byte[] Payload { get { return (byte[])payload.Clone(); } }
    }

    public static class MiniportWireProtocol
    {
        public const int SrbIoControlHeaderSize = 28;
        public const uint DefaultTimeoutSeconds = 30;

        private static readonly byte[] Signature =
            Encoding.ASCII.GetBytes(BrokerWireProtocol.SrbIoControlSignature +
                "\0");

        public static byte[] WrapMessage(byte[] payload)
        {
            return WrapMessage(payload, DefaultTimeoutSeconds);
        }

        public static byte[] WrapMessage(byte[] payload, uint timeoutSeconds)
        {
            if (payload == null)
            {
                throw new ArgumentNullException("payload");
            }
            if (payload.Length < BrokerWireProtocol.HeaderSize ||
                payload.Length > BrokerWireProtocol.CompletionMaximumSize ||
                timeoutSeconds == 0 || timeoutSeconds > 300)
            {
                throw new BrokerProtocolException(
                    "Miniport payload or timeout is invalid.");
            }

            byte[] bytes = new byte[checked(SrbIoControlHeaderSize +
                payload.Length)];
            WriteUInt32(bytes, 0, SrbIoControlHeaderSize);
            Buffer.BlockCopy(Signature, 0, bytes, 4, Signature.Length);
            WriteUInt32(bytes, 12, timeoutSeconds);
            WriteUInt32(bytes, 16,
                BrokerWireProtocol.SrbControlBrokerMessage);
            WriteUInt32(bytes, 20, (uint)MiniportReturnCode.Success);
            WriteUInt32(bytes, 24, checked((uint)payload.Length));
            Buffer.BlockCopy(payload, 0, bytes, SrbIoControlHeaderSize,
                payload.Length);
            return bytes;
        }

        public static MiniportResponse ParseResponse(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }
            if (bytes.Length < SrbIoControlHeaderSize ||
                ReadUInt32(bytes, 0) != SrbIoControlHeaderSize ||
                ReadUInt32(bytes, 16) !=
                    BrokerWireProtocol.SrbControlBrokerMessage)
            {
                throw new BrokerProtocolException(
                    "SRB_IO_CONTROL header is invalid.");
            }
            for (int i = 0; i < Signature.Length; i++)
            {
                if (bytes[4 + i] != Signature[i])
                {
                    throw new BrokerProtocolException(
                        "SRB_IO_CONTROL signature is invalid.");
                }
            }
            uint payloadLength = ReadUInt32(bytes, 24);
            if (payloadLength != bytes.Length - SrbIoControlHeaderSize)
            {
                throw new BrokerProtocolException(
                    "SRB_IO_CONTROL payload length is invalid.");
            }
            uint returnCodeValue = ReadUInt32(bytes, 20);
            if (!Enum.IsDefined(typeof(MiniportReturnCode), returnCodeValue))
            {
                throw new BrokerProtocolException(
                    "Miniport returned an unknown status.");
            }
            byte[] payload = new byte[checked((int)payloadLength)];
            Buffer.BlockCopy(bytes, SrbIoControlHeaderSize, payload, 0,
                payload.Length);
            return new MiniportResponse((MiniportReturnCode)returnCodeValue,
                payload);
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            unchecked
            {
                return (uint)(bytes[offset] |
                    (bytes[offset + 1] << 8) |
                    (bytes[offset + 2] << 16) |
                    (bytes[offset + 3] << 24));
            }
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = unchecked((byte)value);
            bytes[offset + 1] = unchecked((byte)(value >> 8));
            bytes[offset + 2] = unchecked((byte)(value >> 16));
            bytes[offset + 3] = unchecked((byte)(value >> 24));
        }
    }
}
