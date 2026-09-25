// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Usb2Xchange.BrokerProtocol
{
    public enum PrecisionTwoLoaderSequenceState
    {
        Ready = 0,
        Records = 1,
        Complete = 2,
        Failed = 3
    }

    public enum PrecisionTwoLoaderSequenceStepKind
    {
        FirmwareRecord = 0,
        TerminalRecord = 1
    }

    public sealed class PrecisionTwoLoaderSequenceStep
    {
        internal PrecisionTwoLoaderSequenceStep(
            PrecisionTwoLoaderSequenceStepKind kind, int recordIndex,
            ushort addressLikeValue, string sha256)
        {
            Kind = kind;
            RecordIndex = recordIndex;
            AddressLikeValue = addressLikeValue;
            Sha256 = sha256;
        }

        public PrecisionTwoLoaderSequenceStepKind Kind { get; private set; }
        public int RecordIndex { get; private set; }
        public ushort AddressLikeValue { get; private set; }
        public string Sha256 { get; private set; }
    }

    public sealed class PrecisionTwoLoaderSequenceManifest
    {
        public const int FirmwareLength = 65536;
        public const int RecordCount = 16;
        public const int RecordHeaderLength = 10;
        public const int RecordPayloadLength = 4096;
        public const int RecordLength =
            RecordHeaderLength + RecordPayloadLength;
        public const int TerminalRecordLength = 10;

        private readonly byte[] firmwareSha256;
        private readonly byte[][] recordSha256;
        private readonly byte[] terminalRecordSha256;

        private PrecisionTwoLoaderSequenceManifest(byte[] firmwareSha256,
            byte[][] recordSha256, byte[] terminalRecordSha256)
        {
            this.firmwareSha256 = Clone(firmwareSha256);
            this.recordSha256 = new byte[recordSha256.Length][];
            for (int index = 0; index < recordSha256.Length; index++)
            {
                this.recordSha256[index] = Clone(recordSha256[index]);
            }
            this.terminalRecordSha256 = Clone(terminalRecordSha256);
        }

        public string FirmwareSha256
        {
            get { return ToHex(firmwareSha256); }
        }

        public string TerminalRecordSha256
        {
            get { return ToHex(terminalRecordSha256); }
        }

        public string GetRecordSha256(int recordIndex)
        {
            if (recordIndex < 0 || recordIndex >= RecordCount)
            {
                throw new ArgumentOutOfRangeException("recordIndex");
            }
            return ToHex(recordSha256[recordIndex]);
        }

        public static PrecisionTwoLoaderSequenceManifest Create(
            byte[] firmwareImage, string expectedFirmwareSha256)
        {
            if (firmwareImage == null)
            {
                throw new ArgumentNullException("firmwareImage");
            }
            if (firmwareImage.Length != FirmwareLength)
            {
                throw new BrokerProtocolException(string.Format(
                    "Precision II firmware must contain exactly {0} bytes.",
                    FirmwareLength));
            }

            byte[] expectedHash = ParseSha256(expectedFirmwareSha256);
            byte[] actualHash = ComputeSha256(firmwareImage);
            if (!FixedTimeEquals(expectedHash, actualHash))
            {
                throw new BrokerProtocolException(string.Format(
                    "Firmware SHA-256 {0} does not match required {1}.",
                    ToHex(actualHash), ToHex(expectedHash)));
            }

            byte[][] recordHashes = new byte[RecordCount][];
            for (int index = 0; index < RecordCount; index++)
            {
                byte[] record = BuildRecord(firmwareImage, index);
                try
                {
                    recordHashes[index] = ComputeSha256(record);
                }
                finally
                {
                    Array.Clear(record, 0, record.Length);
                }
            }
            byte[] terminalHash = ComputeSha256(BuildTerminalRecord());
            return new PrecisionTwoLoaderSequenceManifest(actualHash,
                recordHashes, terminalHash);
        }

        internal byte[] GetRecordHash(int recordIndex)
        {
            return Clone(recordSha256[recordIndex]);
        }

        internal byte[] GetTerminalRecordHash()
        {
            return Clone(terminalRecordSha256);
        }

        internal static byte[] BuildRecord(byte[] firmwareImage,
            int recordIndex)
        {
            if (recordIndex < 0 || recordIndex >= RecordCount)
            {
                throw new ArgumentOutOfRangeException("recordIndex");
            }
            ushort address = checked((ushort)(0x3000 + (recordIndex * 0x100)));
            byte[] record = new byte[RecordLength];
            record[0] = 0x01;
            record[1] = (byte)(address >> 8);
            record[2] = (byte)(address & 0xFF);
            Buffer.BlockCopy(firmwareImage,
                recordIndex * RecordPayloadLength, record,
                RecordHeaderLength, RecordPayloadLength);
            return record;
        }

        internal static byte[] BuildTerminalRecord()
        {
            return new byte[]
            {
                0x00, 0x30, 0x01, 0x00, 0x0E,
                0x00, 0x00, 0x00, 0x00, 0x00
            };
        }

        internal static byte[] ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(bytes);
            }
        }

        internal static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }
            int difference = 0;
            for (int index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }
            return difference == 0;
        }

        internal static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        private static byte[] ParseSha256(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("expectedFirmwareSha256");
            }
            if (value.Length != 64)
            {
                throw new BrokerProtocolException(
                    "Expected firmware SHA-256 must contain 64 hex digits.");
            }
            byte[] bytes = new byte[32];
            for (int index = 0; index < bytes.Length; index++)
            {
                int high = HexValue(value[index * 2]);
                int low = HexValue(value[(index * 2) + 1]);
                if (high < 0 || low < 0)
                {
                    throw new BrokerProtocolException(
                        "Expected firmware SHA-256 contains a non-hex digit.");
                }
                bytes[index] = (byte)((high << 4) | low);
            }
            return bytes;
        }

        private static int HexValue(char value)
        {
            if (value >= '0' && value <= '9')
            {
                return value - '0';
            }
            if (value >= 'A' && value <= 'F')
            {
                return value - 'A' + 10;
            }
            if (value >= 'a' && value <= 'f')
            {
                return value - 'a' + 10;
            }
            return -1;
        }

        private static byte[] Clone(byte[] bytes)
        {
            return (byte[])bytes.Clone();
        }
    }

    public sealed class PrecisionTwoLoaderSequenceValidator
    {
        public const long MaximumCompletionWaitMilliseconds = 60000;
        public const long MaximumSequenceDurationMilliseconds =
            (PrecisionTwoLoaderSequenceManifest.RecordCount + 1) *
            MaximumCompletionWaitMilliseconds;

        private readonly PrecisionTwoLoaderSequenceManifest manifest;
        private readonly Func<long> monotonicMilliseconds;
        private PrecisionTwoLoaderSequenceState state;
        private int nextRecordIndex;
        private ulong generation;
        private ulong lastRequestId;
        private BrokerRequest pendingCompletionRequest;
        private PrecisionTwoLoaderSequenceStep pendingStep;
        private bool hasObservedTime;
        private bool sequenceStarted;
        private long lastObservedMilliseconds;
        private long sequenceStartedMilliseconds;
        private long pendingStartedMilliseconds;

        public PrecisionTwoLoaderSequenceValidator(
            PrecisionTwoLoaderSequenceManifest manifest)
            : this(manifest, GetMonotonicMilliseconds)
        {
        }

        public PrecisionTwoLoaderSequenceValidator(
            PrecisionTwoLoaderSequenceManifest manifest,
            Func<long> monotonicMilliseconds)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException("manifest");
            }
            if (monotonicMilliseconds == null)
            {
                throw new ArgumentNullException("monotonicMilliseconds");
            }
            this.manifest = manifest;
            this.monotonicMilliseconds = monotonicMilliseconds;
            state = PrecisionTwoLoaderSequenceState.Ready;
        }

        public PrecisionTwoLoaderSequenceState State
        {
            get { return state; }
        }

        public int AcceptedRecordCount
        {
            get { return nextRecordIndex; }
        }

        public ulong Generation
        {
            get { return generation; }
        }

        public bool CompletionPending
        {
            get { return pendingCompletionRequest != null; }
        }

        public PrecisionTwoLoaderSequenceStep ValidateNext(
            BrokerRequest request)
        {
            if (state == PrecisionTwoLoaderSequenceState.Failed)
            {
                throw new BrokerProtocolException(
                    "The loader sequence is already in the failed state.");
            }
            if (state == PrecisionTwoLoaderSequenceState.Complete)
            {
                return Fail("The loader sequence is already complete.");
            }
            if (pendingCompletionRequest != null)
            {
                return Fail("The previous loader request has no completion.");
            }
            if (request == null)
            {
                return Fail("Loader sequence request cannot be null.");
            }

            try
            {
                long now = ObserveTime();
                if (!sequenceStarted)
                {
                    sequenceStarted = true;
                    sequenceStartedMilliseconds = now;
                }
                ValidateSequenceDeadline(now);
                BrokerWireProtocol.ValidateRequestEnvelope(request);
                ValidateCommonRequest(request);
                PrecisionTwoLoaderSequenceStep step;
                if (nextRecordIndex <
                    PrecisionTwoLoaderSequenceManifest.RecordCount)
                {
                    step = ValidateFirmwareRecord(request);
                }
                else
                {
                    step = ValidateTerminalRecord(request);
                }

                pendingCompletionRequest = BuildCompletionEnvelope(request);
                pendingStep = step;
                pendingStartedMilliseconds = now;
                lastRequestId = request.RequestId;
                state = PrecisionTwoLoaderSequenceState.Records;
                return step;
            }
            catch (BrokerProtocolException)
            {
                SetFailed();
                throw;
            }
        }

        public PrecisionTwoLoaderSequenceStep ValidateCompletion(
            BrokerCompletion completion)
        {
            if (state == PrecisionTwoLoaderSequenceState.Failed)
            {
                throw new BrokerProtocolException(
                    "The loader sequence is already in the failed state.");
            }
            if (state == PrecisionTwoLoaderSequenceState.Complete)
            {
                return Fail("The loader sequence is already complete.");
            }
            if (pendingCompletionRequest == null || pendingStep == null)
            {
                return Fail("No loader request is awaiting completion.");
            }
            if (completion == null)
            {
                return Fail("Loader sequence completion cannot be null.");
            }

            try
            {
                long now = ObserveTime();
                ValidateSequenceDeadline(now);
                if (now - pendingStartedMilliseconds >
                    MaximumCompletionWaitMilliseconds)
                {
                    throw new BrokerProtocolException(
                        "Loader request completion deadline expired.");
                }
                BrokerWireProtocol.ValidateCompletion(
                    pendingCompletionRequest, completion);
                if (completion.TransportStatus !=
                        BrokerTransportStatus.Success ||
                    completion.AdapterStatus != 0 ||
                    completion.ScsiStatus != 0 ||
                    completion.TransferLength !=
                        pendingCompletionRequest.TransferLength ||
                    completion.Residue != 0 ||
                    completion.Sense.Length != 0 ||
                    completion.Data.Length != 0)
                {
                    throw new BrokerProtocolException(
                        "Loader completion is not an exact successful " +
                        "data-out completion.");
                }

                PrecisionTwoLoaderSequenceStep completedStep = pendingStep;
                pendingCompletionRequest = null;
                pendingStep = null;
                if (completedStep.Kind ==
                    PrecisionTwoLoaderSequenceStepKind.FirmwareRecord)
                {
                    nextRecordIndex++;
                    state = PrecisionTwoLoaderSequenceState.Records;
                }
                else
                {
                    state = PrecisionTwoLoaderSequenceState.Complete;
                }
                return completedStep;
            }
            catch (BrokerProtocolException)
            {
                SetFailed();
                throw;
            }
        }

        public void FailClosed()
        {
            SetFailed();
        }

        private void ValidateCommonRequest(BrokerRequest request)
        {
            if (request.PathId != 0 || request.TargetId != 0 ||
                request.Lun != 0 || request.PhysicalTargetId != 5 ||
                request.PhysicalLun != 0 ||
                request.Direction != BrokerDirection.Out ||
                request.TimeoutMilliseconds !=
                    BrokerWireProtocol.MaximumTimeoutMilliseconds ||
                request.SenseAllocationLength !=
                    BrokerWireProtocol.MaximumSenseLength)
            {
                throw new BrokerProtocolException(
                    "Loader sequence request is outside the exact target, " +
                    "direction, timeout, or sense envelope.");
            }
            if (generation == 0)
            {
                generation = request.Generation;
            }
            else if (request.Generation != generation)
            {
                throw new BrokerProtocolException(
                    "Loader sequence generation changed mid-sequence.");
            }
            if (request.RequestId <= lastRequestId)
            {
                throw new BrokerProtocolException(
                    "Loader sequence request IDs must increase strictly.");
            }
        }

        private PrecisionTwoLoaderSequenceStep ValidateFirmwareRecord(
            BrokerRequest request)
        {
            int recordIndex = nextRecordIndex;
            ushort address = checked((ushort)(0x3000 + (recordIndex * 0x100)));
            byte[] data = request.Data;
            if (request.TransferLength !=
                    PrecisionTwoLoaderSequenceManifest.RecordLength ||
                data.Length !=
                    PrecisionTwoLoaderSequenceManifest.RecordLength ||
                !HasExactWriteBufferCdb(request.Cdb,
                    PrecisionTwoLoaderSequenceManifest.RecordLength) ||
                data[0] != 0x01 || data[1] != (byte)(address >> 8) ||
                data[2] != (byte)(address & 0xFF) || data[3] != 0 ||
                data[4] != 0 ||
                data[5] != 0 || data[6] != 0 || data[7] != 0 ||
                data[8] != 0 || data[9] != 0)
            {
                throw new BrokerProtocolException(string.Format(
                    "Loader firmware record {0} has an invalid CDB, length, " +
                    "or ten-byte header.", recordIndex));
            }

            byte[] hash = PrecisionTwoLoaderSequenceManifest.
                ComputeSha256(data);
            if (!PrecisionTwoLoaderSequenceManifest.FixedTimeEquals(hash,
                    manifest.GetRecordHash(recordIndex)))
            {
                throw new BrokerProtocolException(string.Format(
                    "Loader firmware record {0} SHA-256 does not match the " +
                    "verified manifest.", recordIndex));
            }

            return new PrecisionTwoLoaderSequenceStep(
                PrecisionTwoLoaderSequenceStepKind.FirmwareRecord,
                recordIndex, address,
                PrecisionTwoLoaderSequenceManifest.ToHex(hash));
        }

        private PrecisionTwoLoaderSequenceStep ValidateTerminalRecord(
            BrokerRequest request)
        {
            byte[] data = request.Data;
            byte[] expected = PrecisionTwoLoaderSequenceManifest.
                BuildTerminalRecord();
            if (request.TransferLength != expected.Length ||
                data.Length != expected.Length ||
                !HasExactWriteBufferCdb(request.Cdb, expected.Length) ||
                !PrecisionTwoLoaderSequenceManifest.FixedTimeEquals(
                    data, expected))
            {
                throw new BrokerProtocolException(
                    "Loader terminal record is outside the exact captured form.");
            }
            byte[] hash = PrecisionTwoLoaderSequenceManifest.
                ComputeSha256(data);
            if (!PrecisionTwoLoaderSequenceManifest.FixedTimeEquals(hash,
                    manifest.GetTerminalRecordHash()))
            {
                throw new BrokerProtocolException(
                    "Loader terminal record SHA-256 does not match the manifest.");
            }

            return new PrecisionTwoLoaderSequenceStep(
                PrecisionTwoLoaderSequenceStepKind.TerminalRecord, -1,
                0x3001, PrecisionTwoLoaderSequenceManifest.ToHex(hash));
        }

        private static BrokerRequest BuildCompletionEnvelope(
            BrokerRequest request)
        {
            return new BrokerRequest(request.RequestId, request.Generation,
                request.PathId, request.TargetId, request.Lun,
                request.PhysicalTargetId, request.PhysicalLun,
                request.Direction, request.TransferLength,
                request.TimeoutMilliseconds, request.SenseAllocationLength,
                request.Cdb, new byte[checked((int)request.TransferLength)]);
        }

        private long ObserveTime()
        {
            long now = monotonicMilliseconds();
            if (now < 0 ||
                (hasObservedTime && now < lastObservedMilliseconds))
            {
                throw new BrokerProtocolException(
                    "Loader sequence clock is not monotonic.");
            }
            hasObservedTime = true;
            lastObservedMilliseconds = now;
            return now;
        }

        private static long GetMonotonicMilliseconds()
        {
            long timestamp = Stopwatch.GetTimestamp();
            long wholeSeconds = timestamp / Stopwatch.Frequency;
            long remainder = timestamp % Stopwatch.Frequency;
            return checked((wholeSeconds * 1000) +
                ((remainder * 1000) / Stopwatch.Frequency));
        }

        private void ValidateSequenceDeadline(long now)
        {
            if (sequenceStarted &&
                now - sequenceStartedMilliseconds >
                    MaximumSequenceDurationMilliseconds)
            {
                throw new BrokerProtocolException(
                    "Loader sequence deadline expired.");
            }
        }

        private static bool HasExactWriteBufferCdb(byte[] cdb,
            int parameterListLength)
        {
            return cdb.Length == 10 && cdb[0] == 0x3B && cdb[1] == 0x01 &&
                cdb[2] == 0 && cdb[3] == 0 && cdb[4] == 0 && cdb[5] == 0 &&
                cdb[6] == (byte)((parameterListLength >> 16) & 0xFF) &&
                cdb[7] == (byte)((parameterListLength >> 8) & 0xFF) &&
                cdb[8] == (byte)(parameterListLength & 0xFF) && cdb[9] == 0;
        }

        private PrecisionTwoLoaderSequenceStep Fail(string message)
        {
            SetFailed();
            throw new BrokerProtocolException(message);
        }

        private void SetFailed()
        {
            pendingCompletionRequest = null;
            pendingStep = null;
            state = PrecisionTwoLoaderSequenceState.Failed;
        }
    }
}
