// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Threading;
using Usb2Xchange.Protocol;

namespace Usb2Xchange.WinUsb
{
    public sealed class ScsiCommandResult
    {
        internal ScsiCommandResult(byte[] data, CommandStatusWrapper status)
        {
            Data = data;
            Status = status;
        }

        public byte[] Data { get; private set; }
        public CommandStatusWrapper Status { get; private set; }
    }

    public sealed class Usb2XchangeDevice : IDisposable
    {
        public const uint MaximumPassThroughTransferLength =
            16U * 1024U * 1024U;
        private static readonly PrecisionTwoLoaderRecordManifest
            PrecisionTwoLoaderManifest =
                PrecisionTwoLoaderRecordManifest.CreateKnown();
        private readonly WinUsbDevice usb;
        private readonly IUsbLog log;
        private readonly int transferTimeoutMilliseconds;
        private readonly string precisionTwoPreviewSetWindowSha256;
        private byte bulkIn;
        private byte bulkOut;
        private int nextTag;
        private bool operationalInitialized;
        private int precisionTwoLoaderRecordCount;
        private bool precisionTwoLoaderRecordSequenceFailed;
        private bool precisionTwoLoaderTerminalConsumed;
        private bool precisionTwoPreviewSetWindowAttempted;
        private bool precisionTwoPreviewSetWindowCompleted;
        private int precisionTwoPreviewImageReadsAttempted;
        private int precisionTwoPreviewStreamScannerReadyPollsAttempted;
        private int precisionTwoPreviewCleanupSetWindowsAttempted;
        private bool precisionTwoFullScanSetWindowAttempted;
        private bool precisionTwoFullScanSetWindowCompleted;
        private int precisionTwoFullScanImageReadsAttempted;
        private bool precisionTwoFullScanTerminalProbeAttempted;
        private int precisionTwoFullScanScannerReadyPollsAttempted;
        private int precisionTwoFullScanCleanupSetWindowsAttempted;

        private Usb2XchangeDevice(WinUsbDevice usb, IUsbLog log,
            int transferTimeoutMilliseconds,
            string precisionTwoPreviewSetWindowSha256)
        {
            this.usb = usb;
            this.log = log ?? new NullUsbLog();
            this.transferTimeoutMilliseconds = transferTimeoutMilliseconds;
            this.precisionTwoPreviewSetWindowSha256 =
                precisionTwoPreviewSetWindowSha256;
            Descriptor = usb.GetDeviceDescriptor();
            nextTag = Environment.TickCount;
        }

        public UsbDeviceDescriptorInfo Descriptor { get; private set; }

        public static Usb2XchangeDevice Find(ushort productId, IUsbLog log,
            int transferTimeoutMilliseconds)
        {
            return Find(productId, log, transferTimeoutMilliseconds,
                PrecisionTwoPreviewCommandManifest.SetWindowSha256);
        }

        internal static Usb2XchangeDevice Find(ushort productId, IUsbLog log,
            int transferTimeoutMilliseconds,
            string expectedPreviewSetWindowSha256)
        {
            expectedPreviewSetWindowSha256 = NormalizeSha256(
                expectedPreviewSetWindowSha256,
                "expectedPreviewSetWindowSha256");
            IList<string> paths = DeviceEnumerator.FindDevicePaths();
            Usb2XchangeDevice match = null;
            Win32Exception lastOpenError = null;
            foreach (string path in paths)
            {
                WinUsbDevice candidate = null;
                try
                {
                    candidate = WinUsbDevice.Open(path, log);
                    UsbDeviceDescriptorInfo descriptor = candidate.GetDeviceDescriptor();
                    if (descriptor.VendorId == UsbConstants.VendorId &&
                        descriptor.ProductId == productId)
                    {
                        if (match != null)
                        {
                            candidate.Dispose();
                            match.Dispose();
                            throw new InvalidOperationException(string.Format(
                                "More than one USB2Xchange PID {0:X4} device is present.",
                                productId));
                        }
                        match = new Usb2XchangeDevice(candidate, log,
                            transferTimeoutMilliseconds,
                            expectedPreviewSetWindowSha256);
                        candidate = null;
                    }
                }
                catch (Win32Exception ex)
                {
                    lastOpenError = ex;
                    if (log != null)
                    {
                        log.Warning("Could not inspect WinUSB path: " + ex.Message);
                    }
                }
                finally
                {
                    if (candidate != null)
                    {
                        candidate.Dispose();
                    }
                }
            }
            if (match == null && lastOpenError != null)
            {
                throw lastOpenError;
            }
            return match;
        }

        public static Usb2XchangeDevice WaitFor(ushort productId,
            TimeSpan timeout, IUsbLog log, int transferTimeoutMilliseconds)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            Win32Exception lastError = null;
            while (DateTime.UtcNow <= deadline)
            {
                try
                {
                    Usb2XchangeDevice device = Find(productId, log,
                        transferTimeoutMilliseconds);
                    if (device != null)
                    {
                        return device;
                    }
                }
                catch (Win32Exception ex)
                {
                    lastError = ex;
                }
                Thread.Sleep(250);
            }

            string message = string.Format(
                "Timed out after {0:0.0} seconds waiting for VID {1:X4}, PID {2:X4}.",
                timeout.TotalSeconds, UsbConstants.VendorId, productId);
            if (lastError != null)
            {
                throw new TimeoutException(message, lastError);
            }
            throw new TimeoutException(message);
        }

        public void UploadKnownUsb2Firmware(FirmwareImage firmware)
        {
            if (firmware == null)
            {
                throw new ArgumentNullException("firmware");
            }
            if (Descriptor.VendorId != UsbConstants.VendorId ||
                Descriptor.ProductId != UsbConstants.LoaderProductId)
            {
                throw new InvalidOperationException(
                    "Firmware upload requires the USB2Xchange loader PID 2002.");
            }

            firmware.RequireKnownUsb2XchangeImage();
            foreach (FirmwareRecord record in firmware.Records)
            {
                if (record.Address >= 0x2000)
                {
                    throw new ProtocolException(string.Format(
                        "Refusing unverified external-memory record at 0x{0:X4}.",
                        record.Address));
                }
            }

            log.Info(string.Format(
                "Uploading known USB2Xchange firmware: records={0}, payload={1}, SHA-256={2}",
                firmware.Records.Count, firmware.PayloadLength, firmware.Sha256));

            WriteCpuReset(1);
            WriteCpuReset(1);

            for (int i = 0; i < firmware.Records.Count; i++)
            {
                FirmwareRecord record = firmware.Records[i];
                byte[] recordData = record.Data;
                log.Trace(string.Format("FW record {0}/{1} address={2:X4} length={3}",
                    i + 1, firmware.Records.Count, record.Address,
                    recordData.Length));
                usb.ControlOut(0xA0, (ushort)record.Address, 0,
                    recordData, transferTimeoutMilliseconds);
            }

            WriteCpuReset(1);
            try
            {
                WriteCpuReset(0);
            }
            catch (Win32Exception ex)
            {
                if (!IsExpectedDisconnect(ex.NativeErrorCode))
                {
                    throw;
                }
                log.Warning(string.Format(
                    "CPU release completed with disconnect-related Win32 error {0}; " +
                    "PID 2003 re-enumeration will determine success.",
                    ex.NativeErrorCode));
            }
            catch (TimeoutException ex)
            {
                log.Warning(
                    "CPU release timed out; PID 2003 re-enumeration will determine success. " +
                    ex.Message);
            }
        }

