// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Security.Cryptography;
using System.Text;

namespace Usb2Xchange.BrokerProtocol
{
    public enum BrokerMessageType : ushort
    {
        ScsiRequest = 1,
        ScsiCompletion = 2,
        AdapterState = 3,
        ServiceWait = 4
    }

    public enum BrokerDirection : byte
    {
        None = 0,
        In = 1,
        Out = 2
    }

    public enum BrokerTransportStatus : uint
    {
        Success = 0,
        NoDevice = 1,
        Timeout = 2,
        Cancelled = 3,
        IoError = 4,
        ProtocolError = 5,
        Blocked = 6,
        StaleGeneration = 7
    }

    public enum BrokerAdapterStateKind : uint
    {
        Offline = 0,
        Online = 1
    }

    public sealed class BrokerProtocolException : Exception
    {
        public BrokerProtocolException(string message) : base(message)
        {
        }
    }

    public sealed class BrokerRequest
    {
        private readonly byte[] cdb;
        private readonly byte[] data;

        public BrokerRequest(ulong requestId, ulong generation, byte pathId,
            byte targetId, byte lun, byte physicalTargetId, byte physicalLun,
            BrokerDirection direction, uint transferLength,
            uint timeoutMilliseconds, ushort senseAllocationLength, byte[] cdb)
            : this(requestId, generation, pathId, targetId, lun,
                physicalTargetId, physicalLun, direction, transferLength,
                timeoutMilliseconds, senseAllocationLength, cdb, new byte[0])
        {
        }

        public BrokerRequest(ulong requestId, ulong generation, byte pathId,
            byte targetId, byte lun, byte physicalTargetId, byte physicalLun,
            BrokerDirection direction, uint transferLength,
            uint timeoutMilliseconds, ushort senseAllocationLength, byte[] cdb,
            byte[] data)
        {
            if (cdb == null)
            {
                throw new ArgumentNullException("cdb");
            }
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            RequestId = requestId;
            Generation = generation;
            PathId = pathId;
            TargetId = targetId;
            Lun = lun;
            PhysicalTargetId = physicalTargetId;
            PhysicalLun = physicalLun;
            Direction = direction;
            TransferLength = transferLength;
            TimeoutMilliseconds = timeoutMilliseconds;
            SenseAllocationLength = senseAllocationLength;
            this.cdb = (byte[])cdb.Clone();
            this.data = (byte[])data.Clone();
        }

        public ulong RequestId { get; private set; }
        public ulong Generation { get; private set; }
        public byte PathId { get; private set; }
        public byte TargetId { get; private set; }
        public byte Lun { get; private set; }
        public byte PhysicalTargetId { get; private set; }
        public byte PhysicalLun { get; private set; }
        public BrokerDirection Direction { get; private set; }
        public uint TransferLength { get; private set; }
        public uint TimeoutMilliseconds { get; private set; }
        public ushort SenseAllocationLength { get; private set; }
        public byte[] Cdb { get { return (byte[])cdb.Clone(); } }
        public int DataLength { get { return data.Length; } }
        public byte[] Data { get { return (byte[])data.Clone(); } }
    }

    public sealed class BrokerCompletion
    {
        private readonly byte[] sense;
        private readonly byte[] data;

        public BrokerCompletion(ulong requestId, ulong generation,
            BrokerTransportStatus transportStatus, byte adapterStatus,
            byte scsiStatus, uint transferLength, uint residue, byte[] sense,
            byte[] data)
        {
            if (sense == null)
            {
                throw new ArgumentNullException("sense");
            }
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            RequestId = requestId;
            Generation = generation;
            TransportStatus = transportStatus;
            AdapterStatus = adapterStatus;
            ScsiStatus = scsiStatus;
            TransferLength = transferLength;
            Residue = residue;
            this.sense = (byte[])sense.Clone();
            this.data = (byte[])data.Clone();
        }

        public ulong RequestId { get; private set; }
        public ulong Generation { get; private set; }
        public BrokerTransportStatus TransportStatus { get; private set; }
        public byte AdapterStatus { get; private set; }
        public byte ScsiStatus { get; private set; }
        public uint TransferLength { get; private set; }
        public uint Residue { get; private set; }
        public byte[] Sense { get { return (byte[])sense.Clone(); } }
        public byte[] Data { get { return (byte[])data.Clone(); } }
    }

    public sealed class BrokerAdapterState
    {
        private readonly byte[] inquiry;

