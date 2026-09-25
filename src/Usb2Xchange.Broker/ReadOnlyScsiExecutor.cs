// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using Usb2Xchange.BrokerProtocol;
using Usb2Xchange.Protocol;

namespace Usb2Xchange.Broker
{
    internal interface IReadOnlyScsiTransport
    {
        RawScsiResult Execute(byte target, byte lun, byte[] cdb,
            uint requestedLength, int timeoutMilliseconds);

        RawScsiResult ExecutePrecisionTwoLoaderReadBufferD8Once();

        RawScsiResult ExecutePrecisionTwoOperationalReadBufferD8Once();

        RawScsiResult ExecutePrecisionTwoOperationalScannerReadyOnce();

        RawScsiResult
            ExecutePrecisionTwoOperationalFaultPixelReadBufferOnce();

        RawScsiResult
            ExecutePrecisionTwoOperationalFaultPixelDataReadBufferOnce();

        RawScsiResult
            ExecutePrecisionTwoOperationalCalibrationReadBufferOnce();

        RawScsiResult
            ExecutePrecisionTwoOperationalD8Offset55ReadBufferOnce();

        RawScsiResult ExecutePrecisionTwoOperationalPreviewSetWindowOnce(
            byte[] data);

        RawScsiResult ExecutePrecisionTwoLoaderWriteBufferModeOneZeroOnce();

        RawScsiResult ExecutePrecisionTwoLoaderWriteBufferRecordOnce(
            byte[] record, int recordIndex);

        RawScsiResult ExecutePrecisionTwoLoaderWriteBufferTerminalOnce(
            byte[] record);
    }

    internal enum ScannerIdentityRequirement
    {
        KnownPrecisionTwo = 0,
        Loader = 1,
        Operational = 2
    }

    internal static class ExperimentSafety
    {
        internal static void ValidateD8AndLoaderWriteCombination(
            bool loaderWriteEnabled, bool loaderD8Enabled,
            bool operationalD8Enabled)
        {
            if (loaderD8Enabled && operationalD8Enabled)
            {
                throw new ArgumentException(
                    "Loader and operational D8 gates cannot be enabled " +
                    "together.");
            }
            if (loaderWriteEnabled && !loaderD8Enabled)
            {
                throw new ArgumentException(
                    "A loader WRITE BUFFER experiment requires the " +
                    "loader-only preceding D8 gate.");
            }
        }
    }

    internal sealed class RawScsiResult
    {
        private readonly byte[] data;

        internal RawScsiResult(byte adapterStatus, uint requestedLength,
            uint residue, byte[] data)
            : this(adapterStatus, requestedLength, residue, data, true)
        {
        }

        private RawScsiResult(byte adapterStatus, uint requestedLength,
            uint residue, byte[] data, bool dataIn)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            if (residue > requestedLength ||
                (dataIn && data.Length != requestedLength - residue) ||
                (!dataIn && data.Length != 0))
            {
                throw new ArgumentException(
                    "Raw SCSI data length and residue are inconsistent.");
            }
            AdapterStatus = adapterStatus;
            RequestedLength = requestedLength;
            Residue = residue;
            this.data = (byte[])data.Clone();
        }

        internal static RawScsiResult ForDataOut(byte adapterStatus,
            uint requestedLength, uint residue)
        {
            return new RawScsiResult(adapterStatus, requestedLength, residue,
                new byte[0], false);
        }