        public void InitializeOperational()
        {
            if (Descriptor.VendorId != UsbConstants.VendorId ||
                Descriptor.ProductId != UsbConstants.OperationalProductId)
            {
                throw new InvalidOperationException(
                    "Operational initialization requires USB2Xchange PID 2003.");
            }

            byte foundIn = 0;
            byte foundOut = 0;
            IList<UsbPipeInfo> pipes = usb.GetPipes();
            foreach (UsbPipeInfo pipe in pipes)
            {
                log.Info(string.Format(
                    "Pipe {0:X2}: type={1}, direction={2}, max-packet={3}, interval={4}",
                    pipe.PipeId, pipe.TypeName, pipe.IsIn ? "IN" : "OUT",
                    pipe.MaximumPacketSize, pipe.Interval));

                if (pipe.IsBulk && pipe.IsIn && foundIn == 0)
                {
                    foundIn = pipe.PipeId;
                }
                if (pipe.IsBulk && !pipe.IsIn && foundOut == 0)
                {
                    foundOut = pipe.PipeId;
                }
            }

            if (foundIn == 0 || foundOut == 0)
            {
                throw new ProtocolException(
                    "Operational interface did not expose one bulk IN and one bulk OUT pipe.");
            }

            bulkIn = foundIn;
            bulkOut = foundOut;
            usb.SetPipeTimeout(bulkIn, transferTimeoutMilliseconds);
            usb.SetPipeTimeout(bulkOut, transferTimeoutMilliseconds);

            if (bulkOut != 0x02 || bulkIn != 0x86)
            {
                log.Warning(string.Format(
                    "Observed endpoints OUT={0:X2}, IN={1:X2}; firmware evidence predicted 02/86.",
                    bulkOut, bulkIn));
            }

            usb.ControlOut(0x5A, 1, 0, new byte[0],
                transferTimeoutMilliseconds);
            usb.ControlOut(0x5A, 2, 0, new byte[0],
                transferTimeoutMilliseconds);
            operationalInitialized = true;
            log.Info(string.Format(
                "Operational interface initialized: bulk OUT={0:X2}, bulk IN={1:X2}.",
                bulkOut, bulkIn));
        }

        public InquiryData InquiryTargetZero()
        {
            return Inquiry(0, 0);
        }

        public InquiryData Inquiry(byte target, byte lun)
        {
            const byte allocationLength = 36;
            ScsiCommandResult result = ExecuteReadOnly(target, lun,
                ScsiFraming.BuildInquiryCdb(allocationLength), allocationLength);

            if (result.Status.RawStatus == (byte)AdapterStatus.CheckCondition)
            {
                ScsiCommandResult sense = ExecuteReadOnly(target, lun,
                    ScsiFraming.BuildRequestSenseCdb(18), 18);
                throw new ProtocolException(string.Format(
                    "INQUIRY for target {0}, LUN {1} returned CHECK CONDITION. " +
                    "REQUEST SENSE status={2:X2}, data={3}",
                    target, lun, sense.Status.RawStatus,
                    ScsiFraming.ToHex(sense.Data)));
            }
            RequireSuccess("INQUIRY", result.Status, target, lun);
            return InquiryData.Parse(result.Data);
        }

        public ScsiCommandResult ExecuteReadOnly(byte target, byte lun,
            byte[] cdb, uint requestedLength)
        {
            return ExecuteReadOnly(target, lun, cdb, requestedLength,
                transferTimeoutMilliseconds);
        }

        public ScsiCommandResult ExecuteReadOnly(byte target, byte lun,
            byte[] cdb, uint requestedLength, int timeoutMilliseconds)
        {
            EnsureOperational();
            if (timeoutMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            }
            if (cdb == null)
            {
                throw new ArgumentNullException("cdb");
            }
            if (cdb.Length != 6)
            {
                throw new ArgumentException(
                    "The initial read-only allowlist accepts six-byte CDBs only.", "cdb");
            }

            switch (cdb[0])
            {
                case 0x00: // TEST UNIT READY
                    if (requestedLength != 0)
                    {
                        throw new ArgumentException(
                            "TEST UNIT READY must not have a data phase.",
                            "requestedLength");
                    }
                    break;

                case 0x03: // REQUEST SENSE
                case 0x12: // INQUIRY
                    if (requestedLength == 0 || requestedLength > 255 ||
                        cdb[4] != requestedLength)
                    {
                        throw new ArgumentException(
                            "The transfer length must match the CDB allocation length.",
                            "requestedLength");
                    }
                    break;

                default:
                    throw new InvalidOperationException(string.Format(
                        "SCSI opcode 0x{0:X2} is blocked by the initial read-only allowlist.",
                        cdb[0]));
            }

            return ExecuteAcceptedCommand(target, lun, cdb, requestedLength,
                requestedLength == 0 ? DataDirection.None : DataDirection.In,
                timeoutMilliseconds);
        }

        public ScsiCommandResult ExecutePassThrough(byte target, byte lun,
            byte[] cdb, DataDirection direction, uint requestedLength,
            byte[] dataOut, int timeoutMilliseconds)
        {
            EnsureOperational();
            if (timeoutMilliseconds <= 0 || timeoutMilliseconds > 300000)
            {
                throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            }
            if (cdb == null)
            {
                throw new ArgumentNullException("cdb");
            }
            if (cdb.Length != 6 && cdb.Length != 10 &&
                cdb.Length != 12 && cdb.Length != 16)
            {
                throw new ArgumentException(
                    "CDB length must be 6, 10, 12, or 16 bytes.", "cdb");
            }
            if (requestedLength > MaximumPassThroughTransferLength)
            {
                throw new ArgumentOutOfRangeException("requestedLength");
            }

            int outputLength = dataOut == null ? 0 : dataOut.Length;
            if (direction == DataDirection.Out)
            {
                if (requestedLength == 0 ||
                    checked((uint)outputLength) != requestedLength)
                {
                    throw new ArgumentException(
                        "Data-out length must exactly match requestedLength.",
                        "dataOut");
                }
                return ExecuteAcceptedDataOutCommand(target, lun, cdb,
                    dataOut, timeoutMilliseconds, false);
            }
            if (outputLength != 0)
            {
                throw new ArgumentException(
                    "Only data-out commands may provide a payload.",
                    "dataOut");
            }
            if ((direction == DataDirection.None && requestedLength != 0) ||
                (direction == DataDirection.In && requestedLength == 0))
            {
                throw new ArgumentException(
                    "Direction and requested length are inconsistent.",
                    "direction");
            }
            if (direction != DataDirection.None &&
                direction != DataDirection.In)
            {
                throw new ArgumentOutOfRangeException("direction");
            }
            return ExecuteAcceptedCommand(target, lun, cdb, requestedLength,
                direction, timeoutMilliseconds, true);
        }