        public BrokerAdapterState(ulong generation, BrokerAdapterStateKind state,
            byte physicalTargetId, byte physicalLun,
            byte peripheralDeviceType, byte[] inquiry)
        {
            if (inquiry == null)
            {
                throw new ArgumentNullException("inquiry");
            }
            Generation = generation;
            State = state;
            PhysicalTargetId = physicalTargetId;
            PhysicalLun = physicalLun;
            PeripheralDeviceType = peripheralDeviceType;
            this.inquiry = (byte[])inquiry.Clone();
        }

        public ulong Generation { get; private set; }
        public BrokerAdapterStateKind State { get; private set; }
        public byte PhysicalTargetId { get; private set; }
        public byte PhysicalLun { get; private set; }
        public byte PeripheralDeviceType { get; private set; }
        public byte[] Inquiry { get { return (byte[])inquiry.Clone(); } }
    }

    public static class BrokerWireProtocol
    {
        public const string PrecisionTwoPreviewSetWindowSha256 =
            "F45F2A91958286CF750AB56588E2C20A83E95E9B5973C046B6068C6C2FEC0EF6";
        public const uint Magic = 0x58423255;
        public const ushort VersionMajor = 1;
        public const ushort VersionMinor = 2;
        public const string SrbIoControlSignature = "U2XCHG1";
        public const uint SrbControlBrokerMessage = 1;
        public const int HeaderSize = 32;
        public const int RequestPrefixSize = 80;
        public const int RequestMaximumSize =
            RequestPrefixSize + MaximumTransferLength;
        public const int CompletionPrefixSize = 88;
        public const int CompletionMaximumSize = 4200;
        public const int AdapterStateSize = 80;
        public const int ServiceWaitSize = 32;
        public const int MaximumCdbLength = 16;
        public const int MaximumSenseLength = 32;
        public const int MaximumTransferLength = 4112;
        public const uint MaximumTimeoutMilliseconds = 60000;
        public const int InquiryLength = 36;

        private const byte SrbFunctionExecuteScsi = 0;
        private const byte AdapterSuccess = 0x00;
        private const byte AdapterCheckCondition = 0x02;
        private const byte AdapterBusy = 0x08;
        private const byte AdapterSelectionTimeout = 0x8A;

        public static byte[] SerializeRequest(BrokerRequest request)
        {
            ValidateRequestEnvelope(request);
            byte[] data = request.Data;
            int messageSize = checked(RequestPrefixSize + data.Length);
            byte[] bytes = new byte[messageSize];
            WriteHeader(bytes, BrokerMessageType.ScsiRequest, messageSize,
                request.RequestId, request.Generation);
            bytes[32] = request.PathId;
            bytes[33] = request.TargetId;
            bytes[34] = request.Lun;
            bytes[35] = request.PhysicalTargetId;
            bytes[36] = request.PhysicalLun;
            byte[] cdb = request.Cdb;
            bytes[37] = checked((byte)cdb.Length);
            bytes[38] = (byte)request.Direction;
            bytes[39] = SrbFunctionExecuteScsi;
            WriteUInt32(bytes, 40, request.TransferLength);
            WriteUInt32(bytes, 44, request.TimeoutMilliseconds);
            WriteUInt16(bytes, 48, request.SenseAllocationLength);
            Buffer.BlockCopy(cdb, 0, bytes, 52, cdb.Length);
            WriteUInt32(bytes, 68, checked((uint)data.Length));
            Buffer.BlockCopy(data, 0, bytes, RequestPrefixSize, data.Length);
            return bytes;
        }

        public static BrokerRequest ParseRequest(byte[] bytes)
        {
            ValidateHeader(bytes, BrokerMessageType.ScsiRequest,
                RequestPrefixSize, RequestMaximumSize);
            RequireZero(bytes, 50, 2, "request reserved field");
            byte cdbLength = bytes[37];
            if (cdbLength == 0 || cdbLength > MaximumCdbLength)
            {
                throw new BrokerProtocolException("Invalid CDB length.");
            }
            RequireZero(bytes, 52 + cdbLength,
                MaximumCdbLength - cdbLength, "unused CDB bytes");
            uint dataLength = ReadUInt32(bytes, 68);
            if (dataLength > MaximumTransferLength ||
                bytes.Length != RequestPrefixSize + dataLength)
            {
                throw new BrokerProtocolException(
                    "Invalid request data length.");
            }
            RequireZero(bytes, 72, 8, "request reserved bytes");
            byte[] cdb = Copy(bytes, 52, cdbLength);
            BrokerRequest request = new BrokerRequest(ReadUInt64(bytes, 16),
                ReadUInt64(bytes, 24), bytes[32], bytes[33], bytes[34],
                bytes[35], bytes[36], (BrokerDirection)bytes[38],
                ReadUInt32(bytes, 40), ReadUInt32(bytes, 44),
                ReadUInt16(bytes, 48), cdb,
                Copy(bytes, RequestPrefixSize, checked((int)dataLength)));
            if (bytes[39] != SrbFunctionExecuteScsi)
            {
                throw new BrokerProtocolException("Unsupported SRB function.");
            }
            ValidateRequestEnvelope(request);
            return request;
        }