        internal byte AdapterStatus { get; private set; }
        internal uint RequestedLength { get; private set; }
        internal uint Residue { get; private set; }
        internal uint TransferLength { get { return RequestedLength - Residue; } }
        internal byte[] Data { get { return (byte[])data.Clone(); } }
    }

    internal static class ScannerIdentity
    {
        internal const byte PhysicalTargetId = 5;
        internal const byte PhysicalLun = 0;

        internal static byte[] RequireKnownPrecisionTwo(byte[] inquiry)
        {
            InquiryData parsed = InquiryData.Parse(inquiry);
            if (inquiry.Length != BrokerWireProtocol.InquiryLength ||
                parsed.PeripheralQualifier != 0 ||
                parsed.PeripheralDeviceType != 0x06 ||
                !string.Equals(parsed.Vendor, "Imacon",
                    StringComparison.Ordinal) ||
                (!IsPrecisionTwoLoader(parsed) &&
                    !IsPrecisionTwoOperational(parsed)))
            {
                throw new ProtocolException(string.Format(
                    "Refusing unexpected target-5 identity: type={0:X2}, " +
                    "vendor='{1}', product='{2}', revision='{3}'.",
                    parsed.PeripheralDeviceType, parsed.Vendor,
                    parsed.Product, parsed.Revision));
            }
            return (byte[])inquiry.Clone();
        }

        internal static byte[] RequireKnownPrecisionTwoLoader(byte[] inquiry)
        {
            byte[] accepted = RequireKnownPrecisionTwo(inquiry);
            InquiryData parsed = InquiryData.Parse(accepted);
            if (!IsPrecisionTwoLoader(parsed))
            {
                throw new ProtocolException(string.Format(
                    "A loader experiment requires Imacon / SCSI Loader / " +
                    "L302; target 5 is {0} / {1} / {2}.", parsed.Vendor,
                    parsed.Product, parsed.Revision));
            }
            return accepted;
        }

        internal static byte[] RequireKnownPrecisionTwoOperational(
            byte[] inquiry)
        {
            byte[] accepted = RequireKnownPrecisionTwo(inquiry);
            InquiryData parsed = InquiryData.Parse(accepted);
            if (!IsPrecisionTwoOperational(parsed))
            {
                throw new ProtocolException(string.Format(
                    "An operational experiment requires Imacon / FlexTight " +
                    "II / M333; target 5 is {0} / {1} / {2}.", parsed.Vendor,
                    parsed.Product, parsed.Revision));
            }
            return accepted;
        }

        private static bool IsPrecisionTwoLoader(InquiryData parsed)
        {
            return string.Equals(parsed.Product, "SCSI Loader",
                    StringComparison.Ordinal) &&
                string.Equals(parsed.Revision, "L302",
                    StringComparison.Ordinal);
        }

        private static bool IsPrecisionTwoOperational(InquiryData parsed)
        {
            return string.Equals(parsed.Product, "FlexTight II",
                    StringComparison.Ordinal) &&
                string.Equals(parsed.Revision, "M333",
                    StringComparison.Ordinal);
        }
    }

    internal static class ReadOnlyScsiExecutor
    {
        internal static byte[] ProbeScanner(IReadOnlyScsiTransport transport,
            int timeoutMilliseconds,
            ScannerIdentityRequirement identityRequirement)
        {
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }
            byte[] cdb = ScsiFraming.BuildInquiryCdb(
                BrokerWireProtocol.InquiryLength);
            RawScsiResult result = transport.Execute(
                ScannerIdentity.PhysicalTargetId, ScannerIdentity.PhysicalLun,
                cdb, BrokerWireProtocol.InquiryLength, timeoutMilliseconds);
            if (result.AdapterStatus != (byte)AdapterStatus.Success ||
                result.Residue != 0)
            {
                throw new ProtocolException(string.Format(
                    "Target-5 identity INQUIRY failed: status={0:X2}, residue={1}.",
                    result.AdapterStatus, result.Residue));
            }
            if (identityRequirement == ScannerIdentityRequirement.Loader)
            {
                return ScannerIdentity.RequireKnownPrecisionTwoLoader(
                    result.Data);
            }
            if (identityRequirement == ScannerIdentityRequirement.Operational)
            {
                return ScannerIdentity.RequireKnownPrecisionTwoOperational(
                    result.Data);
            }
            if (identityRequirement !=
                ScannerIdentityRequirement.KnownPrecisionTwo)
            {
                throw new ArgumentOutOfRangeException("identityRequirement");
            }
            return ScannerIdentity.RequireKnownPrecisionTwo(result.Data);
        }

        internal static BrokerCompletion Execute(BrokerRequest request,
            IReadOnlyScsiTransport transport)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }

            // This is the authoritative hardware-execution safety gate. The
            // wire parser intentionally accepts metadata for commands that
            // capture mode must observe and reject without touching WinUSB.
            BrokerWireProtocol.ValidateReadOnlyRequest(request);
            if (request.PhysicalTargetId != ScannerIdentity.PhysicalTargetId ||
                request.PhysicalLun != ScannerIdentity.PhysicalLun)
            {
                throw new BrokerProtocolException(
                    "The live broker accepts only the verified physical target 5, LUN 0.");
            }

            byte[] cdb = request.Cdb;
            if (cdb[0] == 0xA0)
            {
                return SyntheticReportLuns(request);
            }
            RawScsiResult result = transport.Execute(
                request.PhysicalTargetId, request.PhysicalLun, cdb,
                request.TransferLength,
                checked((int)request.TimeoutMilliseconds));
            return BuildCompletion(request, transport, result, true);
        }

        internal static BrokerCompletion ExecuteCapturedLoaderReadBufferD8Once(
            BrokerRequest request, IReadOnlyScsiTransport transport)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }
            BrokerWireProtocol.ValidateCapturedLoaderReadBufferD8(request);
            RawScsiResult result =
                transport.ExecutePrecisionTwoLoaderReadBufferD8Once();
            if (result == null ||
                result.RequestedLength != request.TransferLength)
            {
                throw new ProtocolException(
                    "READ BUFFER D8 returned an inconsistent requested length.");
            }
            return BuildCompletion(request, transport, result, false);
        }

        internal static BrokerCompletion
            ExecuteCapturedOperationalReadBufferD8Once(
                BrokerRequest request, IReadOnlyScsiTransport transport)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }
            BrokerWireProtocol.ValidateCapturedLoaderReadBufferD8(request);
            RawScsiResult result =
                transport.ExecutePrecisionTwoOperationalReadBufferD8Once();
            if (result == null ||
                result.RequestedLength != request.TransferLength)
            {
                throw new ProtocolException(
                    "Operational READ BUFFER D8 returned an inconsistent " +
                    "requested length.");
            }
            return BuildCompletion(request, transport, result, false);
        }

        internal static BrokerCompletion
            ExecuteCapturedOperationalScannerReadyOnce(
                BrokerRequest request, IReadOnlyScsiTransport transport)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }
            BrokerWireProtocol.ValidateCapturedOperationalScannerReady(request);
            RawScsiResult result =
                transport.ExecutePrecisionTwoOperationalScannerReadyOnce();
            if (result == null ||
                result.RequestedLength != request.TransferLength)
            {
                throw new ProtocolException(
                    "Operational ScannerReady returned an inconsistent " +
                    "requested length.");
            }
            return BuildCompletion(request, transport, result, false);
        }

        internal static BrokerCompletion
            ExecuteCapturedOperationalFaultPixelReadBufferOnce(
                BrokerRequest request, IReadOnlyScsiTransport transport)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }
            BrokerWireProtocol.
                ValidateCapturedOperationalFaultPixelReadBuffer(request);
            RawScsiResult result = transport.
                ExecutePrecisionTwoOperationalFaultPixelReadBufferOnce();
            if (result == null ||
                result.RequestedLength != request.TransferLength)
            {
                throw new ProtocolException(
                    "Operational fault-pixel READ BUFFER returned an " +
                    "inconsistent requested length.");
            }
            return BuildCompletion(request, transport, result, false);
        }

        internal static BrokerCompletion
            ExecuteCapturedOperationalFaultPixelDataReadBufferOnce(
                BrokerRequest request, IReadOnlyScsiTransport transport)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }
            BrokerWireProtocol.
                ValidateCapturedOperationalFaultPixelDataReadBuffer(request);
            RawScsiResult result = transport.
                ExecutePrecisionTwoOperationalFaultPixelDataReadBufferOnce();
            if (result == null ||
                result.RequestedLength != request.TransferLength)
            {
                throw new ProtocolException(
                    "Operational fault-pixel data READ BUFFER returned an " +
                    "inconsistent requested length.");
            }
            return BuildCompletion(request, transport, result, false);
        }

        internal static BrokerCompletion
            ExecuteCapturedOperationalCalibrationReadBufferOnce(
                BrokerRequest request, IReadOnlyScsiTransport transport)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }
            BrokerWireProtocol.
                ValidateCapturedOperationalCalibrationReadBuffer(request);
            RawScsiResult result = transport.
                ExecutePrecisionTwoOperationalCalibrationReadBufferOnce();
            if (result == null ||
                result.RequestedLength != request.TransferLength)
            {
                throw new ProtocolException(
                    "Operational calibration READ BUFFER returned an " +
                    "inconsistent requested length.");
            }
            return BuildCompletion(request, transport, result, false);
        }

        internal static BrokerCompletion
            ExecuteCapturedOperationalD8Offset55ReadBufferOnce(
                BrokerRequest request, IReadOnlyScsiTransport transport)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }
            BrokerWireProtocol.
                ValidateCapturedOperationalD8Offset55ReadBuffer(request);
            RawScsiResult result = transport.
                ExecutePrecisionTwoOperationalD8Offset55ReadBufferOnce();
            if (result == null ||
                result.RequestedLength != request.TransferLength)
            {
                throw new ProtocolException(
                    "Operational D8 offset-55 READ BUFFER returned an " +
                    "inconsistent requested length.");
            }
            return BuildCompletion(request, transport, result, false);
        }

        internal static BrokerCompletion
            ExecuteCapturedOperationalPreviewSetWindowOnce(
                BrokerRequest request, IReadOnlyScsiTransport transport)
        {
            return ExecuteCapturedOperationalPreviewSetWindowOnce(request,
                transport,
                BrokerWireProtocol.PrecisionTwoPreviewSetWindowSha256);
        }

        internal static BrokerCompletion
            ExecuteCapturedOperationalPreviewSetWindowOnce(
                BrokerRequest request, IReadOnlyScsiTransport transport,
                string expectedPayloadSha256)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }
            BrokerWireProtocol.ValidateCapturedOperationalPreviewSetWindow(
                request, expectedPayloadSha256);
            RawScsiResult result = transport.
                ExecutePrecisionTwoOperationalPreviewSetWindowOnce(
                    request.Data);
            if (result == null ||
                result.RequestedLength != request.TransferLength ||
                result.Data.Length != 0)
            {
                throw new ProtocolException(
                    "Operational Preview SET WINDOW returned an inconsistent " +
                    "data-out result.");
            }
            return BuildCompletion(request, transport, result, false);
        }

        internal static BrokerCompletion
            ExecuteCapturedLoaderWriteBufferModeOneZeroOnce(
                BrokerRequest request, IReadOnlyScsiTransport transport)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }
            BrokerWireProtocol.ValidateCapturedLoaderWriteBufferModeOneZero(
                request);
            RawScsiResult result = transport.
                ExecutePrecisionTwoLoaderWriteBufferModeOneZeroOnce();
            if (result == null || result.RequestedLength != 0 ||
                result.TransferLength != 0 || result.Data.Length != 0)
            {
                throw new ProtocolException(
                    "Zero-parameter WRITE BUFFER returned data or a nonzero " +
                    "requested length.");
            }
            return BuildCompletion(request, transport, result, false);
        }

        internal static BrokerCompletion
            ExecuteCapturedLoaderWriteBufferFirstRecordOnce(
                BrokerRequest request, IReadOnlyScsiTransport transport,
                PrecisionTwoLoaderSequenceManifest manifest)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }
            if (manifest == null)
            {
                throw new ArgumentNullException("manifest");
            }

            PrecisionTwoLoaderSequenceValidator validator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            BrokerCompletion completion =
                ExecuteCapturedLoaderWriteBufferRecordOnce(request,
                    transport, validator, 1);
            validator.FailClosed();
            return completion;
        }

        internal static BrokerCompletion
            ExecuteCapturedLoaderWriteBufferRecordOnce(
                BrokerRequest request, IReadOnlyScsiTransport transport,
                PrecisionTwoLoaderSequenceValidator validator,
                int maximumRecordCount)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }
            if (validator == null)
            {
                throw new ArgumentNullException("validator");
            }
            if (maximumRecordCount < 1 || maximumRecordCount > 16)
            {
                throw new ArgumentOutOfRangeException("maximumRecordCount",
                    "The bounded experimental prefix is one through sixteen records.");
            }
            if (validator.AcceptedRecordCount >= maximumRecordCount)
            {
                validator.FailClosed();
                throw new BrokerProtocolException(
                    "The approved loader-record prefix is already complete.");
            }

            int expectedRecordIndex = validator.AcceptedRecordCount;
            PrecisionTwoLoaderSequenceStep step =
                validator.ValidateNext(request);
            ushort expectedAddress = checked((ushort)(0x3000 +
                (expectedRecordIndex * 0x100)));
            if (step.Kind !=
                    PrecisionTwoLoaderSequenceStepKind.FirmwareRecord ||
                step.RecordIndex != expectedRecordIndex ||
                step.RecordIndex >= maximumRecordCount ||
                step.AddressLikeValue != expectedAddress)
            {
                validator.FailClosed();
                throw new BrokerProtocolException(
                    "The request is outside the exact approved loader-record prefix.");
            }

            RawScsiResult result = transport.
                ExecutePrecisionTwoLoaderWriteBufferRecordOnce(
                    request.Data, expectedRecordIndex);
            if (result == null ||
                result.RequestedLength != request.TransferLength ||
                result.Data.Length != 0)
            {
                validator.FailClosed();
                throw new ProtocolException(string.Format(
                    "Loader record {0} returned an inconsistent data-out result.",
                    expectedRecordIndex + 1));
            }

            BrokerCompletion completion = BuildCompletion(request, transport,
                result, false);
            try
            {
                validator.ValidateCompletion(completion);
            }
            catch (BrokerProtocolException ex)
            {
                throw new ProtocolException(string.Format(
                    "Loader record {0} completion failed the exact policy: {1}",
                    expectedRecordIndex + 1, ex.Message), ex);
            }
            if (validator.AcceptedRecordCount != expectedRecordIndex + 1 ||
                validator.State != PrecisionTwoLoaderSequenceState.Records)
            {
                validator.FailClosed();
                throw new ProtocolException(string.Format(
                    "Loader record {0} did not advance exactly one step.",
                    expectedRecordIndex + 1));
            }
            return completion;
        }

        internal static BrokerCompletion
            ExecuteCapturedLoaderWriteBufferTerminalOnce(
                BrokerRequest request, IReadOnlyScsiTransport transport,
                PrecisionTwoLoaderSequenceValidator validator)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }
            if (validator == null)
            {
                throw new ArgumentNullException("validator");
            }
            if (validator.AcceptedRecordCount != 16 ||
                validator.State != PrecisionTwoLoaderSequenceState.Records)
            {
                validator.FailClosed();
                throw new BrokerProtocolException(
                    "The terminal record requires sixteen exact successful " +
                    "loader-record completions.");
            }

            PrecisionTwoLoaderSequenceStep step =
                validator.ValidateNext(request);
            if (step.Kind !=
                    PrecisionTwoLoaderSequenceStepKind.TerminalRecord ||
                step.RecordIndex != -1 || step.AddressLikeValue != 0x3001)
            {
                validator.FailClosed();
                throw new BrokerProtocolException(
                    "The request is outside the exact approved loader terminal.");
            }

            RawScsiResult result;
            try
            {
                result = transport.
                    ExecutePrecisionTwoLoaderWriteBufferTerminalOnce(
                        request.Data);
            }
            catch
            {
                validator.FailClosed();
                throw;
            }
            if (result == null ||
                result.RequestedLength != request.TransferLength ||
                result.Data.Length != 0)
            {
                validator.FailClosed();
                throw new ProtocolException(
                    "Loader terminal returned an inconsistent data-out result.");
            }

            BrokerCompletion completion = BuildCompletion(request, transport,
                result, false);
            try
            {
                validator.ValidateCompletion(completion);
            }
            catch (BrokerProtocolException ex)
            {
                throw new ProtocolException(
                    "Loader terminal completion failed the exact policy: " +
                    ex.Message, ex);
            }
            if (validator.State != PrecisionTwoLoaderSequenceState.Complete ||
                validator.AcceptedRecordCount != 16)
            {
                validator.FailClosed();
                throw new ProtocolException(
                    "Loader terminal did not complete the exact sequence.");
            }
            return completion;
        }

        internal static BrokerCompletion
            SimulateCapturedLoaderWriteBufferModeOneZeroProtocolError(
                BrokerRequest request)
        {
            BrokerWireProtocol.ValidateCapturedLoaderWriteBufferModeOneZero(
                request);
            return Failure(request, BrokerTransportStatus.ProtocolError);
        }

        private static BrokerCompletion BuildCompletion(BrokerRequest request,
            IReadOnlyScsiTransport transport, RawScsiResult result,
            bool allowAutomaticSense)
        {
            ValidateHardwareResult(result);

            byte[] cdb = request.Cdb;
            byte scsiStatus;
            byte[] sense = new byte[0];
            switch (result.AdapterStatus)
            {
                case (byte)AdapterStatus.Success:
                    scsiStatus = 0;
                    break;

                case (byte)AdapterStatus.CheckCondition:
                    scsiStatus = 0x02;
                    if (allowAutomaticSense && cdb[0] != 0x03 &&
                        request.SenseAllocationLength != 0)
                    {
                        byte senseLength = checked((byte)Math.Min(
                            (int)request.SenseAllocationLength,
                            BrokerWireProtocol.MaximumSenseLength));
                        RawScsiResult senseResult = transport.Execute(
                            request.PhysicalTargetId, request.PhysicalLun,
                            ScsiFraming.BuildRequestSenseCdb(senseLength),
                            senseLength,
                            checked((int)request.TimeoutMilliseconds));
                        ValidateHardwareResult(senseResult);
                        if (senseResult.AdapterStatus ==
                                (byte)AdapterStatus.Success)
                        {
                            sense = senseResult.Data;
                        }
                    }
                    break;

                case (byte)AdapterStatus.Busy:
                    scsiStatus = 0x08;
                    break;

                case (byte)AdapterStatus.SelectionTimeout:
                    scsiStatus = 0;
                    break;

                default:
                    throw new ProtocolException(string.Format(
                        "USB2Xchange returned unknown adapter status {0:X2}.",
                        result.AdapterStatus));
            }

            BrokerCompletion completion = new BrokerCompletion(
                request.RequestId, request.Generation,
                BrokerTransportStatus.Success, result.AdapterStatus,
                scsiStatus, result.TransferLength, result.Residue, sense,
                result.Data);
            BrokerWireProtocol.ValidateCompletion(request, completion);
            return completion;
        }

        private static BrokerCompletion SyntheticReportLuns(
            BrokerRequest request)
        {
            byte[] data = new byte[16];
            // One eight-byte LUN entry follows the eight-byte header. The
            // all-zero entry is peripheral-addressing LUN 0.
            data[3] = 8;
            BrokerCompletion completion = new BrokerCompletion(
                request.RequestId, request.Generation,
                BrokerTransportStatus.Success, 0, 0, 16, 0,
                new byte[0], data);
            BrokerWireProtocol.ValidateCompletion(request, completion);
            return completion;
        }

        internal static BrokerCompletion Failure(BrokerRequest request,
            BrokerTransportStatus status)
        {
            if (status == BrokerTransportStatus.Success)
            {
                throw new ArgumentException(
                    "A failed completion cannot use transport success.", "status");
            }
            BrokerCompletion completion = new BrokerCompletion(
                request.RequestId, request.Generation, status, 0, 0, 0, 0,
                new byte[0], new byte[0]);
            BrokerWireProtocol.ValidateCompletion(request, completion);
            return completion;
        }

        private static void ValidateHardwareResult(RawScsiResult result)
        {
            if (result == null)
            {
                throw new ProtocolException("The SCSI transport returned no result.");
            }
            switch (result.AdapterStatus)
            {
                case (byte)AdapterStatus.Success:
                case (byte)AdapterStatus.CheckCondition:
                    return;

                case (byte)AdapterStatus.Busy:
                case (byte)AdapterStatus.SelectionTimeout:
                    if (result.TransferLength == 0)
                    {
                        return;
                    }
                    break;
            }
            throw new ProtocolException(string.Format(
                "USB2Xchange returned invalid status/length fields: " +
                "status={0:X2}, transferred={1}.",
                result.AdapterStatus, result.TransferLength));
        }
    }

    internal sealed class OperationalReadOnlyGate
    {
        private readonly bool allowOneScannerReady;
        private readonly bool allowOneFaultPixelReadBuffer;
        private readonly bool allowOnePreviewSetWindow;
        private readonly string expectedPreviewSetWindowSha256;
        private bool operationalD8Succeeded;
        private bool scannerReadyConsumed;
        private bool scannerReadySucceeded;
        private bool faultPixelReadBufferConsumed;
        private bool faultPixelReadBufferSucceeded;
        private bool postFaultPixelScannerReadyConsumed;
        private bool postFaultPixelScannerReadySucceeded;
        private bool faultPixelDataReadBufferConsumed;
        private bool faultPixelDataReadBufferSucceeded;
        private bool calibrationReadBufferConsumed;
        private bool calibrationReadBufferSucceeded;
        private bool d8Offset55ReadBufferConsumed;
        private bool d8Offset55ReadBufferSucceeded;
        private bool postCalibrationScannerReadyConsumed;
        private bool postCalibrationScannerReadySucceeded;
        private readonly int maximumInitializationCycles;
        private int completedInitializationCycles;
        private bool previewSetWindowReady;
        private bool previewSetWindowConsumed;
        private bool previewSetWindowSucceeded;
        private bool predictedPreviewImageReadCaptured;

        internal OperationalReadOnlyGate(bool allowOneScannerReady)
            : this(allowOneScannerReady, false)
        {
        }

        internal OperationalReadOnlyGate(bool allowOneScannerReady,
            bool allowOneFaultPixelReadBuffer)
            : this(allowOneScannerReady, allowOneFaultPixelReadBuffer, 1)
        {
        }

        internal OperationalReadOnlyGate(bool allowOneScannerReady,
            bool allowOneFaultPixelReadBuffer, int maximumInitializationCycles)
            : this(allowOneScannerReady, allowOneFaultPixelReadBuffer,
                maximumInitializationCycles, false,
                BrokerWireProtocol.PrecisionTwoPreviewSetWindowSha256)
        {
        }

        internal OperationalReadOnlyGate(bool allowOneScannerReady,
            bool allowOneFaultPixelReadBuffer, int maximumInitializationCycles,
            bool allowOnePreviewSetWindow)
            : this(allowOneScannerReady, allowOneFaultPixelReadBuffer,
                maximumInitializationCycles, allowOnePreviewSetWindow,
                BrokerWireProtocol.PrecisionTwoPreviewSetWindowSha256)
        {
        }

        internal OperationalReadOnlyGate(bool allowOneScannerReady,
            bool allowOneFaultPixelReadBuffer, int maximumInitializationCycles,
            bool allowOnePreviewSetWindow,
            string expectedPreviewSetWindowSha256)
        {
            if (maximumInitializationCycles < 1 ||
                maximumInitializationCycles > 6)
            {
                throw new ArgumentOutOfRangeException(
                    "maximumInitializationCycles");
            }
            this.allowOneScannerReady = allowOneScannerReady;
            this.allowOneFaultPixelReadBuffer = allowOneFaultPixelReadBuffer;
            this.maximumInitializationCycles = maximumInitializationCycles;
            this.allowOnePreviewSetWindow = allowOnePreviewSetWindow;
            if (expectedPreviewSetWindowSha256 == null ||
                expectedPreviewSetWindowSha256.Length != 64)
            {
                throw new ArgumentException(
                    "An exact Preview SET WINDOW SHA-256 is required.",
                    "expectedPreviewSetWindowSha256");
            }
            this.expectedPreviewSetWindowSha256 =
                expectedPreviewSetWindowSha256;
        }

        internal bool OperationalD8Succeeded
        {
            get { return operationalD8Succeeded; }
        }

        internal bool ScannerReadyConsumed
        {
            get { return scannerReadyConsumed; }
        }

        internal bool ScannerReadySucceeded
        {
            get { return scannerReadySucceeded; }
        }

        internal bool FaultPixelReadBufferConsumed
        {
            get { return faultPixelReadBufferConsumed; }
        }

        internal bool FaultPixelReadBufferSucceeded
        {
            get { return faultPixelReadBufferSucceeded; }
        }

        internal bool PostFaultPixelScannerReadyConsumed
        {
            get { return postFaultPixelScannerReadyConsumed; }
        }

        internal bool PostFaultPixelScannerReadySucceeded
        {
            get { return postFaultPixelScannerReadySucceeded; }
        }

        internal bool FaultPixelDataReadBufferConsumed
        {
            get { return faultPixelDataReadBufferConsumed; }
        }

        internal bool FaultPixelDataReadBufferSucceeded
        {
            get { return faultPixelDataReadBufferSucceeded; }
        }

        internal bool CalibrationReadBufferConsumed
        {
            get { return calibrationReadBufferConsumed; }
        }

        internal bool CalibrationReadBufferSucceeded
        {
            get { return calibrationReadBufferSucceeded; }
        }

        internal bool D8Offset55ReadBufferConsumed
        {
            get { return d8Offset55ReadBufferConsumed; }
        }

        internal bool D8Offset55ReadBufferSucceeded
        {
            get { return d8Offset55ReadBufferSucceeded; }
        }

        internal bool PostCalibrationScannerReadyConsumed
        {
            get { return postCalibrationScannerReadyConsumed; }
        }

        internal bool PostCalibrationScannerReadySucceeded
        {
            get { return postCalibrationScannerReadySucceeded; }
        }

        internal int CompletedInitializationCycles
        {
            get { return completedInitializationCycles; }
        }

        internal bool PreviewSetWindowReady
        {
            get { return previewSetWindowReady; }
        }

        internal bool PreviewSetWindowConsumed
        {
            get { return previewSetWindowConsumed; }
        }

        internal bool PreviewSetWindowSucceeded
        {
            get { return previewSetWindowSucceeded; }
        }

        internal bool PredictedPreviewImageReadCaptured
        {
            get { return predictedPreviewImageReadCaptured; }
        }

        internal void RecordOperationalD8Completion(
            BrokerCompletion completion)
        {
            if (completion == null)
            {
                throw new ArgumentNullException("completion");
            }
            operationalD8Succeeded =
                completion.TransportStatus == BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 66 && completion.Residue == 0 &&
                completion.Data.Length == 66;
        }

        internal bool TryConsumeScannerReady(BrokerRequest request)
        {
            if (!allowOneScannerReady || !operationalD8Succeeded ||
                scannerReadyConsumed)
            {
                return false;
            }
            try
            {
                BrokerWireProtocol.
                    ValidateCapturedOperationalScannerReady(request);
            }
            catch (BrokerProtocolException)
            {
                return false;
            }
            scannerReadyConsumed = true;
            return true;
        }

        internal void RecordScannerReadyCompletion(BrokerCompletion completion)
        {
            if (completion == null)
            {
                throw new ArgumentNullException("completion");
            }
            scannerReadySucceeded =
                completion.TransportStatus == BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 2 && completion.Residue == 0 &&
                completion.Data.Length == 2;
        }

        internal bool TryConsumeFaultPixelReadBuffer(BrokerRequest request)
        {
            if (!allowOneFaultPixelReadBuffer || !scannerReadySucceeded ||
                faultPixelReadBufferConsumed)
            {
                return false;
            }
            try
            {
                BrokerWireProtocol.
                    ValidateCapturedOperationalFaultPixelReadBuffer(request);
            }
            catch (BrokerProtocolException)
            {
                return false;
            }
            faultPixelReadBufferConsumed = true;
            previewSetWindowReady = false;
            return true;
        }

        internal void RecordFaultPixelReadBufferCompletion(
            BrokerCompletion completion)
        {
            if (completion == null)
            {
                throw new ArgumentNullException("completion");
            }
            faultPixelReadBufferSucceeded =
                completion.TransportStatus == BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 22 && completion.Residue == 0 &&
                HasKnownPrecisionTwoFaultPixelHeader(completion.Data);
        }

        internal bool TryConsumePostFaultPixelScannerReady(
            BrokerRequest request)
        {
            if (!allowOneFaultPixelReadBuffer ||
                !faultPixelReadBufferSucceeded ||
                postFaultPixelScannerReadyConsumed)
            {
                return false;
            }
            try
            {
                BrokerWireProtocol.
                    ValidateCapturedOperationalScannerReady(request);
            }
            catch (BrokerProtocolException)
            {
                return false;
            }
            postFaultPixelScannerReadyConsumed = true;
            return true;
        }

        internal void RecordPostFaultPixelScannerReadyCompletion(
            BrokerCompletion completion)
        {
            if (completion == null)
            {
                throw new ArgumentNullException("completion");
            }
            postFaultPixelScannerReadySucceeded =
                completion.TransportStatus == BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 2 && completion.Residue == 0 &&
                completion.Data.Length == 2;
        }

        internal bool TryConsumeFaultPixelDataReadBuffer(
            BrokerRequest request)
        {
            if (!allowOneFaultPixelReadBuffer ||
                !postFaultPixelScannerReadySucceeded ||
                faultPixelDataReadBufferConsumed)
            {
                return false;
            }
            try
            {
                BrokerWireProtocol.
                    ValidateCapturedOperationalFaultPixelDataReadBuffer(request);
            }
            catch (BrokerProtocolException)
            {
                return false;
            }
            faultPixelDataReadBufferConsumed = true;
            return true;
        }

        internal void RecordFaultPixelDataReadBufferCompletion(
            BrokerCompletion completion)
        {
            if (completion == null)
            {
                throw new ArgumentNullException("completion");
            }
            faultPixelDataReadBufferSucceeded =
                completion.TransportStatus == BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 58 && completion.Residue == 0 &&
                completion.Data.Length == 58;
        }

        internal bool TryConsumeCalibrationReadBuffer(BrokerRequest request)
        {
            if (!allowOneFaultPixelReadBuffer ||
                !faultPixelDataReadBufferSucceeded ||
                calibrationReadBufferConsumed)
            {
                return false;
            }
            try
            {
                BrokerWireProtocol.
                    ValidateCapturedOperationalCalibrationReadBuffer(request);
            }
            catch (BrokerProtocolException)
            {
                return false;
            }
            calibrationReadBufferConsumed = true;
            return true;
        }

        internal void RecordCalibrationReadBufferCompletion(
            BrokerCompletion completion)
        {
            if (completion == null)
            {
                throw new ArgumentNullException("completion");
            }
            calibrationReadBufferSucceeded =
                completion.TransportStatus == BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 1024 && completion.Residue == 0 &&
                completion.Data.Length == 1024;
        }

        internal bool TryConsumeD8Offset55ReadBuffer(BrokerRequest request)
        {
            if (!allowOneFaultPixelReadBuffer ||
                !calibrationReadBufferSucceeded ||
                d8Offset55ReadBufferConsumed ||
                postCalibrationScannerReadyConsumed)
            {
                return false;
            }
            try
            {
                BrokerWireProtocol.
                    ValidateCapturedOperationalD8Offset55ReadBuffer(request);
            }
            catch (BrokerProtocolException)
            {
                return false;
            }
            d8Offset55ReadBufferConsumed = true;
            return true;
        }

        internal void RecordD8Offset55ReadBufferCompletion(
            BrokerCompletion completion)
        {
            if (completion == null)
            {
                throw new ArgumentNullException("completion");
            }
            d8Offset55ReadBufferSucceeded =
                completion.TransportStatus == BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 10 && completion.Residue == 0 &&
                completion.Data.Length == 10;
        }

        internal bool TryConsumePostCalibrationScannerReady(
            BrokerRequest request)
        {
            if (!allowOneFaultPixelReadBuffer ||
                !calibrationReadBufferSucceeded ||
                (d8Offset55ReadBufferConsumed &&
                    !d8Offset55ReadBufferSucceeded) ||
                postCalibrationScannerReadyConsumed)
            {
                return false;
            }
            try
            {
                BrokerWireProtocol.
                    ValidateCapturedOperationalScannerReady(request);
            }
            catch (BrokerProtocolException)
            {
                return false;
            }
            postCalibrationScannerReadyConsumed = true;
            return true;
        }

        internal void RecordPostCalibrationScannerReadyCompletion(
            BrokerCompletion completion)
        {
            if (completion == null)
            {
                throw new ArgumentNullException("completion");
            }
            postCalibrationScannerReadySucceeded =
                completion.TransportStatus == BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 2 && completion.Residue == 0 &&
                completion.Data.Length == 2;
            if (!postCalibrationScannerReadySucceeded)
            {
                return;
            }

            completedInitializationCycles++;
            previewSetWindowReady = true;
            if (completedInitializationCycles >= maximumInitializationCycles)
            {
                return;
            }

            scannerReadySucceeded = true;
            faultPixelReadBufferConsumed = false;
            faultPixelReadBufferSucceeded = false;
            postFaultPixelScannerReadyConsumed = false;
            postFaultPixelScannerReadySucceeded = false;
            faultPixelDataReadBufferConsumed = false;
            faultPixelDataReadBufferSucceeded = false;
            calibrationReadBufferConsumed = false;
            calibrationReadBufferSucceeded = false;
            d8Offset55ReadBufferConsumed = false;
            d8Offset55ReadBufferSucceeded = false;
            postCalibrationScannerReadyConsumed = false;
            postCalibrationScannerReadySucceeded = false;
        }

        internal bool TryConsumePreviewSetWindow(BrokerRequest request)
        {
            if (!allowOnePreviewSetWindow || !previewSetWindowReady ||
                completedInitializationCycles < 1 ||
                previewSetWindowConsumed)
            {
                return false;
            }
            try
            {
                BrokerWireProtocol.
                    ValidateCapturedOperationalPreviewSetWindow(request,
                        expectedPreviewSetWindowSha256);
            }
            catch (BrokerProtocolException)
            {
                // The allowance applies only to the immediate next captured
                // request after a successful calibration-continuation poll.
                previewSetWindowReady = false;
                return false;
            }
            previewSetWindowReady = false;
            previewSetWindowConsumed = true;
            return true;
        }

        internal void RecordPreviewSetWindowCompletion(
            BrokerCompletion completion)
        {
            if (completion == null)
            {
                throw new ArgumentNullException("completion");
            }
            previewSetWindowSucceeded = previewSetWindowConsumed &&
                completion.TransportStatus == BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 84 && completion.Residue == 0 &&
                completion.Data.Length == 0;
        }

        internal bool TryCapturePredictedPreviewImageRead(
            BrokerRequest request)
        {
            if (!previewSetWindowSucceeded ||
                predictedPreviewImageReadCaptured)
            {
                return false;
            }

            // Static analysis predicts that this is the immediately following
            // SCSI submission. Any different request consumes the observation
            // point but remains subject to the normal fail-closed policy.
            previewSetWindowSucceeded = false;
            try
            {
                BrokerWireProtocol.
                    ValidatePredictedOperationalPreviewImageRead(request);
            }
            catch (BrokerProtocolException)
            {
                return false;
            }
            predictedPreviewImageReadCaptured = true;
            return true;
        }

        private static bool HasKnownPrecisionTwoFaultPixelHeader(byte[] data)
        {
            byte[] expected = new byte[]
            {
                0x46, 0x61, 0x75, 0x6C, 0x20, 0x50, 0x69, 0x78,
                0x73, 0x00, 0x20, 0x20, 0x30, 0x0A, 0x20, 0x20,
                0x31, 0x0A, 0x20, 0x20, 0x30, 0x0A
            };
            if (data == null || data.Length != expected.Length)
            {
                return false;
            }
            for (int index = 0; index < expected.Length; index++)
            {
                if (data[index] != expected[index])
                {
                    return false;
                }
            }
            return true;
        }
    }

    internal sealed class CaptureCommandGate
    {
        private readonly bool allowOneLoaderReadBufferD8;
        private readonly bool allowOneLoaderWriteBufferModeOneZero;
        private readonly int allowedLoaderRecordCount;
        private bool loaderReadBufferD8Consumed;
        private bool loaderWriteBufferModeOneZeroConsumed;

        internal CaptureCommandGate(bool allowOneLoaderReadBufferD8,
            bool allowOneLoaderWriteBufferModeOneZero)
            : this(allowOneLoaderReadBufferD8,
                allowOneLoaderWriteBufferModeOneZero, false, null)
        {
        }

        internal CaptureCommandGate(bool allowOneLoaderReadBufferD8,
            bool allowOneLoaderWriteBufferModeOneZero,
            bool allowOneLoaderWriteBufferFirstRecord,
            PrecisionTwoLoaderSequenceManifest loaderManifest)
            : this(allowOneLoaderReadBufferD8,
                allowOneLoaderWriteBufferModeOneZero,
                allowOneLoaderWriteBufferFirstRecord ? 1 : 0,
                loaderManifest)
        {
        }

        internal CaptureCommandGate(bool allowOneLoaderReadBufferD8,
            bool allowOneLoaderWriteBufferModeOneZero,
            int allowedLoaderRecordCount,
            PrecisionTwoLoaderSequenceManifest loaderManifest)
        {
            if ((allowOneLoaderWriteBufferModeOneZero ||
                    allowedLoaderRecordCount != 0) &&
                !allowOneLoaderReadBufferD8)
            {
                throw new ArgumentException(
                    "The WRITE BUFFER boundary cannot be enabled without " +
                    "the preceding READ BUFFER D8 gate.",
                    "allowOneLoaderWriteBufferModeOneZero");
            }
            if (allowOneLoaderWriteBufferModeOneZero &&
                allowedLoaderRecordCount != 0)
            {
                throw new ArgumentException(
                    "Only one WRITE BUFFER experiment may be enabled.");
            }
            if (allowedLoaderRecordCount < 0 || allowedLoaderRecordCount > 16)
            {
                throw new ArgumentOutOfRangeException(
                    "allowedLoaderRecordCount",
                    "The bounded experimental prefix is zero through sixteen records.");
            }
            if ((allowedLoaderRecordCount != 0) !=
                (loaderManifest != null))
            {
                throw new ArgumentException(
                    "The loader-record gate and manifest must be " +
                    "provided together.", "loaderManifest");
            }
            this.allowOneLoaderReadBufferD8 = allowOneLoaderReadBufferD8;
            this.allowOneLoaderWriteBufferModeOneZero =
                allowOneLoaderWriteBufferModeOneZero;
            this.allowedLoaderRecordCount = allowedLoaderRecordCount;
        }

        internal bool LoaderReadBufferD8Consumed
        {
            get { return loaderReadBufferD8Consumed; }
        }

        internal bool LoaderWriteBufferModeOneZeroConsumed
        {
            get { return loaderWriteBufferModeOneZeroConsumed; }
        }

        internal int AllowedLoaderRecordCount
        {
            get { return allowedLoaderRecordCount; }
        }

        internal bool TryConsumeLoaderReadBufferD8(BrokerRequest request)
        {
            if (!allowOneLoaderReadBufferD8 || loaderReadBufferD8Consumed)
            {
                return false;
            }
            try
            {
                BrokerWireProtocol.ValidateCapturedLoaderReadBufferD8(request);
            }
            catch (BrokerProtocolException)
            {
                return false;
            }
            loaderReadBufferD8Consumed = true;
            return true;
        }

        internal bool TryConsumeLoaderWriteBufferModeOneZero(
            BrokerRequest request)
        {
            if (!allowOneLoaderWriteBufferModeOneZero ||
                !loaderReadBufferD8Consumed ||
                loaderWriteBufferModeOneZeroConsumed)
            {
                return false;
            }
            try
            {
                BrokerWireProtocol.
                    ValidateCapturedLoaderWriteBufferModeOneZero(request);
            }
            catch (BrokerProtocolException)
            {
                return false;
            }
            loaderWriteBufferModeOneZeroConsumed = true;
            return true;
        }

        internal bool ShouldHandleLoaderWriteBufferRecord(
            BrokerRequest request)
        {
            if (allowedLoaderRecordCount == 0 ||
                !loaderReadBufferD8Consumed || request == null)
            {
                return false;
            }
            byte[] cdb = request.Cdb;
            return cdb.Length != 0 && cdb[0] == 0x3B;
        }
    }
}