        public ScsiCommandResult ReadPrecisionTwoLoaderBufferD8Once()
        {
            EnsureOperational();
            RequirePrecisionTwoLoaderIdentity();
            return ExecuteAcceptedCommand(5, 0,
                ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb(),
                ScsiFraming.PrecisionTwoLoaderBufferD8Length,
                DataDirection.In, 10000);
        }

        public ScsiCommandResult ReadPrecisionTwoOperationalBufferD8Once()
        {
            EnsureOperational();
            RequirePrecisionTwoOperationalIdentity();
            return ExecuteAcceptedCommand(5, 0,
                ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb(),
                ScsiFraming.PrecisionTwoLoaderBufferD8Length,
                DataDirection.In, 10000);
        }

        public ScsiCommandResult ReadPrecisionTwoOperationalScannerReadyOnce()
        {
            EnsureOperational();
            RequirePrecisionTwoOperationalIdentity();
            return ExecuteAcceptedCommand(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                ScsiFraming.PrecisionTwoScannerReadyLength,
                DataDirection.In, 10000);
        }

        public ScsiCommandResult
            ReadPrecisionTwoOperationalPreviewStreamScannerReadyOnce(
                int rowsCompleted, int imageSubmissionsCompleted,
                int pollIndex, int maximumRows, int maximumSubmissions,
                int maximumShortRetries, int maximumPolls)
        {
            EnsureOperational();
            RequirePrecisionTwoOperationalIdentity();
            ValidatePrecisionTwoPreviewStreamScannerReadyProgress(
                precisionTwoPreviewSetWindowCompleted,
                precisionTwoPreviewImageReadsAttempted,
                precisionTwoPreviewStreamScannerReadyPollsAttempted,
                rowsCompleted, imageSubmissionsCompleted, pollIndex,
                maximumRows, maximumSubmissions, maximumShortRetries,
                maximumPolls);

            // Consume the poll before USB. A failed, short, or unexpected
            // response closes the upper sequence and cannot be retried.
            ++precisionTwoPreviewStreamScannerReadyPollsAttempted;
            return ExecuteAcceptedCommand(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                ScsiFraming.PrecisionTwoScannerReadyLength,
                DataDirection.In, 10000);
        }

        public ScsiCommandResult
            ReadPrecisionTwoOperationalFullScanStreamScannerReadyOnce(
                int rowsCompleted, int imageSubmissionsCompleted,
                int pollIndex, int maximumRows, int maximumSubmissions,
                int maximumShortRetries, int maximumPolls)
        {
            EnsureOperational();
            RequirePrecisionTwoOperationalIdentity();
            ValidatePrecisionTwoFullScanStreamProgress(
                precisionTwoFullScanSetWindowCompleted,
                precisionTwoFullScanImageReadsAttempted,
                precisionTwoFullScanScannerReadyPollsAttempted,
                rowsCompleted, imageSubmissionsCompleted, pollIndex, true,
                maximumRows, maximumSubmissions, maximumShortRetries,
                maximumPolls);

            ++precisionTwoFullScanScannerReadyPollsAttempted;
            return ExecuteAcceptedCommand(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                ScsiFraming.PrecisionTwoScannerReadyLength,
                DataDirection.In, 10000);
        }

        internal static void ValidatePrecisionTwoFullScanStreamProgress(
            bool setWindowCompleted, int imageAttemptsConsumed,
            int scannerReadyPollsConsumed, int rowsCompleted,
            int imageSubmissionsCompleted, int pollIndex,
            bool scannerReadyRequest)
        {
            ValidatePrecisionTwoFullScanStreamProgress(setWindowCompleted,
                imageAttemptsConsumed, scannerReadyPollsConsumed,
                rowsCompleted, imageSubmissionsCompleted, pollIndex,
                scannerReadyRequest,
                PrecisionTwoPreviewCommandManifest.AspiLiveFullScanRows,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumReadSubmissions,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumScannerReadyPolls);
        }

        internal static void ValidatePrecisionTwoFullScanStreamProgress(
            bool setWindowCompleted, int imageAttemptsConsumed,
            int scannerReadyPollsConsumed, int rowsCompleted,
            int imageSubmissionsCompleted, int pollIndex,
            bool scannerReadyRequest, int maximumRows,
            int maximumSubmissions, int maximumShortRetries,
            int maximumPolls)
        {
            bool acceptedBoundary = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.AspiLiveFullScanRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumReadSubmissions &&
                maximumShortRetries == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumShortRetries &&
                maximumPolls == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumScannerReadyPolls;
            bool naturalBoundary = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanNaturalRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanNaturalMaximumReadSubmissions &&
                maximumShortRetries == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanNaturalMaximumShortRetries &&
                maximumPolls == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumScannerReadyPolls;
            bool row997CompletionBoundary = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanRow997CompletionRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanRow997CompletionMaximumReadSubmissions &&
                maximumShortRetries == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanRow997CompletionMaximumShortRetries &&
                maximumPolls == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumScannerReadyPolls;
            bool progressCompletionBoundary = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanProgressMaximumRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanProgressMaximumReadSubmissions &&
                maximumShortRetries == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanProgressMaximumShortRetries &&
                maximumPolls == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumScannerReadyPolls;
            if ((!acceptedBoundary && !naturalBoundary &&
                    !row997CompletionBoundary &&
                    !progressCompletionBoundary) ||
                !setWindowCompleted || rowsCompleted < 0 ||
                rowsCompleted > maximumRows ||
                imageSubmissionsCompleted < rowsCompleted ||
                imageSubmissionsCompleted > maximumSubmissions ||
                imageSubmissionsCompleted - rowsCompleted >
                    maximumShortRetries ||
                imageAttemptsConsumed != imageSubmissionsCompleted ||
                pollIndex != scannerReadyPollsConsumed || pollIndex < 0 ||
                pollIndex > maximumPolls ||
                (scannerReadyRequest &&
                    (rowsCompleted <= 0 || pollIndex >= maximumPolls)))
            {
                throw new InvalidOperationException(
                    "The full-scan stream transport is outside its exact " +
                    "accepted-row, natural-row, retry, submission, or " +
                    "readiness boundary.");
            }
        }