        public static byte[] SerializeCompletion(BrokerRequest request,
            BrokerCompletion completion)
        {
            ValidateCompletion(request, completion);
            byte[] data = completion.Data;
            byte[] sense = completion.Sense;
            int messageSize = checked(CompletionPrefixSize + data.Length);
            byte[] bytes = new byte[messageSize];
            WriteHeader(bytes, BrokerMessageType.ScsiCompletion, messageSize,
                completion.RequestId, completion.Generation);
            WriteUInt32(bytes, 32, (uint)completion.TransportStatus);
            bytes[36] = completion.AdapterStatus;
            bytes[37] = completion.ScsiStatus;
            bytes[38] = checked((byte)sense.Length);
            WriteUInt32(bytes, 40, completion.TransferLength);
            WriteUInt32(bytes, 44, completion.Residue);
            WriteUInt32(bytes, 48, checked((uint)data.Length));
            Buffer.BlockCopy(sense, 0, bytes, 56, sense.Length);
            Buffer.BlockCopy(data, 0, bytes, CompletionPrefixSize,
                data.Length);
            return bytes;
        }

        private static BrokerCompletion ParseCompletion(byte[] bytes)
        {
            ValidateHeader(bytes, BrokerMessageType.ScsiCompletion,
                CompletionPrefixSize, CompletionMaximumSize);
            if (!Enum.IsDefined(typeof(BrokerTransportStatus),
                ReadUInt32(bytes, 32)))
            {
                throw new BrokerProtocolException("Unknown transport status.");
            }
            int senseLength = bytes[38];
            if (senseLength > MaximumSenseLength)
            {
                throw new BrokerProtocolException("Sense data is too long.");
            }
            RequireZero(bytes, 39, 1, "completion reserved byte");
            RequireZero(bytes, 52, 4, "completion reserved field");
            RequireZero(bytes, 56 + senseLength,
                MaximumSenseLength - senseLength, "unused sense bytes");
            uint dataLength = ReadUInt32(bytes, 48);
            if (dataLength > MaximumTransferLength ||
                bytes.Length != CompletionPrefixSize + dataLength)
            {
                throw new BrokerProtocolException("Invalid completion data length.");
            }
            return new BrokerCompletion(ReadUInt64(bytes, 16),
                ReadUInt64(bytes, 24),
                (BrokerTransportStatus)ReadUInt32(bytes, 32), bytes[36],
                bytes[37], ReadUInt32(bytes, 40), ReadUInt32(bytes, 44),
                Copy(bytes, 56, senseLength),
                Copy(bytes, CompletionPrefixSize, checked((int)dataLength)));
        }

        public static BrokerCompletion ParseCompletion(BrokerRequest request,
            byte[] bytes)
        {
            BrokerCompletion completion = ParseCompletion(bytes);
            ValidateCompletion(request, completion);
            return completion;
        }

        public static void ValidateCompletion(BrokerRequest request,
            BrokerCompletion completion)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            if (completion == null)
            {
                throw new ArgumentNullException("completion");
            }
            ValidateRequestEnvelope(request);
            if (completion.RequestId != request.RequestId ||
                completion.Generation != request.Generation)
            {
                throw new BrokerProtocolException(
                    "Completion request ID or generation does not match.");
            }
            if (!Enum.IsDefined(typeof(BrokerTransportStatus),
                completion.TransportStatus))
            {
                throw new BrokerProtocolException("Unknown transport status.");
            }
            byte[] sense = completion.Sense;
            byte[] data = completion.Data;
            if (sense.Length > MaximumSenseLength ||
                sense.Length > request.SenseAllocationLength ||
                data.Length > MaximumTransferLength)
            {
                throw new BrokerProtocolException(
                    "Completion payload exceeds the protocol limit.");
            }
            if (completion.TransportStatus != BrokerTransportStatus.Success)
            {
                if (completion.AdapterStatus != 0 ||
                    completion.ScsiStatus != 0 ||
                    completion.TransferLength != 0 ||
                    completion.Residue != 0 || sense.Length != 0 ||
                    data.Length != 0)
                {
                    throw new BrokerProtocolException(
                        "Failed transport completion contains device data.");
                }
                return;
            }

            ValidateAdapterAndScsiStatus(completion.AdapterStatus,
                completion.ScsiStatus, sense.Length);
            if (completion.TransferLength > request.TransferLength ||
                completion.Residue > request.TransferLength ||
                completion.TransferLength + completion.Residue !=
                    request.TransferLength)
            {
                throw new BrokerProtocolException(
                    "Completion transfer length or residue is invalid.");
            }
            int expectedDataLength = request.Direction == BrokerDirection.In
                ? checked((int)completion.TransferLength) : 0;
            if (data.Length != expectedDataLength)
            {
                throw new BrokerProtocolException(
                    "Completion data length does not match the transfer.");
            }
            if ((completion.AdapterStatus == AdapterBusy ||
                    completion.AdapterStatus == AdapterSelectionTimeout) &&
                completion.TransferLength != 0)
            {
                throw new BrokerProtocolException(
                    "Busy or absent targets cannot return transferred data.");
            }
        }

        public static byte[] SerializeAdapterState(BrokerAdapterState state)
        {
            ValidateAdapterState(state);
            byte[] bytes = new byte[AdapterStateSize];
            WriteHeader(bytes, BrokerMessageType.AdapterState,
                AdapterStateSize, 0, state.Generation);
            WriteUInt32(bytes, 32, (uint)state.State);
            bytes[36] = state.PhysicalTargetId;
            bytes[37] = state.PhysicalLun;
            bytes[38] = state.PeripheralDeviceType;
            byte[] inquiry = state.Inquiry;
            WriteUInt32(bytes, 40, checked((uint)inquiry.Length));
            Buffer.BlockCopy(inquiry, 0, bytes, 44, inquiry.Length);
            return bytes;
        }

        public static BrokerAdapterState ParseAdapterState(byte[] bytes)
        {
            ValidateHeader(bytes, BrokerMessageType.AdapterState,
                AdapterStateSize, AdapterStateSize);
            if (ReadUInt64(bytes, 16) != 0)
            {
                throw new BrokerProtocolException(
                    "Adapter-state request ID must be zero.");
            }
            RequireZero(bytes, 39, 1, "adapter-state reserved byte");
            uint inquiryLength = ReadUInt32(bytes, 40);
            if (inquiryLength > InquiryLength)
            {
                throw new BrokerProtocolException("Invalid INQUIRY length.");
            }
            RequireZero(bytes, 44 + checked((int)inquiryLength),
                InquiryLength - checked((int)inquiryLength),
                "unused INQUIRY bytes");
            BrokerAdapterState state = new BrokerAdapterState(
                ReadUInt64(bytes, 24),
                (BrokerAdapterStateKind)ReadUInt32(bytes, 32), bytes[36],
                bytes[37], bytes[38],
                Copy(bytes, 44, checked((int)inquiryLength)));
            ValidateAdapterState(state);
            return state;
        }

        public static byte[] SerializeServiceWait(ulong generation)
        {
            if (generation == 0)
            {
                throw new BrokerProtocolException(
                    "Service-wait generation must be nonzero.");
            }
            byte[] bytes = new byte[ServiceWaitSize];
            WriteHeader(bytes, BrokerMessageType.ServiceWait,
                ServiceWaitSize, 0, generation);
            return bytes;
        }

        public static ulong ParseServiceWait(byte[] bytes)
        {
            ValidateHeader(bytes, BrokerMessageType.ServiceWait,
                ServiceWaitSize, ServiceWaitSize);
            if (ReadUInt64(bytes, 16) != 0)
            {
                throw new BrokerProtocolException(
                    "Service-wait request ID must be zero.");
            }
            return ReadUInt64(bytes, 24);
        }

        public static void ValidateReadOnlyRequest(BrokerRequest request)
        {
            ValidateRequestEnvelope(request);
            if (request.Direction == BrokerDirection.Out)
            {
                throw new BrokerProtocolException(
                    "Data-out requests are blocked from hardware.");
            }
            byte[] cdb = request.Cdb;
            if (cdb.Length == 12 && cdb[0] == 0xA0)
            {
                uint allocationLength = ((uint)cdb[6] << 24) |
                    ((uint)cdb[7] << 16) | ((uint)cdb[8] << 8) | cdb[9];
                if (request.Direction != BrokerDirection.In ||
                    request.TransferLength != 16 || allocationLength != 16 ||
                    cdb[1] != 0 || cdb[2] != 0 || cdb[3] != 0 ||
                    cdb[4] != 0 || cdb[5] != 0 || cdb[10] != 0 ||
                    cdb[11] != 0)
                {
                    throw new BrokerProtocolException(
                        "REPORT LUNS is outside the observed single-LUN form.");
                }
                return;
            }
            if (cdb.Length != 6)
            {
                throw new BrokerProtocolException(
                    "Only six-byte read-only CDBs are accepted initially.");
            }
            switch (cdb[0])
            {
                case 0x00:
                    if (request.Direction != BrokerDirection.None ||
                        request.TransferLength != 0)
                    {
                        throw new BrokerProtocolException(
                            "TEST UNIT READY must have no data phase.");
                    }
                    break;
                case 0x03:
                case 0x12:
                    if (request.Direction != BrokerDirection.In ||
                        request.TransferLength == 0 ||
                        request.TransferLength > 255 ||
                        cdb[4] != request.TransferLength)
                    {
                        throw new BrokerProtocolException(
                            "Read-only CDB allocation length is invalid.");
                    }
                    break;
                default:
                    throw new BrokerProtocolException(
                        "CDB opcode is outside the read-only allowlist.");
            }
        }