        internal static void ValidatePrecisionTwoFullScanTerminalProbeProgress(
            bool setWindowCompleted, int imageAttemptsConsumed,
            bool terminalProbeAttempted, int rowsCompleted,
            int imageSubmissionsCompleted)
        {
            if (!setWindowCompleted || terminalProbeAttempted ||
                rowsCompleted != PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanRows ||
                imageSubmissionsCompleted < rowsCompleted ||
                imageSubmissionsCompleted >
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanMaximumReadSubmissions ||
                imageSubmissionsCompleted - rowsCompleted >
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanMaximumShortRetries ||
                imageAttemptsConsumed != imageSubmissionsCompleted)
            {
                throw new InvalidOperationException(
                    "The full-scan terminal probe is outside its exact " +
                    "post-762-row one-shot boundary.");
            }
        }

        internal static void
            ValidatePrecisionTwoPreviewStreamScannerReadyProgress(
                bool setWindowCompleted, int imageAttemptsConsumed,
                int scannerReadyPollsConsumed, int rowsCompleted,
                int imageSubmissionsCompleted, int pollIndex,
                int maximumRows, int maximumSubmissions,
                int maximumShortRetries, int maximumPolls)
        {
            bool exactBound = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamMaximumReadSubmissions &&
                maximumShortRetries == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamMaximumShortRetries &&
                maximumPolls == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamMaximumScannerReadyPolls;
            bool legacyNaturalBound = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalMaximumReadSubmissions &&
                maximumShortRetries == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalMaximumShortRetries &&
                maximumPolls == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalMaximumScannerReadyPolls;
            bool perRowNaturalBound = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalPerRowMaximumReadSubmissions &&
                maximumShortRetries == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalPerRowMaximumShortRetries &&
                maximumPolls == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalMaximumScannerReadyPolls;
            bool poweredNaturalBound = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalMaximumReadSubmissions &&
                maximumShortRetries == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalMaximumShortRetries &&
                maximumPolls == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalMaximumScannerReadyPolls;
            if (!setWindowCompleted ||
                (!exactBound && !legacyNaturalBound &&
                    !perRowNaturalBound && !poweredNaturalBound) ||
                rowsCompleted <= 0 || rowsCompleted >= maximumRows ||
                imageSubmissionsCompleted < rowsCompleted ||
                imageSubmissionsCompleted > maximumSubmissions ||
                imageSubmissionsCompleted - rowsCompleted >
                    maximumShortRetries ||
                imageAttemptsConsumed != imageSubmissionsCompleted ||
                pollIndex != scannerReadyPollsConsumed ||
                pollIndex < 0 || pollIndex >= maximumPolls)
            {
                throw new InvalidOperationException(
                    "The in-stream ScannerReady transport is outside its " +
                    "exact row/poll sequence boundary.");
            }
        }

        public ScsiCommandResult
            ReadPrecisionTwoOperationalFaultPixelBufferOnce()
        {
            EnsureOperational();
            RequirePrecisionTwoOperationalIdentity();
            return ExecuteAcceptedCommand(5, 0,
                ScsiFraming.BuildPrecisionTwoFaultPixelReadBufferCdb(),
                ScsiFraming.PrecisionTwoFaultPixelBufferLength,
                DataDirection.In, 10000);
        }

        public ScsiCommandResult
            ReadPrecisionTwoOperationalFaultPixelDataBufferOnce()
        {
            EnsureOperational();
            RequirePrecisionTwoOperationalIdentity();
            return ExecuteAcceptedCommand(5, 0,
                ScsiFraming.BuildPrecisionTwoFaultPixelDataReadBufferCdb(),
                ScsiFraming.PrecisionTwoFaultPixelDataLength,
                DataDirection.In, 10000);
        }

        public ScsiCommandResult
            ReadPrecisionTwoOperationalCalibrationBufferOnce()
        {
            EnsureOperational();
            RequirePrecisionTwoOperationalIdentity();
            return ExecuteAcceptedCommand(5, 0,
                ScsiFraming.BuildPrecisionTwoCalibrationReadBufferCdb(),
                ScsiFraming.PrecisionTwoCalibrationBufferLength,
                DataDirection.In, 10000);
        }

        public ScsiCommandResult
            ReadPrecisionTwoOperationalD8Offset55BufferOnce()
        {
            EnsureOperational();
            RequirePrecisionTwoOperationalIdentity();
            return ExecuteAcceptedCommand(5, 0,
                ScsiFraming.BuildPrecisionTwoD8Offset55ReadBufferCdb(),
                ScsiFraming.PrecisionTwoD8Offset55Length,
                DataDirection.In, 10000);
        }

        public ScsiCommandResult
            ReadPrecisionTwoOperationalDynamicConfigurationD8Once()
        {
            EnsureOperational();
            RequirePrecisionTwoOperationalIdentity();
            return ExecuteAcceptedCommand(5, 0,
                ScsiFraming.
                    BuildPrecisionTwoDynamicConfigurationD8ReadBufferCdb(),
                ScsiFraming.PrecisionTwoDynamicConfigurationLength,
                DataDirection.In, 10000);
        }

        public ScsiCommandResult
            ExecutePrecisionTwoOperationalPreviewSetWindowOnce(byte[] data)
        {
            EnsureOperational();
            if (precisionTwoPreviewSetWindowAttempted)
            {
                throw new InvalidOperationException(
                    "The operational Preview SET WINDOW transport can execute " +
                    "only once per device session.");
            }
            ValidatePrecisionTwoPreviewSetWindow(data);
            RequirePrecisionTwoOperationalIdentity();

            // Mark the allowance consumed before the first data-out USB write.
            // A timeout, disconnect, or malformed completion must never retry it.
            precisionTwoPreviewSetWindowAttempted = true;
            ScsiCommandResult result = ExecuteAcceptedDataOutCommand(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data,
                60000);
            precisionTwoPreviewSetWindowCompleted =
                result.Status.RawStatus == (byte)AdapterStatus.Success &&
                result.Status.Residue == 0 &&
                result.Status.ActualLength ==
                    ScsiFraming.PrecisionTwoPreviewSetWindowLength;
            return result;
        }

        public ScsiCommandResult
            ExecutePrecisionTwoOperationalPreviewCleanupSetWindowOnce(
                byte[] data, int cleanupIndex, int rowsCompleted,
                int imageSubmissionsCompleted)
        {
            EnsureOperational();
            ValidatePrecisionTwoPreviewCleanupProgress(
                precisionTwoPreviewSetWindowCompleted,
                precisionTwoPreviewImageReadsAttempted,
                precisionTwoPreviewCleanupSetWindowsAttempted,
                cleanupIndex, rowsCompleted, imageSubmissionsCompleted);
            PrecisionTwoPreviewCommandManifest.ValidateCleanupSetWindow(
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveCleanupSetWindowSha256);

            // Do not inject a fresh INQUIRY between row 911 and FlexColor's
            // immediate cleanup pair. The exact identity was already bound by
            // the successful initial SET WINDOW in this retained session.

            // Consume the exact ordinal before the first USB write. A failed
            // cleanup command cannot regain or repeat its allowance.
            ++precisionTwoPreviewCleanupSetWindowsAttempted;
            return ExecuteAcceptedDataOutCommand(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data,
                60000);
        }