        public static void ValidateCapturedLoaderReadBufferD8(
            BrokerRequest request)
        {
            ValidateRequestEnvelope(request);
            byte[] cdb = request.Cdb;
            byte[] expected = new byte[]
            {
                0x3C, 0x00, 0xD8, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x42, 0x00
            };
            bool exactCdb = cdb.Length == expected.Length;
            if (exactCdb)
            {
                for (int index = 0; index < expected.Length; index++)
                {
                    if (cdb[index] != expected[index])
                    {
                        exactCdb = false;
                        break;
                    }
                }
            }
            if (!exactCdb || request.PhysicalTargetId != 5 ||
                request.PhysicalLun != 0 ||
                request.Direction != BrokerDirection.In ||
                request.TransferLength != 66 ||
                request.TimeoutMilliseconds != MaximumTimeoutMilliseconds ||
                request.SenseAllocationLength != MaximumSenseLength)
            {
                throw new BrokerProtocolException(
                    "READ BUFFER D8 is outside the exact captured loader form.");
            }
        }

        public static void ValidateCapturedOperationalScannerReady(
            BrokerRequest request)
        {
            ValidateRequestEnvelope(request);
            byte[] cdb = request.Cdb;
            byte[] expected = new byte[]
            {
                0xDF, 0x00, 0x00, 0x00, 0x02, 0x00
            };
            bool exactCdb = cdb.Length == expected.Length;
            if (exactCdb)
            {
                for (int index = 0; index < expected.Length; index++)
                {
                    if (cdb[index] != expected[index])
                    {
                        exactCdb = false;
                        break;
                    }
                }
            }
            if (!exactCdb || request.PhysicalTargetId != 5 ||
                request.PhysicalLun != 0 ||
                request.Direction != BrokerDirection.In ||
                request.TransferLength != 2 ||
                request.TimeoutMilliseconds != MaximumTimeoutMilliseconds ||
                request.SenseAllocationLength != MaximumSenseLength)
            {
                throw new BrokerProtocolException(
                    "ScannerReady is outside the exact captured operational " +
                    "two-byte data-in form.");
            }
        }

        public static void ValidateCapturedOperationalFaultPixelReadBuffer(
            BrokerRequest request)
        {
            ValidateRequestEnvelope(request);
            byte[] cdb = request.Cdb;
            byte[] expected = new byte[]
            {
                0x3C, 0x00, 0xE0, 0x00, 0x04,
                0x00, 0x00, 0x00, 0x16, 0x00
            };
            bool exactCdb = cdb.Length == expected.Length;
            if (exactCdb)
            {
                for (int index = 0; index < expected.Length; index++)
                {
                    if (cdb[index] != expected[index])
                    {
                        exactCdb = false;
                        break;
                    }
                }
            }
            if (!exactCdb || request.PhysicalTargetId != 5 ||
                request.PhysicalLun != 0 ||
                request.Direction != BrokerDirection.In ||
                request.TransferLength != 22 ||
                request.TimeoutMilliseconds != MaximumTimeoutMilliseconds ||
                request.SenseAllocationLength != MaximumSenseLength)
            {
                throw new BrokerProtocolException(
                    "Fault-pixel READ BUFFER is outside the exact captured " +
                    "operational 22-byte data-in form.");
            }
        }

        public static void ValidateCapturedOperationalFaultPixelDataReadBuffer(
            BrokerRequest request)
        {
            ValidateRequestEnvelope(request);
            byte[] cdb = request.Cdb;
            byte[] expected = new byte[]
            {
                0x3C, 0x00, 0xE0, 0x00, 0x04,
                0x00, 0x00, 0x00, 0x3A, 0x00
            };
            bool exactCdb = cdb.Length == expected.Length;
            if (exactCdb)
            {
                for (int index = 0; index < expected.Length; index++)
                {
                    if (cdb[index] != expected[index])
                    {
                        exactCdb = false;
                        break;
                    }
                }
            }
            if (!exactCdb || request.PhysicalTargetId != 5 ||
                request.PhysicalLun != 0 ||
                request.Direction != BrokerDirection.In ||
                request.TransferLength != 58 ||
                request.TimeoutMilliseconds != MaximumTimeoutMilliseconds ||
                request.SenseAllocationLength != MaximumSenseLength)
            {
                throw new BrokerProtocolException(
                    "Fault-pixel data READ BUFFER is outside the exact " +
                    "captured operational 58-byte data-in form.");
            }
        }

        public static void ValidateCapturedOperationalCalibrationReadBuffer(
            BrokerRequest request)
        {
            ValidateRequestEnvelope(request);
            byte[] cdb = request.Cdb;
            byte[] expected = new byte[]
            {
                0x3C, 0x00, 0xE0, 0x00, 0x00,
                0x00, 0x00, 0x04, 0x00, 0x00
            };
            bool exactCdb = cdb.Length == expected.Length;
            if (exactCdb)
            {
                for (int index = 0; index < expected.Length; index++)
                {
                    if (cdb[index] != expected[index])
                    {
                        exactCdb = false;
                        break;
                    }
                }
            }
            if (!exactCdb || request.PhysicalTargetId != 5 ||
                request.PhysicalLun != 0 ||
                request.Direction != BrokerDirection.In ||
                request.TransferLength != 1024 ||
                request.TimeoutMilliseconds != MaximumTimeoutMilliseconds ||
                request.SenseAllocationLength != MaximumSenseLength)
            {
                throw new BrokerProtocolException(
                    "Calibration READ BUFFER is outside the exact captured " +
                    "operational 1,024-byte data-in form.");
            }
        }

        public static void ValidateCapturedOperationalD8Offset55ReadBuffer(
            BrokerRequest request)
        {
            ValidateRequestEnvelope(request);
            byte[] cdb = request.Cdb;
            byte[] expected = new byte[]
            {
                0x3C, 0x00, 0xD8, 0x00, 0x00,
                0x37, 0x00, 0x00, 0x0A, 0x00
            };
            bool exactCdb = cdb.Length == expected.Length;
            if (exactCdb)
            {
                for (int index = 0; index < expected.Length; index++)
                {
                    if (cdb[index] != expected[index])
                    {
                        exactCdb = false;
                        break;
                    }
                }
            }
            if (!exactCdb || request.PhysicalTargetId != 5 ||
                request.PhysicalLun != 0 ||
                request.Direction != BrokerDirection.In ||
                request.TransferLength != 10 ||
                request.TimeoutMilliseconds != MaximumTimeoutMilliseconds ||
                request.SenseAllocationLength != MaximumSenseLength)
            {
                throw new BrokerProtocolException(
                    "D8 offset-55 READ BUFFER is outside the exact captured " +
                    "operational ten-byte data-in form.");
            }
        }

        public static void ValidateCapturedOperationalPreviewSetWindow(
            BrokerRequest request)
        {
            ValidateCapturedOperationalPreviewSetWindow(request,
                PrecisionTwoPreviewSetWindowSha256);
        }

        public static void ValidateCapturedOperationalPreviewSetWindow(
            BrokerRequest request, string expectedPayloadSha256)
        {
            ValidateRequestEnvelope(request);
            if (expectedPayloadSha256 == null ||
                expectedPayloadSha256.Length != 64)
            {
                throw new ArgumentException(
                    "An exact SHA-256 is required.",
                    "expectedPayloadSha256");
            }

            byte[] cdb = request.Cdb;
            byte[] expected = new byte[]
            {
                0x24, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x54, 0x00
            };
            bool exactCdb = cdb.Length == expected.Length;
            if (exactCdb)
            {
                for (int index = 0; index < expected.Length; index++)
                {
                    if (cdb[index] != expected[index])
                    {
                        exactCdb = false;
                        break;
                    }
                }
            }

            byte[] data = request.Data;
            bool exactHeader = data.Length == 84 && data[0] == 0 &&
                data[1] == 0 && data[2] == 0 && data[3] == 0 &&
                data[4] == 0 && data[5] == 0 && data[6] == 0 &&
                data[7] == 0x4C;
            if (!exactCdb || !exactHeader ||
                request.PhysicalTargetId != 5 ||
                request.PhysicalLun != 0 ||
                request.Direction != BrokerDirection.Out ||
                request.TransferLength != 84 ||
                request.TimeoutMilliseconds != MaximumTimeoutMilliseconds ||
                request.SenseAllocationLength != MaximumSenseLength ||
                !string.Equals(Sha256Hex(data), expectedPayloadSha256,
                    StringComparison.Ordinal))
            {
                throw new BrokerProtocolException(
                    "SET WINDOW is outside the exact captured Preview form.");
            }
        }