        public ScsiCommandResult
            ExecutePrecisionTwoOperationalPreviewCancellationCleanupOnce(
                byte[] data, int cleanupIndex, int rowsCompleted,
                int imageSubmissionsCompleted)
        {
            EnsureOperational();
            ValidatePrecisionTwoPreviewCancellationCleanupProgress(
                precisionTwoPreviewSetWindowCompleted,
                precisionTwoPreviewImageReadsAttempted,
                precisionTwoPreviewCleanupSetWindowsAttempted,
                cleanupIndex, rowsCompleted, imageSubmissionsCompleted);
            PrecisionTwoPreviewCommandManifest.ValidateCleanupSetWindow(
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveCleanupSetWindowSha256);

            ++precisionTwoPreviewCleanupSetWindowsAttempted;
            return ExecuteAcceptedDataOutCommand(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data,
                60000);
        }

        public ScsiCommandResult
            ExecutePrecisionTwoOperationalFullScanSetWindowOnce(
                byte[] data, int previewRowsCompleted,
                int previewImageSubmissionsCompleted)
        {
            EnsureOperational();
            ValidatePrecisionTwoFullScanSetWindowProgress(
                precisionTwoPreviewSetWindowCompleted,
                precisionTwoPreviewImageReadsAttempted,
                precisionTwoPreviewCleanupSetWindowsAttempted,
                precisionTwoFullScanSetWindowAttempted,
                previewRowsCompleted, previewImageSubmissionsCompleted);
            PrecisionTwoPreviewCommandManifest.ValidateSetWindow(
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanSetWindowSha256);
            RequirePrecisionTwoOperationalIdentity();

            // Consume before the first USB write. A failed full-scan window
            // cannot be retried or regain its one-shot authority.
            precisionTwoFullScanSetWindowAttempted = true;
            ScsiCommandResult result = ExecuteAcceptedDataOutCommand(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data,
                60000);
            precisionTwoFullScanSetWindowCompleted =
                result.Status.RawStatus == (byte)AdapterStatus.Success &&
                result.Status.Residue == 0 &&
                result.Status.ActualLength ==
                    ScsiFraming.PrecisionTwoPreviewSetWindowLength;
            return result;
        }

        internal static void ValidatePrecisionTwoFullScanSetWindowProgress(
            bool previewSetWindowCompleted, int previewImageAttemptsConsumed,
            int previewCleanupAttemptsConsumed,
            bool fullScanSetWindowAttempted, int previewRowsCompleted,
            int previewImageSubmissionsCompleted)
        {
            if (!previewSetWindowCompleted || fullScanSetWindowAttempted ||
                previewRowsCompleted != PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows ||
                previewImageSubmissionsCompleted < previewRowsCompleted ||
                previewImageSubmissionsCompleted >
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalMaximumReadSubmissions ||
                previewImageAttemptsConsumed !=
                    previewImageSubmissionsCompleted ||
                previewCleanupAttemptsConsumed !=
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewCleanupSetWindowCount)
            {
                throw new InvalidOperationException(
                    "The full-scan SET WINDOW transport is outside its " +
                    "exact completed Preview, cleanup-pair, and one-shot " +
                    "boundary.");
            }
        }

        internal static void ValidatePrecisionTwoPreviewCleanupProgress(
            bool setWindowCompleted, int imageAttemptsConsumed,
            int cleanupAttemptsConsumed, int cleanupIndex,
            int rowsCompleted, int imageSubmissionsCompleted)
        {
            if (!setWindowCompleted ||
                rowsCompleted != PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows ||
                imageSubmissionsCompleted < rowsCompleted ||
                imageSubmissionsCompleted >
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalMaximumReadSubmissions ||
                imageAttemptsConsumed != imageSubmissionsCompleted ||
                cleanupIndex != cleanupAttemptsConsumed ||
                cleanupIndex < 0 ||
                cleanupIndex >= PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewCleanupSetWindowCount)
            {
                throw new InvalidOperationException(
                    "The Preview cleanup transport is outside the exact " +
                    "powered 911-row, two-command sequence boundary.");
            }
        }

        internal static void
            ValidatePrecisionTwoPreviewCancellationCleanupProgress(
                bool setWindowCompleted, int imageAttemptsConsumed,
                int cleanupAttemptsConsumed, int cleanupIndex,
                int rowsCompleted, int imageSubmissionsCompleted)
        {
            if (!setWindowCompleted ||
                rowsCompleted < PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewCancellationTriggerRows ||
                rowsCompleted > PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewCancellationMaximumRows ||
                imageSubmissionsCompleted < rowsCompleted ||
                imageSubmissionsCompleted > checked(rowsCompleted * 9) ||
                imageAttemptsConsumed != imageSubmissionsCompleted ||
                cleanupIndex != cleanupAttemptsConsumed || cleanupIndex < 0 ||
                cleanupIndex >= PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewCleanupSetWindowCount)
            {
                throw new InvalidOperationException(
                    "The Preview cancellation cleanup transport is outside " +
                    "the exact 32--96-row, eight-retries-per-row, " +
                    "two-command sequence boundary.");
            }
        }

        public ScsiCommandResult
            ReadPrecisionTwoOperationalPreviewFirstImageOnce(byte[] cdb,
                uint requestedLength)
        {
            EnsureOperational();
            PrecisionTwoPreviewCommandManifest.ValidateAspiLiveFirstImageRead(
                cdb, requestedLength);
            if (!precisionTwoPreviewSetWindowCompleted)
            {
                throw new InvalidOperationException(
                    "The first Preview image READ requires one exact, fully " +
                    "successful SET WINDOW completion in this device session.");
            }
            if (precisionTwoPreviewImageReadsAttempted != 0)
            {
                throw new InvalidOperationException(
                    "The first Preview image READ transport can execute only " +
                    "once per device session.");
            }

            // Consume the allowance before USB submission. Failed and short
            // completions are observations, never reasons to retry the row.
            ++precisionTwoPreviewImageReadsAttempted;
            return ExecuteAcceptedCommand(5, 0, cdb, requestedLength,
                DataDirection.In, 60000);
        }

        public ScsiCommandResult
            ReadPrecisionTwoOperationalPreviewImageBurstRow(byte[] cdb,
                uint requestedLength, int rowIndex, int maximumRows)
        {
            EnsureOperational();
            PrecisionTwoPreviewCommandManifest.ValidateAspiLiveFirstImageRead(
                cdb, requestedLength);
            if ((maximumRows != PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewBurstRows &&
                 maximumRows != PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamRows) || rowIndex < 0 ||
                rowIndex >= maximumRows)
            {
                throw new InvalidOperationException(
                    "The Preview image transport requires one exact " +
                    "project-owned row boundary.");
            }
            if (!precisionTwoPreviewSetWindowCompleted)
            {
                throw new InvalidOperationException(
                    "The Preview image burst requires one exact, fully " +
                    "successful SET WINDOW completion in this device session.");
            }
            if (precisionTwoPreviewImageReadsAttempted != rowIndex)
            {
                throw new InvalidOperationException(
                    "Preview image burst rows must execute once, in order, " +
                    "without retry or omission.");
            }

            // Consume this row before USB submission. A failed or short row is
            // an observation and cannot be retried within the same session.
            ++precisionTwoPreviewImageReadsAttempted;
            return ExecuteAcceptedCommand(5, 0, cdb, requestedLength,
                DataDirection.In, 60000);
        }

        public ScsiCommandResult
            ReadPrecisionTwoOperationalPreviewImageShortRetryAttempt(
                byte[] cdb, uint requestedLength, int rowIndex,
                int submissionIndex, int maximumRows,
                int maximumSubmissions)
        {
            EnsureOperational();
            PrecisionTwoPreviewCommandManifest.ValidateAspiLiveFirstImageRead(
                cdb, requestedLength);
            bool exactBound = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamMaximumReadSubmissions;
            bool legacyNaturalBound = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalMaximumReadSubmissions;
            bool perRowNaturalBound = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalPerRowMaximumReadSubmissions;
            bool poweredNaturalBound = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalMaximumReadSubmissions;
            if ((!exactBound && !legacyNaturalBound &&
                    !perRowNaturalBound && !poweredNaturalBound) ||
                rowIndex < 0 ||
                rowIndex >= maximumRows || submissionIndex < 0 ||
                submissionIndex >= maximumSubmissions)
            {
                throw new InvalidOperationException(
                    "The Preview short-retry transport requires exact " +
                    "project-owned row and submission boundaries.");
            }
            if (!precisionTwoPreviewSetWindowCompleted)
            {
                throw new InvalidOperationException(
                    "The Preview short-retry stream requires one exact, " +
                    "fully successful SET WINDOW completion in this device " +
                    "session.");
            }
            if (precisionTwoPreviewImageReadsAttempted != submissionIndex)
            {
                throw new InvalidOperationException(
                    "Preview short-retry submissions must execute once and " +
                    "in order without regaining an attempted allowance.");
            }

            // Consume every attempt before USB. The upper gate alone decides
            // whether an exact 10-byte short completion may expose one bounded
            // ASPI BUSY retry without advancing the logical row.
            ++precisionTwoPreviewImageReadsAttempted;
            return ExecuteAcceptedCommand(5, 0, cdb, requestedLength,
                DataDirection.In, 60000);
        }

        public ScsiCommandResult
            ReadPrecisionTwoOperationalFullScanImageShortRetryAttempt(
                byte[] cdb, uint requestedLength, int rowIndex,
                int submissionIndex, int maximumRows,
                int maximumSubmissions, int maximumShortRetries)
        {
            EnsureOperational();
            PrecisionTwoPreviewCommandManifest.
                ValidateAspiLiveFullScanImageRead(cdb, requestedLength);
            ValidatePrecisionTwoFullScanStreamProgress(
                precisionTwoFullScanSetWindowCompleted,
                precisionTwoFullScanImageReadsAttempted,
                precisionTwoFullScanScannerReadyPollsAttempted,
                rowIndex, submissionIndex,
                precisionTwoFullScanScannerReadyPollsAttempted, false,
                maximumRows, maximumSubmissions, maximumShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumScannerReadyPolls);
            if (rowIndex >= maximumRows ||
                submissionIndex >= maximumSubmissions)
            {
                throw new InvalidOperationException(
                    "The full-scan image submission is outside its exact " +
                    "row or one-shot attempt boundary.");
            }

            ++precisionTwoFullScanImageReadsAttempted;
            return ExecuteAcceptedCommand(5, 0, cdb, requestedLength,
                DataDirection.In, 60000);
        }

        public ScsiCommandResult
            ReadPrecisionTwoOperationalFullScanTerminalProbeOnce(
                byte[] cdb, uint requestedLength, int rowsCompleted,
                int submissionIndex)
        {
            EnsureOperational();
            RequirePrecisionTwoOperationalIdentity();
            PrecisionTwoPreviewCommandManifest.
                ValidateAspiLiveFullScanImageRead(cdb, requestedLength);
            ValidatePrecisionTwoFullScanTerminalProbeProgress(
                precisionTwoFullScanSetWindowCompleted,
                precisionTwoFullScanImageReadsAttempted,
                precisionTwoFullScanTerminalProbeAttempted, rowsCompleted,
                submissionIndex);

            // This is the sole post-row probe. Consume it before USB and keep
            // it distinct from ordinary row/retry authorization.
            precisionTwoFullScanTerminalProbeAttempted = true;
            ++precisionTwoFullScanImageReadsAttempted;
            return ExecuteAcceptedCommand(5, 0, cdb, requestedLength,
                DataDirection.In, 60000);
        }

        public ScsiCommandResult
            ExecutePrecisionTwoOperationalFullScanCleanupSetWindowOnce(
                byte[] data, int cleanupIndex, int rowsCompleted,
                int imageSubmissionsCompleted, int maximumRows,
                int maximumSubmissions, int maximumShortRetries,
                int minimumCleanupRows = -1)
        {
            EnsureOperational();
            ValidatePrecisionTwoFullScanStreamProgress(
                precisionTwoFullScanSetWindowCompleted,
                precisionTwoFullScanImageReadsAttempted,
                precisionTwoFullScanScannerReadyPollsAttempted,
                rowsCompleted, imageSubmissionsCompleted,
                precisionTwoFullScanScannerReadyPollsAttempted, false,
                maximumRows, maximumSubmissions, maximumShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumScannerReadyPolls);
            int requiredMinimum = minimumCleanupRows < 0
                ? maximumRows : minimumCleanupRows;
            ValidatePrecisionTwoFullScanCleanupProgress(rowsCompleted,
                maximumRows, maximumSubmissions, maximumShortRetries,
                requiredMinimum, cleanupIndex,
                precisionTwoFullScanCleanupSetWindowsAttempted);
            PrecisionTwoPreviewCommandManifest.ValidateCleanupSetWindow(
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanCleanupSetWindowSha256);

            ++precisionTwoFullScanCleanupSetWindowsAttempted;
            return ExecuteAcceptedDataOutCommand(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data,
                60000);
        }