        public static uint ValidatePredictedOperationalPreviewImageRead(
            BrokerRequest request)
        {
            ValidateRequestEnvelope(request);
            byte[] cdb = request.Cdb;
            bool exactShape = cdb.Length == 10 && cdb[0] == 0x28 &&
                cdb[1] == 0 && cdb[2] == 0 && cdb[3] == 0 &&
                cdb[4] == 0x28 && cdb[5] == 0 && cdb[9] == 0;
            uint encodedLength = exactShape ?
                ((uint)cdb[6] << 16) | ((uint)cdb[7] << 8) | cdb[8] : 0;
            if (!exactShape || request.PhysicalTargetId != 5 ||
                request.PhysicalLun != 0 ||
                request.Direction != BrokerDirection.In ||
                request.TransferLength == 0 ||
                request.TransferLength != encodedLength ||
                request.TransferLength % 6 != 0 ||
                request.TimeoutMilliseconds != MaximumTimeoutMilliseconds ||
                request.SenseAllocationLength != MaximumSenseLength)
            {
                throw new BrokerProtocolException(
                    "Image READ(10) is outside the observed Precision II " +
                    "post-SET-WINDOW one-row form.");
            }
            return request.TransferLength / 6;
        }

        public static void ValidateCapturedLoaderWriteBufferModeOneZero(
            BrokerRequest request)
        {
            ValidateRequestEnvelope(request);
            byte[] cdb = request.Cdb;
            byte[] expected = new byte[]
            {
                0x3B, 0x01, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00
            };
            bool exactCdb = cdb.Length == expected.Length;
            if (exactCdb)
            {
                for (int index = 0; index < expected.Length; index++)
                {
                    if (cdb[index] != expected[index])
                    {
                        exactCdb = false;
                        break;
                    }
                }
            }
            if (!exactCdb || request.PhysicalTargetId != 5 ||
                request.PhysicalLun != 0 ||
                request.Direction != BrokerDirection.Out ||
                request.TransferLength != 0 ||
                request.TimeoutMilliseconds != MaximumTimeoutMilliseconds ||
                request.SenseAllocationLength != MaximumSenseLength)
            {
                throw new BrokerProtocolException(
                    "WRITE BUFFER mode 1 is outside the exact captured " +
                    "zero-parameter loader form.");
            }
        }

        internal static void ValidateRequestEnvelope(BrokerRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            if (request.RequestId == 0 || request.Generation == 0)
            {
                throw new BrokerProtocolException(
                    "Request ID and generation must be nonzero.");
            }
            if (request.PathId != 0 || request.TargetId != 0 ||
                request.Lun != 0 || request.PhysicalTargetId > 6 ||
                request.PhysicalLun != 0)
            {
                throw new BrokerProtocolException(
                    "Request address is outside the initial device map.");
            }
            if (!Enum.IsDefined(typeof(BrokerDirection), request.Direction))
            {
                throw new BrokerProtocolException(
                    "Unknown data direction.");
            }
            if (request.TransferLength > MaximumTransferLength ||
                request.TimeoutMilliseconds == 0 ||
                request.TimeoutMilliseconds > MaximumTimeoutMilliseconds ||
                request.SenseAllocationLength > MaximumSenseLength)
            {
                throw new BrokerProtocolException(
                    "Request length, timeout, or sense allocation is invalid.");
            }
            if (request.Direction == BrokerDirection.None &&
                request.TransferLength != 0)
            {
                throw new BrokerProtocolException(
                    "A no-data request has a nonzero transfer length.");
            }
            byte[] data = request.Data;
            if (data.Length > MaximumTransferLength ||
                (request.Direction == BrokerDirection.Out &&
                    data.Length != request.TransferLength) ||
                (request.Direction != BrokerDirection.Out && data.Length != 0))
            {
                throw new BrokerProtocolException(
                    "Request payload does not match the data direction " +
                    "and transfer length.");
            }
            byte[] cdb = request.Cdb;
            if (cdb.Length == 0 || cdb.Length > MaximumCdbLength)
            {
                throw new BrokerProtocolException("Invalid CDB length.");
            }
        }