        internal static void ValidatePrecisionTwoFullScanCleanupProgress(
            int rowsCompleted, int maximumRows, int maximumSubmissions,
            int maximumShortRetries, int minimumCleanupRows,
            int cleanupIndex, int cleanupAttemptsConsumed)
        {
            bool fixedBoundary = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.AspiLiveFullScanRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumReadSubmissions &&
                maximumShortRetries == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumShortRetries ||
                maximumRows == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanNaturalRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanNaturalMaximumReadSubmissions &&
                maximumShortRetries == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanNaturalMaximumShortRetries ||
                maximumRows == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanRow997CompletionRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanRow997CompletionMaximumReadSubmissions &&
                maximumShortRetries == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanRow997CompletionMaximumShortRetries;
            bool progressBoundary = maximumRows ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanProgressMaximumRows &&
                maximumSubmissions == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanProgressMaximumReadSubmissions &&
                maximumShortRetries == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanProgressMaximumShortRetries &&
                minimumCleanupRows == PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanProgressMinimumCleanupRows;
            if ((!fixedBoundary && !progressBoundary) ||
                (fixedBoundary &&
                    (minimumCleanupRows != maximumRows ||
                     rowsCompleted != maximumRows)) ||
                (progressBoundary &&
                    (rowsCompleted < minimumCleanupRows ||
                     rowsCompleted > maximumRows)) ||
                cleanupIndex != cleanupAttemptsConsumed ||
                cleanupIndex < 0 || cleanupIndex >=
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewCleanupSetWindowCount)
            {
                throw new InvalidOperationException(
                    "The full-scan cleanup is outside its exact completed " +
                    "row-range and two-command boundary.");
            }
        }

        public ScsiCommandResult
            ExecutePrecisionTwoLoaderWriteBufferModeOneZeroOnce()
        {
            EnsureOperational();
            RequirePrecisionTwoLoaderIdentity();
            return ExecuteAcceptedCommand(5, 0,
                ScsiFraming.BuildPrecisionTwoLoaderWriteBufferModeOneZeroCdb(),
                0, DataDirection.None, 10000);
        }

        public ScsiCommandResult
            ExecutePrecisionTwoLoaderWriteBufferFirstRecordOnce(byte[] record)
        {
            return ExecutePrecisionTwoLoaderWriteBufferRecordOnce(record, 0);
        }

        public ScsiCommandResult
            ExecutePrecisionTwoLoaderWriteBufferRecordOnce(byte[] record,
                int recordIndex)
        {
            EnsureOperational();
            if (recordIndex < 0 || recordIndex > 15)
            {
                throw new ArgumentOutOfRangeException("recordIndex",
                    "Only the separately gated sixteen loader records exist " +
                    "in this experimental transport path.");
            }
            if (precisionTwoLoaderRecordSequenceFailed ||
                recordIndex != precisionTwoLoaderRecordCount)
            {
                throw new InvalidOperationException(
                    "The loader-record transport is not at the exact requested " +
                    "sequence position.");
            }

            ValidatePrecisionTwoLoaderRecord(record, recordIndex);
            if (recordIndex == 0)
            {
                RequirePrecisionTwoLoaderIdentity();
            }

            try
            {
                ScsiCommandResult result = ExecuteAcceptedDataOutCommand(5, 0,
                    ScsiFraming.BuildPrecisionTwoLoaderFirstRecordCdb(),
                    record, 10000);
                if (result.Status.RawStatus !=
                        (byte)AdapterStatus.Success ||
                    result.Status.Residue != 0)
                {
                    precisionTwoLoaderRecordSequenceFailed = true;
                    return result;
                }
                precisionTwoLoaderRecordCount++;
                return result;
            }
            catch
            {
                precisionTwoLoaderRecordSequenceFailed = true;
                throw;
            }
        }

        public ScsiCommandResult
            ExecutePrecisionTwoLoaderWriteBufferTerminalOnce(byte[] record)
        {
            EnsureOperational();
            if (precisionTwoLoaderRecordSequenceFailed ||
                precisionTwoLoaderRecordCount != 16 ||
                precisionTwoLoaderTerminalConsumed)
            {
                throw new InvalidOperationException(
                    "The loader terminal transport requires sixteen exact " +
                    "successful records and can execute only once.");
            }
            ValidatePrecisionTwoLoaderTerminalRecord(record);

            // This command may start a scanner-side transition. Submit it once,
            // perform one bounded CSW read, and leave all recovery/retry disabled.
            try
            {
                ScsiCommandResult result = ExecuteAcceptedDataOutCommand(5, 0,
                    ScsiFraming.BuildPrecisionTwoLoaderTerminalRecordCdb(),
                    record, 10000);
                if (result.Status.RawStatus !=
                        (byte)AdapterStatus.Success ||
                    result.Status.Residue != 0)
                {
                    precisionTwoLoaderRecordSequenceFailed = true;
                    return result;
                }
                precisionTwoLoaderTerminalConsumed = true;
                return result;
            }
            catch
            {
                precisionTwoLoaderRecordSequenceFailed = true;
                throw;
            }
        }