        private static string Sha256Hex(byte[] bytes)
        {
            byte[] hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(bytes);
            }
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int index = 0; index < hash.Length; index++)
            {
                builder.Append(hash[index].ToString("X2"));
            }
            return builder.ToString();
        }

        private static void ValidateAdapterAndScsiStatus(byte adapterStatus,
            byte scsiStatus, int senseLength)
        {
            if (adapterStatus == AdapterSuccess && scsiStatus == 0 &&
                senseLength == 0)
            {
                return;
            }
            if (adapterStatus == AdapterCheckCondition && scsiStatus == 0x02)
            {
                return;
            }
            if (adapterStatus == AdapterBusy && scsiStatus == 0x08 &&
                senseLength == 0)
            {
                return;
            }
            if (adapterStatus == AdapterSelectionTimeout && scsiStatus == 0 &&
                senseLength == 0)
            {
                return;
            }
            throw new BrokerProtocolException(
                "Adapter, SCSI, and sense status fields are inconsistent.");
        }

        private static void ValidateAdapterState(BrokerAdapterState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }
            if (state.Generation == 0 ||
                !Enum.IsDefined(typeof(BrokerAdapterStateKind), state.State))
            {
                throw new BrokerProtocolException(
                    "Adapter state or generation is invalid.");
            }
            byte[] inquiry = state.Inquiry;
            if (state.State == BrokerAdapterStateKind.Offline)
            {
                if (state.PhysicalTargetId != 0 || state.PhysicalLun != 0 ||
                    state.PeripheralDeviceType != 0 || inquiry.Length != 0)
                {
                    throw new BrokerProtocolException(
                        "Offline state contains device identity data.");
                }
                return;
            }
            if (state.PhysicalTargetId > 6 || state.PhysicalLun != 0 ||
                state.PeripheralDeviceType != 0x06 ||
                inquiry.Length != InquiryLength ||
                (inquiry[0] & 0x1F) != state.PeripheralDeviceType)
            {
                throw new BrokerProtocolException(
                    "Online state is not a verified type-6 scanner identity.");
            }
        }

        private static void WriteHeader(byte[] bytes, BrokerMessageType type,
            int messageSize, ulong requestId, ulong generation)
        {
            WriteUInt32(bytes, 0, Magic);
            WriteUInt16(bytes, 4, VersionMajor);
            WriteUInt16(bytes, 6, VersionMinor);
            WriteUInt16(bytes, 8, (ushort)type);
            WriteUInt16(bytes, 10, HeaderSize);
            WriteUInt32(bytes, 12, checked((uint)messageSize));
            WriteUInt64(bytes, 16, requestId);
            WriteUInt64(bytes, 24, generation);
        }

        private static void ValidateHeader(byte[] bytes,
            BrokerMessageType expectedType, int minimumSize, int maximumSize)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }
            if (bytes.Length < minimumSize || bytes.Length > maximumSize)
            {
                throw new BrokerProtocolException("Message size is invalid.");
            }
            if (ReadUInt32(bytes, 0) != Magic ||
                ReadUInt16(bytes, 4) != VersionMajor ||
                ReadUInt16(bytes, 6) != VersionMinor ||
                ReadUInt16(bytes, 8) != (ushort)expectedType ||
                ReadUInt16(bytes, 10) != HeaderSize ||
                ReadUInt32(bytes, 12) != bytes.Length)
            {
                throw new BrokerProtocolException(
                    "Message header is invalid or incompatible.");
            }
            if (ReadUInt64(bytes, 24) == 0)
            {
                throw new BrokerProtocolException(
                    "Message generation must be nonzero.");
            }
        }

        private static void RequireZero(byte[] bytes, int offset, int length,
            string field)
        {
            for (int i = 0; i < length; i++)
            {
                if (bytes[offset + i] != 0)
                {
                    throw new BrokerProtocolException(field + " must be zero.");
                }
            }
        }

        private static byte[] Copy(byte[] bytes, int offset, int length)
        {
            byte[] result = new byte[length];
            Buffer.BlockCopy(bytes, offset, result, 0, length);
            return result;
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return unchecked((ushort)(bytes[offset] |
                (bytes[offset + 1] << 8)));
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

        private static ulong ReadUInt64(byte[] bytes, int offset)
        {
            return ReadUInt32(bytes, offset) |
                ((ulong)ReadUInt32(bytes, offset + 4) << 32);
        }

        private static void WriteUInt16(byte[] bytes, int offset, ushort value)
        {
            bytes[offset] = unchecked((byte)value);
            bytes[offset + 1] = unchecked((byte)(value >> 8));
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = unchecked((byte)value);
            bytes[offset + 1] = unchecked((byte)(value >> 8));
            bytes[offset + 2] = unchecked((byte)(value >> 16));
            bytes[offset + 3] = unchecked((byte)(value >> 24));
        }

        private static void WriteUInt64(byte[] bytes, int offset, ulong value)
        {
            WriteUInt32(bytes, offset, unchecked((uint)value));
            WriteUInt32(bytes, offset + 4,
                unchecked((uint)(value >> 32)));
        }
    }
}