        private void RequirePrecisionTwoLoaderIdentity()
        {
            InquiryData identity = Inquiry(5, 0);
            if (identity.PeripheralDeviceType != 0x06 ||
                !string.Equals(identity.Vendor, "Imacon",
                    StringComparison.Ordinal) ||
                !string.Equals(identity.Product, "SCSI Loader",
                    StringComparison.Ordinal) ||
                !string.Equals(identity.Revision, "L302",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(string.Format(
                    "The exact target-5 loader identity gate failed: " +
                    "{0} / {1} / {2}, type 0x{3:X2}.", identity.Vendor,
                    identity.Product, identity.Revision,
                    identity.PeripheralDeviceType));
            }
        }

        private void RequirePrecisionTwoOperationalIdentity()
        {
            InquiryData identity = Inquiry(5, 0);
            if (identity.PeripheralDeviceType != 0x06 ||
                !string.Equals(identity.Vendor, "Imacon",
                    StringComparison.Ordinal) ||
                !string.Equals(identity.Product, "FlexTight II",
                    StringComparison.Ordinal) ||
                !string.Equals(identity.Revision, "M333",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(string.Format(
                    "The exact target-5 operational identity gate failed: " +
                    "{0} / {1} / {2}, type 0x{3:X2}.", identity.Vendor,
                    identity.Product, identity.Revision,
                    identity.PeripheralDeviceType));
            }
        }

        private ScsiCommandResult ExecuteAcceptedCommand(byte target, byte lun,
            byte[] cdb, uint requestedLength, DataDirection direction,
            int timeoutMilliseconds)
        {
            return ExecuteAcceptedCommand(target, lun, cdb, requestedLength,
                direction, timeoutMilliseconds, false);
        }

        private ScsiCommandResult ExecuteAcceptedCommand(byte target, byte lun,
            byte[] cdb, uint requestedLength, DataDirection direction,
            int timeoutMilliseconds, bool redactData)
        {
            uint tag = unchecked((uint)Interlocked.Increment(ref nextTag));
            byte[] cbw = ScsiFraming.BuildCommandWrapper(tag, requestedLength,
                direction, target, lun, cdb);
            log.Info(string.Format(
                "SCSI command target={0}, lun={1}, tag={2:X8}, CDB={3}",
                target, lun, tag, ScsiFraming.ToHex(cdb)));
            usb.WritePipe(bulkOut, cbw, timeoutMilliseconds);

            byte[] data = requestedLength == 0
                ? new byte[0]
                : redactData
                    ? usb.ReadPipeRedacted(bulkIn,
                        checked((int)requestedLength), timeoutMilliseconds)
                    : usb.ReadPipe(bulkIn, checked((int)requestedLength),
                        timeoutMilliseconds);
            byte[] cswBytes = usb.ReadPipe(bulkIn,
                ScsiFraming.StatusWrapperLength, timeoutMilliseconds);
            CommandStatusWrapper csw = ScsiFraming.ParseStatusWrapper(
                cswBytes, tag, requestedLength);

            if (data.Length != csw.ActualLength)
            {
                throw new ProtocolException(string.Format(
                    "Data phase length {0} does not match CSW actual length {1}.",
                    data.Length, csw.ActualLength));
            }
            log.Info(string.Format(
                "SCSI status={0:X2}, requested={1}, actual={2}, residue={3}",
                csw.RawStatus, requestedLength, csw.ActualLength, csw.Residue));
            return new ScsiCommandResult(data, csw);
        }

        private ScsiCommandResult ExecuteAcceptedDataOutCommand(byte target,
            byte lun, byte[] cdb, byte[] data, int timeoutMilliseconds)
        {
            return ExecuteAcceptedDataOutCommand(target, lun, cdb, data,
                timeoutMilliseconds, true);
        }

        private ScsiCommandResult ExecuteAcceptedDataOutCommand(byte target,
            byte lun, byte[] cdb, byte[] data, int timeoutMilliseconds,
            bool logPayloadFingerprint)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException(
                    "A data-out command requires a nonempty payload.", "data");
            }
            uint requestedLength = checked((uint)data.Length);
            uint tag = unchecked((uint)Interlocked.Increment(ref nextTag));
            byte[] cbw = ScsiFraming.BuildCommandWrapper(tag, requestedLength,
                DataDirection.Out, target, lun, cdb);
            log.Info(logPayloadFingerprint
                ? string.Format(
                    "SCSI data-out target={0}, lun={1}, tag={2:X8}, CDB={3}, " +
                    "length={4}, sha256={5}", target, lun, tag,
                    ScsiFraming.ToHex(cdb), data.Length, Sha256Hex(data))
                : string.Format(
                    "SCSI pass-through data-out target={0}, lun={1}, " +
                    "tag={2:X8}, CDB={3}, length={4}, data=<redacted>",
                    target, lun, tag, ScsiFraming.ToHex(cdb), data.Length));
            usb.WritePipe(bulkOut, cbw, timeoutMilliseconds);
            usb.WritePipeRedacted(bulkOut, data, timeoutMilliseconds);
            byte[] cswBytes = usb.ReadPipe(bulkIn,
                ScsiFraming.StatusWrapperLength, timeoutMilliseconds);
            CommandStatusWrapper csw = ScsiFraming.ParseStatusWrapper(
                cswBytes, tag, requestedLength);
            log.Info(string.Format(
                "SCSI status={0:X2}, requested={1}, actual={2}, residue={3}",
                csw.RawStatus, requestedLength, csw.ActualLength, csw.Residue));
            return new ScsiCommandResult(new byte[0], csw);
        }

        private static void ValidatePrecisionTwoLoaderRecord(byte[] record,
            int recordIndex)
        {
            PrecisionTwoLoaderManifest.ValidateRecord(recordIndex,
                ScsiFraming.BuildPrecisionTwoLoaderFirstRecordCdb(), record);
        }

        private void ValidatePrecisionTwoPreviewSetWindow(byte[] data)
        {
            try
            {
                PrecisionTwoPreviewCommandManifest.ValidateSetWindow(
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data,
                    precisionTwoPreviewSetWindowSha256);
            }
            catch (ProtocolException ex)
            {
                throw new InvalidOperationException(
                    "Refusing a Preview SET WINDOW payload that does not match " +
                    "the captured length, header, and SHA-256.", ex);
            }
        }

        private static string NormalizeSha256(string value,
            string parameterName)
        {
            if (value == null || value.Length != 64)
            {
                throw new ArgumentException(
                    "Expected a 64-character hexadecimal SHA-256 value.",
                    parameterName);
            }
            for (int index = 0; index < value.Length; ++index)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'A' && character <= 'F') ||
                      (character >= 'a' && character <= 'f')))
                {
                    throw new ArgumentException(
                        "Expected a 64-character hexadecimal SHA-256 value.",
                        parameterName);
                }
            }
            return value.ToUpperInvariant();
        }

        private static void ValidatePrecisionTwoLoaderTerminalRecord(
            byte[] record)
        {
            PrecisionTwoLoaderManifest.ValidateTerminal(
                ScsiFraming.BuildPrecisionTwoLoaderTerminalRecordCdb(),
                record);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
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

        private static string Sha256Hex(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(bytes)).
                    Replace("-", string.Empty);
            }
        }

        private static void RequireSuccess(string operation,
            CommandStatusWrapper status, byte target, byte lun)
        {
            if (status.RawStatus == (byte)AdapterStatus.Success)
            {
                return;
            }
            if (status.RawStatus == (byte)AdapterStatus.Busy)
            {
                throw new ProtocolException(operation + " returned SCSI BUSY (08).");
            }
            if (status.RawStatus == (byte)AdapterStatus.SelectionTimeout)
            {
                throw new ScsiSelectionTimeoutException(
                    operation, target, lun);
            }
            throw new ProtocolException(string.Format(
                "{0} returned unknown adapter status {1:X2}.",
                operation, status.RawStatus));
        }

        private void WriteCpuReset(byte value)
        {
            log.Info(value == 0 ? "Releasing USB2 CPU reset." :
                "Holding USB2 CPU in reset.");
            usb.ControlOut(0xA0, UsbConstants.Usb2CpuControlAddress,
                0, new byte[] { value }, transferTimeoutMilliseconds);
        }

        private static bool IsExpectedDisconnect(int error)
        {
            return error == 31 || error == 433 || error == 995 || error == 1167;
        }

        private void EnsureOperational()
        {
            if (!operationalInitialized)
            {
                throw new InvalidOperationException(
                    "InitializeOperational must be called before SCSI commands.");
            }
        }

        public void Dispose()
        {
            usb.Dispose();
        }
    }
}
