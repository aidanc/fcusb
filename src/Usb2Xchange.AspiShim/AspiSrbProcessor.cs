// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Usb2Xchange.Protocol;
using Usb2Xchange.WinUsb;

namespace Usb2Xchange.AspiShim
{
    public sealed class AspiSrbProcessor
    {
        private readonly IAspiReadOnlyTransport transport;
        private readonly IUsbLog log;
        private readonly bool loaderDataOutEnabled;
        private readonly bool operationalReplayEnabled;
        private readonly bool predictedPreviewObservationEnabled;
        private readonly bool previewSetWindowEnabled;
        private readonly bool startupWriteFingerprintEnabled;
        private readonly bool transparentPassThroughEnabled;
        private readonly object transparentPassThroughSync = new object();
        private readonly string expectedPreviewSetWindowSha256;
        private int previewFingerprintCaptured;
        private int startupWriteFingerprintCaptured;

        public AspiSrbProcessor(IAspiReadOnlyTransport transport)
            : this(transport, null, false)
        {
        }

        internal AspiSrbProcessor(IAspiReadOnlyTransport transport, IUsbLog log)
            : this(transport, log, false)
        {
        }

        internal AspiSrbProcessor(IAspiReadOnlyTransport transport,
            bool loaderDataOutEnabled)
            : this(transport, null, loaderDataOutEnabled)
        {
        }

        internal AspiSrbProcessor(IAspiReadOnlyTransport transport, IUsbLog log,
            bool loaderDataOutEnabled)
            : this(transport, log, loaderDataOutEnabled, false, false)
        {
        }

        internal AspiSrbProcessor(IAspiReadOnlyTransport transport, IUsbLog log,
            bool loaderDataOutEnabled, bool operationalReplayEnabled,
            bool predictedPreviewObservationEnabled)
            : this(transport, log, loaderDataOutEnabled,
                operationalReplayEnabled,
                predictedPreviewObservationEnabled, false,
                PrecisionTwoPreviewCommandManifest.SetWindowSha256)
        {
        }

        internal AspiSrbProcessor(IAspiReadOnlyTransport transport, IUsbLog log,
            bool loaderDataOutEnabled, bool operationalReplayEnabled,
            bool predictedPreviewObservationEnabled,
            bool previewSetWindowEnabled)
            : this(transport, log, loaderDataOutEnabled,
                operationalReplayEnabled,
                predictedPreviewObservationEnabled, previewSetWindowEnabled,
                PrecisionTwoPreviewCommandManifest.SetWindowSha256)
        {
        }

        internal AspiSrbProcessor(IAspiReadOnlyTransport transport, IUsbLog log,
            bool loaderDataOutEnabled, bool operationalReplayEnabled,
            bool predictedPreviewObservationEnabled,
            bool previewSetWindowEnabled,
            string expectedPreviewSetWindowSha256)
            : this(transport, log, loaderDataOutEnabled,
                operationalReplayEnabled,
                predictedPreviewObservationEnabled,
                previewSetWindowEnabled, expectedPreviewSetWindowSha256,
                false)
        {
        }

        internal AspiSrbProcessor(IAspiReadOnlyTransport transport, IUsbLog log,
            bool loaderDataOutEnabled, bool operationalReplayEnabled,
            bool predictedPreviewObservationEnabled,
            bool previewSetWindowEnabled,
            string expectedPreviewSetWindowSha256,
            bool startupWriteFingerprintEnabled)
        {
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }
            this.transport = transport;
            this.log = log;
            this.loaderDataOutEnabled = loaderDataOutEnabled;
            this.operationalReplayEnabled = operationalReplayEnabled;
            this.predictedPreviewObservationEnabled =
                predictedPreviewObservationEnabled;
            this.previewSetWindowEnabled = previewSetWindowEnabled;
            this.startupWriteFingerprintEnabled =
                startupWriteFingerprintEnabled;
            transparentPassThroughEnabled =
                transport is IAspiTransparentPassThroughTransport;
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

        public uint Process(IntPtr srb)
        {
            if (srb == IntPtr.Zero)
            {
                return AspiConstants.StatusInvalidSrb;
            }

            byte command = ReadByte(srb, AspiConstants.CommandOffset);
            if (log != null)
            {
                string address = command == AspiConstants.GetDeviceType ||
                    command == AspiConstants.ExecuteScsiCommand
                    ? string.Format(", target={0}, lun={1}",
                        ReadByte(srb, AspiConstants.TargetOffset),
                        ReadByte(srb, AspiConstants.LunOffset))
                    : string.Empty;
                log.Info(string.Format(
                    "SendASPI32Command: command=0x{0:X2}, adapter={1}, flags=0x{2:X2}{3}.",
                    command,
                    ReadByte(srb, AspiConstants.HostAdapterOffset),
                    ReadByte(srb, AspiConstants.FlagsOffset), address));
            }
            switch (command)
            {
                case AspiConstants.HostAdapterInquiry:
                    return ProcessHostAdapterInquiry(srb);
                case AspiConstants.GetDeviceType:
                    if (transparentPassThroughEnabled)
                    {
                        lock (transparentPassThroughSync)
                        {
                            return ProcessGetDeviceType(srb);
                        }
                    }
                    return ProcessGetDeviceType(srb);
                case AspiConstants.ExecuteScsiCommand:
                    return ProcessExecuteScsi(srb);
                default:
                    if (log != null)
                    {
                        log.Warning(string.Format(
                            "Blocked unsupported ASPI command 0x{0:X2}.", command));
                    }
                    return SetStatus(srb, AspiConstants.StatusInvalidCommand);
            }
        }

        private uint ProcessHostAdapterInquiry(IntPtr srb)
        {
            if (ReadByte(srb, AspiConstants.HostAdapterOffset) != 0)
            {
                return SetStatus(srb, AspiConstants.StatusInvalidHostAdapter);
            }

            WriteByte(srb, 0x08, 1);
            WriteByte(srb, 0x09, 7);
            WriteFixedAscii(srb, 0x0A, 16, "USB2Xchange");
            WriteFixedAscii(srb, 0x1A, 16, "WinUSB ASPI");
            Zero(srb, 0x2A, 18);
            return SetStatus(srb, AspiConstants.StatusComplete);
        }

        private uint ProcessGetDeviceType(IntPtr srb)
        {
            if (!ValidateAddress(srb))
            {
                LogRejectedAddress(srb, "GET_DEV_TYPE");
                return ReadByte(srb, AspiConstants.StatusOffset);
            }

            byte target = ReadByte(srb, AspiConstants.TargetOffset);
            byte lun = ReadByte(srb, AspiConstants.LunOffset);
            byte[] inquiry = ScsiFraming.BuildInquiryCdb(36);
            try
            {
                AspiTransportResult result = transport.Execute(target, lun,
                    inquiry, 36);
                if (result.AdapterStatus == (byte)AdapterStatus.Success &&
                    result.Data.Length != 0)
                {
                    WriteByte(srb, 0x0A, (byte)(result.Data[0] & 0x1F));
                    return SetStatus(srb, AspiConstants.StatusComplete);
                }
                if (result.AdapterStatus ==
                    (byte)AdapterStatus.SelectionTimeout)
                {
                    return SetStatus(srb, AspiConstants.StatusNoDevice);
                }
                return SetStatus(srb, AspiConstants.StatusError);
            }
            catch (Exception ex)
            {
                Warn("GET_DEV_TYPE failed", ex);
                return SetStatus(srb, AspiConstants.StatusError);
            }
        }

        private uint ProcessExecuteScsi(IntPtr srb)
        {
            if (transparentPassThroughEnabled)
            {
                lock (transparentPassThroughSync)
                {
                    return ProcessExecuteScsiWithCompletion(srb);
                }
            }
            return ProcessExecuteScsiWithCompletion(srb);
        }

        private uint ProcessExecuteScsiWithCompletion(IntPtr srb)
        {
            uint status;
            try
            {
                status = ProcessExecuteScsiCore(srb);
            }
            catch (Exception ex)
            {
                Warn("EXEC_SCSI_CMD failed" + DescribeExecuteSrb(srb), ex);
                WriteByte(srb, AspiConstants.HostStatusOffset,
                    AspiConstants.HostStatusOk);
                WriteByte(srb, AspiConstants.TargetStatusOffset,
                    AspiConstants.TargetStatusCheckCondition);
                status = SetStatus(srb, AspiConstants.StatusError);
            }
            finally
            {
                SignalCompletionEvent(srb);
            }
            return status;
        }

        private static string DescribeExecuteSrb(IntPtr srb)
        {
            try
            {
                byte cdbLength = ReadByte(srb, AspiConstants.CdbLengthOffset);
                string cdb = "<invalid-length>";
                if (cdbLength != 0 && cdbLength <= 16)
                {
                    cdb = BitConverter.ToString(ReadBytes(srb,
                        AspiConstants.CdbOffset, cdbLength)).Replace('-', ' ');
                }
                return string.Format(
                    " [target={0}, lun={1}, flags=0x{2:X2}, length={3}, " +
                    "CDB-length={4}, CDB={5}]",
                    ReadByte(srb, AspiConstants.TargetOffset),
                    ReadByte(srb, AspiConstants.LunOffset),
                    ReadByte(srb, AspiConstants.FlagsOffset),
                    ReadUInt32(srb, AspiConstants.BufferLengthOffset),
                    cdbLength, cdb);
            }
            catch
            {
                return " [request metadata unavailable]";
            }
        }

        private void Warn(string context, Exception ex)
        {
            if (log != null)
            {
                log.Warning(context + ": " + ex.GetType().FullName + ": " +
                    ex.Message);
            }
        }

        private uint ProcessExecuteScsiCore(IntPtr srb)
        {
            if (!ValidateAddress(srb))
            {
                LogRejectedAddress(srb, "EXEC_SCSI_CMD");
                return ReadByte(srb, AspiConstants.StatusOffset);
            }

            byte flags = ReadByte(srb, AspiConstants.FlagsOffset);
            uint requestedLength = ReadUInt32(srb,
                AspiConstants.BufferLengthOffset);
            IntPtr buffer = ReadPointer32(srb, AspiConstants.BufferPointerOffset);
            byte cdbLength = ReadByte(srb, AspiConstants.CdbLengthOffset);
            bool directionOut =
                (flags & AspiConstants.FlagDirectionOut) != 0;
            bool directionIn = (flags & AspiConstants.FlagDirectionIn) != 0;

            if (transparentPassThroughEnabled)
            {
                return ProcessTransparentPassThrough(srb, flags,
                    requestedLength, buffer, cdbLength, directionIn,
                    directionOut);
            }

            if (directionOut)
            {
                return ProcessLoaderDataOut(srb, flags, requestedLength,
                    buffer, cdbLength, directionIn);
            }

            uint maximumTransferLength = operationalReplayEnabled ||
                predictedPreviewObservationEnabled
                ? (uint)AspiConstants.MaximumOfflineReplayTransferLength
                : (uint)AspiConstants.MaximumTransferLength;
            if (requestedLength > maximumTransferLength ||
                (cdbLength != 6 && cdbLength != 10) ||
                (requestedLength != 0 &&
                    (!directionIn || buffer == IntPtr.Zero)))
            {
                LogBlocked(srb, flags, requestedLength, cdbLength,
                    "invalid or data-out SRB");
                return SetStatus(srb, AspiConstants.StatusInvalidSrb);
            }

            byte[] cdb = ReadBytes(srb, AspiConstants.CdbOffset, cdbLength);
            if (!ValidateReadOnlyCdb(cdb, requestedLength))
            {
                LogBlocked(srb, flags, requestedLength, cdbLength,
                    string.Format("opcode 0x{0:X2} is outside the read-only allowlist",
                        cdb[0]));
                return SetStatus(srb, AspiConstants.StatusInvalidCommand);
            }

            byte target = ReadByte(srb, AspiConstants.TargetOffset);
            byte lun = ReadByte(srb, AspiConstants.LunOffset);
            AspiTransportResult result = transport.Execute(target, lun, cdb,
                requestedLength);

            if (result.AdapterStatus == (byte)AdapterStatus.Success)
            {
                int copyLength = Math.Min(result.Data.Length,
                    checked((int)requestedLength));
                if (copyLength != 0)
                {
                    Marshal.Copy(result.Data, 0, buffer, copyLength);
                }
            }
            else if (result.AdapterStatus == (byte)AdapterStatus.CheckCondition &&
                cdb[0] != 0x03)
            {
                CopyRequestSense(srb, target, lun);
            }

            return CompleteFromAdapterStatus(srb, result.AdapterStatus);
        }

        private uint ProcessTransparentPassThrough(IntPtr srb, byte flags,
            uint requestedLength, IntPtr buffer, byte cdbLength,
            bool directionIn, bool directionOut)
        {
            const byte allowedFlags = AspiConstants.FlagResidualCount |
                AspiConstants.FlagDirectionIn |
                AspiConstants.FlagDirectionOut |
                AspiConstants.FlagEventNotify;
            if ((flags & ~allowedFlags) != 0 ||
                directionIn && directionOut ||
                requestedLength >
                    AspiConstants.MaximumTrustedTransferLength ||
                !IsSupportedCdbLength(cdbLength) ||
                requestedLength != 0 &&
                    (!directionIn && !directionOut || buffer == IntPtr.Zero))
            {
                LogBlocked(srb, flags, requestedLength, cdbLength,
                    "trusted pass-through SRB is structurally invalid or " +
                    "exceeds the 16 MiB bound");
                return SetStatus(srb, AspiConstants.StatusInvalidSrb);
            }

            byte target = ReadByte(srb, AspiConstants.TargetOffset);
            byte lun = ReadByte(srb, AspiConstants.LunOffset);
            byte[] cdb = ReadBytes(srb, AspiConstants.CdbOffset, cdbLength);
            AspiTransportResult result;
            byte[] output = null;
            try
            {
                if (requestedLength != 0 && directionOut)
                {
                    IAspiDataOutTransport dataOutTransport =
                        transport as IAspiDataOutTransport;
                    if (dataOutTransport == null)
                    {
                        return SetStatus(srb,
                            AspiConstants.StatusInvalidCommand);
                    }
                    output = ReadBytes(buffer, 0,
                        checked((int)requestedLength));
                    result = dataOutTransport.ExecuteDataOut(target, lun,
                        cdb, output);
                }
                else
                {
                    result = transport.Execute(target, lun, cdb,
                        requestedLength);
                }

                if (result.RequestedLength != requestedLength)
                {
                    throw new InvalidOperationException(
                        "Transport result length does not match its ASPI SRB.");
                }
                if (directionIn && result.Data.Length !=
                        result.ActualLength)
                {
                    throw new InvalidOperationException(
                        "Transport data length does not match its residue.");
                }
                if (!directionIn && result.Data.Length != 0)
                {
                    throw new InvalidOperationException(
                        "A non-IN transport returned unexpected data.");
                }

                byte completionStatus = result.AdapterStatus;
                if (completionStatus == (byte)AdapterStatus.Success &&
                    result.Residue != 0 &&
                    (flags & AspiConstants.FlagResidualCount) == 0)
                {
                    completionStatus = (byte)AdapterStatus.Busy;
                    if (log != null)
                    {
                        log.Warning(string.Format(
                            "Trusted pass-through mapped a short successful " +
                            "completion to BUSY because the ASPI SRB did not " +
                            "enable residual count: requested={0}, actual={1}, " +
                            "residue={2}.", result.RequestedLength,
                            result.ActualLength, result.Residue));
                    }
                }

                if (completionStatus == (byte)AdapterStatus.Success &&
                    directionIn && result.Data.Length != 0)
                {
                    Marshal.Copy(result.Data, 0, buffer,
                        result.Data.Length);
                }
                else if (completionStatus ==
                        (byte)AdapterStatus.CheckCondition &&
                    cdb[0] != 0x03)
                {
                    CopyRequestSense(srb, target, lun);
                }

                if ((flags & AspiConstants.FlagResidualCount) != 0)
                {
                    WriteUInt32(srb, AspiConstants.BufferLengthOffset,
                        result.Residue);
                }
                return CompleteFromAdapterStatus(srb,
                    completionStatus);
            }
            finally
            {
                if (output != null)
                {
                    Array.Clear(output, 0, output.Length);
                }
            }
        }

        private uint ProcessLoaderDataOut(IntPtr srb, byte flags,
            uint requestedLength, IntPtr buffer, byte cdbLength,
            bool directionIn)
        {
            byte expectedFlags = AspiConstants.FlagDirectionOut |
                AspiConstants.FlagEventNotify;
            bool loaderEnvelope = loaderDataOutEnabled &&
                (requestedLength ==
                        PrecisionTwoLoaderRecordManifest.RecordLength ||
                 requestedLength ==
                        PrecisionTwoLoaderRecordManifest.TerminalLength);
            bool previewEnvelope =
                (loaderDataOutEnabled || operationalReplayEnabled ||
                    predictedPreviewObservationEnabled ||
                    previewSetWindowEnabled) &&
                requestedLength ==
                    ScsiFraming.PrecisionTwoPreviewSetWindowLength;
            bool startupReplayEnvelope = operationalReplayEnabled &&
                requestedLength ==
                    ScsiFraming.PrecisionTwoStartupWriteBuffer1082Length;
            bool startupFingerprintEnvelope =
                startupWriteFingerprintEnabled && requestedLength ==
                    ScsiFraming.PrecisionTwoStartupWriteBuffer1082Length;
            if ((!loaderEnvelope && !previewEnvelope &&
                    !startupReplayEnvelope &&
                    !startupFingerprintEnvelope) || directionIn ||
                flags != expectedFlags || cdbLength != 10 ||
                buffer == IntPtr.Zero)
            {
                LogBlocked(srb, flags, requestedLength, cdbLength,
                    "approved data-out is disabled or outside its exact SRB envelope");
                return SetStatus(srb, AspiConstants.StatusInvalidSrb);
            }

            byte[] cdb = ReadBytes(srb, AspiConstants.CdbOffset, cdbLength);
            bool record = requestedLength ==
                PrecisionTwoLoaderRecordManifest.RecordLength;
            bool previewSetWindow = requestedLength ==
                ScsiFraming.PrecisionTwoPreviewSetWindowLength;
            bool startupReplay = requestedLength ==
                ScsiFraming.PrecisionTwoStartupWriteBuffer1082Length;
            byte[] expected = startupReplay
                ? ScsiFraming.BuildPrecisionTwoStartupWriteBuffer1082Cdb()
                : previewSetWindow
                ? ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb()
                : record
                    ? ScsiFraming.BuildPrecisionTwoLoaderFirstRecordCdb()
                    : ScsiFraming.BuildPrecisionTwoLoaderTerminalRecordCdb();
            if (!Matches(cdb, expected))
            {
                LogBlocked(srb, flags, requestedLength, cdbLength,
                    "data-out is outside the exact loader/Preview CDB forms");
                return SetStatus(srb, AspiConstants.StatusInvalidCommand);
            }

            bool previewFingerprintOnly = previewSetWindow &&
                PreviewFingerprintOnly;
            if (previewFingerprintOnly && Interlocked.CompareExchange(
                    ref previewFingerprintCaptured, 1, 0) != 0)
            {
                LogBlocked(srb, flags, requestedLength, cdbLength,
                    "Preview SET WINDOW fingerprint observation was already " +
                    "consumed; request remains blocked before USB");
                return SetStatus(srb, AspiConstants.StatusInvalidCommand);
            }

            IAspiDataOutTransport dataOutTransport =
                transport as IAspiDataOutTransport;
            if (dataOutTransport == null)
            {
                LogBlocked(srb, flags, requestedLength, cdbLength,
                    "the selected transport has no data-out capability");
                return SetStatus(srb, AspiConstants.StatusInvalidCommand);
            }

            byte[] data = ReadBytes(buffer, 0, checked((int)requestedLength));
            string previewSetWindowSha256 = null;
            try
            {
                if (startupReplay && startupWriteFingerprintEnabled)
                {
                    IAspiStartupWriteFingerprintPolicy fingerprintPolicy =
                        transport as IAspiStartupWriteFingerprintPolicy;
                    if (fingerprintPolicy == null)
                    {
                        LogBlocked(srb, flags, requestedLength, cdbLength,
                            "the transport has no startup-write fingerprint " +
                            "policy");
                        return SetStatus(srb,
                            AspiConstants.StatusInvalidCommand);
                    }
                    if (Interlocked.CompareExchange(
                            ref startupWriteFingerprintCaptured, 1, 0) != 0)
                    {
                        LogBlocked(srb, flags, requestedLength, cdbLength,
                            "startup WRITE BUFFER fingerprint observation " +
                            "was already consumed; request remains blocked " +
                            "before USB");
                        return SetStatus(srb,
                            AspiConstants.StatusInvalidCommand);
                    }
                    fingerprintPolicy.ObserveStartupWriteFingerprint(
                        ReadByte(srb, AspiConstants.TargetOffset),
                        ReadByte(srb, AspiConstants.LunOffset), cdb, data);
                    LogBlocked(srb, flags, requestedLength, cdbLength,
                        "startup WRITE BUFFER fingerprint captured and " +
                        "blocked before USB; payload-sha256=" +
                        Sha256Hex(data));
                    return SetStatus(srb,
                        AspiConstants.StatusInvalidCommand);
                }
                if (previewSetWindow)
                {
                    IAspiPreviewSetWindowValidationPolicy validationPolicy =
                        transport as
                            IAspiPreviewSetWindowValidationPolicy;
                    bool statefulPreviewValidation =
                        previewSetWindowEnabled &&
                        validationPolicy != null &&
                        validationPolicy.
                            StatefulPreviewSetWindowValidationEnabled;
                    try
                    {
                        previewSetWindowSha256 =
                            PrecisionTwoPreviewCommandManifest.
                                FingerprintSetWindow(cdb, data);
                        if (log != null)
                        {
                            log.Info("Preview SET WINDOW structured metadata " +
                                "captured without retaining the payload: " +
                                PrecisionTwoPreviewCommandManifest.
                                    DescribeSetWindow(cdb, data));
                        }
                        if (previewFingerprintOnly)
                        {
                            LogBlocked(srb, flags, requestedLength, cdbLength,
                                "Preview SET WINDOW fingerprint captured and " +
                                "blocked before USB; payload-sha256=" +
                                previewSetWindowSha256);
                            return SetStatus(srb,
                                AspiConstants.StatusInvalidCommand);
                        }
                        if ((operationalReplayEnabled &&
                             !loaderDataOutEnabled &&
                             !previewSetWindowEnabled) ||
                            statefulPreviewValidation)
                        {
                            PrecisionTwoPreviewCommandManifest.
                                ValidateSetWindowEnvelope(cdb, data);
                        }
                        else
                        {
                            PrecisionTwoPreviewCommandManifest.
                                ValidateSetWindow(cdb, data,
                                    expectedPreviewSetWindowSha256);
                        }
                        if (log != null)
                        {
                            log.Info("Preview SET WINDOW candidate validated " +
                                "for transport; payload-sha256=" +
                                previewSetWindowSha256);
                        }
                    }
                    catch (ProtocolException)
                    {
                        string fingerprint = previewSetWindowSha256 == null
                            ? string.Empty
                            : "; payload-sha256=" +
                                previewSetWindowSha256;
                        LogBlocked(srb, flags, requestedLength, cdbLength,
                            previewFingerprintOnly
                                ? "Preview SET WINDOW fingerprint envelope " +
                                    "was invalid and blocked before USB" +
                                    fingerprint
                                : operationalReplayEnabled &&
                                    !loaderDataOutEnabled &&
                                    !previewSetWindowEnabled
                                    ? "Preview SET WINDOW is outside the " +
                                        "exact offline 84-byte envelope" +
                                        fingerprint
                                    : "Preview SET WINDOW payload is not the " +
                                        "pinned capture" + fingerprint);
                        return SetStatus(srb,
                            AspiConstants.StatusInvalidCommand);
                    }
                }
                AspiTransportResult result;
                try
                {
                    result = dataOutTransport.ExecuteDataOut(
                        ReadByte(srb, AspiConstants.TargetOffset),
                        ReadByte(srb, AspiConstants.LunOffset), cdb, data);
                }
                catch (Exception ex)
                {
                    if (previewSetWindow && log != null)
                    {
                        bool cleanupCandidate = string.Equals(
                            previewSetWindowSha256,
                            PrecisionTwoPreviewCommandManifest.
                                AspiLiveCleanupSetWindowSha256,
                            StringComparison.Ordinal);
                        log.Warning((cleanupCandidate
                            ? "Post-image cleanup SET WINDOW transport failed "
                            : "Preview SET WINDOW transport failed ") +
                            "after candidate validation; payload-sha256=" +
                            previewSetWindowSha256 + "; error=" +
                            ex.GetType().FullName + ": " + ex.Message);
                    }
                    throw;
                }
                return CompleteFromAdapterStatus(srb, result.AdapterStatus);
            }
            finally
            {
                Array.Clear(data, 0, data.Length);
            }
        }

        private void LogRejectedAddress(IntPtr srb, string command)
        {
            if (log != null)
            {
                log.Warning(string.Format(
                    "Rejected {0} address: adapter={1}, target={2}, lun={3}; " +
                    "the provider advertises only adapter 0 and LUN 0.",
                    command,
                    ReadByte(srb, AspiConstants.HostAdapterOffset),
                    ReadByte(srb, AspiConstants.TargetOffset),
                    ReadByte(srb, AspiConstants.LunOffset)));
            }
        }

        private void LogBlocked(IntPtr srb, byte flags, uint requestedLength,
            byte cdbLength, string reason)
        {
            if (log != null)
            {
                string cdb = cdbLength >= 1 && cdbLength <= 16
                    ? BitConverter.ToString(ReadBytes(srb,
                        AspiConstants.CdbOffset, cdbLength)).Replace('-', ' ')
                    : "unavailable";
                log.Warning(string.Format(
                    "Blocked ASPI SCSI request: target={0}, lun={1}, flags=0x{2:X2}, " +
                    "length={3}, CDB-length={4}, CDB={5}; {6}.",
                    ReadByte(srb, AspiConstants.TargetOffset),
                    ReadByte(srb, AspiConstants.LunOffset), flags,
                    requestedLength, cdbLength, cdb, reason));
            }
        }

        private void CopyRequestSense(IntPtr srb, byte target, byte lun)
        {
            byte senseLength = ReadByte(srb, AspiConstants.SenseLengthOffset);
            int length = Math.Min((int)senseLength,
                AspiConstants.MaximumSenseLength);
            if (length == 0)
            {
                return;
            }

            AspiTransportResult sense = transport.Execute(target, lun,
                ScsiFraming.BuildRequestSenseCdb((byte)length), (uint)length);
            if (sense.AdapterStatus != (byte)AdapterStatus.Success)
            {
                return;
            }
            int copyLength = Math.Min(length, sense.Data.Length);
            for (int i = 0; i < copyLength; i++)
            {
                WriteByte(srb, AspiConstants.SenseAreaOffset + i,
                    sense.Data[i]);
            }
        }

        private bool ValidateReadOnlyCdb(byte[] cdb, uint requestedLength)
        {
            switch (cdb[0])
            {
                case 0x00:
                    return requestedLength == 0;
                case 0x03:
                case 0x12:
                    return requestedLength != 0 && requestedLength <= 255 &&
                        cdb[4] == requestedLength;
                case 0x3C:
                    return requestedLength ==
                            ScsiFraming.PrecisionTwoLoaderBufferD8Length &&
                        Matches(cdb,
                            ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb()) ||
                        (loaderDataOutEnabled || operationalReplayEnabled ||
                            predictedPreviewObservationEnabled ||
                            previewSetWindowEnabled) &&
                        (requestedLength ==
                                ScsiFraming.PrecisionTwoFaultPixelBufferLength &&
                            Matches(cdb, ScsiFraming.
                                BuildPrecisionTwoFaultPixelReadBufferCdb()) ||
                         requestedLength ==
                                ScsiFraming.PrecisionTwoFaultPixelDataLength &&
                            Matches(cdb, ScsiFraming.
                                BuildPrecisionTwoFaultPixelDataReadBufferCdb()) ||
                         requestedLength ==
                                ScsiFraming.PrecisionTwoCalibrationBufferLength &&
                            Matches(cdb, ScsiFraming.
                                BuildPrecisionTwoCalibrationReadBufferCdb()) ||
                         requestedLength ==
                                ScsiFraming.PrecisionTwoD8Offset55Length &&
                            Matches(cdb, ScsiFraming.
                                BuildPrecisionTwoD8Offset55ReadBufferCdb()) ||
                         requestedLength == ScsiFraming.
                                PrecisionTwoDynamicConfigurationLength &&
                            Matches(cdb, ScsiFraming.
                                BuildPrecisionTwoDynamicConfigurationD8ReadBufferCdb()) ||
                         operationalReplayEnabled &&
                            requestedLength == ScsiFraming.
                                PrecisionTwoStartupReadBufferE01072Length &&
                            Matches(cdb, ScsiFraming.
                                BuildPrecisionTwoStartupReadBufferE01072Cdb()));
                case 0xDF:
                    return requestedLength ==
                            ScsiFraming.PrecisionTwoScannerReadyLength &&
                        Matches(cdb,
                            ScsiFraming.BuildPrecisionTwoScannerReadyCdb());
                case 0x28:
                    if (!predictedPreviewObservationEnabled ||
                        PreviewFingerprintOnly)
                    {
                        return false;
                    }
                    try
                    {
                        if (operationalReplayEnabled)
                        {
                            PrecisionTwoPreviewCommandManifest.
                                ValidateOfflineImageRead(cdb,
                                    requestedLength);
                        }
                        else
                        {
                            PrecisionTwoPreviewCommandManifest.
                                ValidatePredictedImageRead(cdb,
                                    requestedLength);
                        }
                        return true;
                    }
                    catch (ProtocolException)
                    {
                        return false;
                    }
                default:
                    return false;
            }
        }

        private bool PreviewFingerprintOnly
        {
            get
            {
                return predictedPreviewObservationEnabled &&
                    !operationalReplayEnabled && !loaderDataOutEnabled &&
                    !previewSetWindowEnabled;
            }
        }

        private static string Sha256Hex(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(
                    data ?? new byte[0])).Replace("-", string.Empty);
            }
        }

        private static bool Matches(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }
            int difference = 0;
            for (int i = 0; i < left.Length; ++i)
            {
                difference |= left[i] ^ right[i];
            }
            return difference == 0;
        }

        private static bool IsSupportedCdbLength(byte cdbLength)
        {
            return cdbLength == 6 || cdbLength == 10 ||
                cdbLength == 12 || cdbLength == 16;
        }

        private static bool ValidateAddress(IntPtr srb)
        {
            if (ReadByte(srb, AspiConstants.HostAdapterOffset) != 0)
            {
                SetStatus(srb, AspiConstants.StatusInvalidHostAdapter);
                return false;
            }
            if (ReadByte(srb, AspiConstants.TargetOffset) > 6 ||
                ReadByte(srb, AspiConstants.LunOffset) != 0)
            {
                SetStatus(srb, AspiConstants.StatusNoDevice);
                return false;
            }
            return true;
        }

        private static uint CompleteFromAdapterStatus(IntPtr srb,
            byte adapterStatus)
        {
            WriteByte(srb, AspiConstants.HostStatusOffset,
                AspiConstants.HostStatusOk);
            WriteByte(srb, AspiConstants.TargetStatusOffset,
                AspiConstants.TargetStatusGood);

            switch (adapterStatus)
            {
                case (byte)AdapterStatus.Success:
                    return SetStatus(srb, AspiConstants.StatusComplete);
                case (byte)AdapterStatus.CheckCondition:
                    WriteByte(srb, AspiConstants.TargetStatusOffset,
                        AspiConstants.TargetStatusCheckCondition);
                    return SetStatus(srb, AspiConstants.StatusError);
                case (byte)AdapterStatus.Busy:
                    WriteByte(srb, AspiConstants.TargetStatusOffset,
                        AspiConstants.TargetStatusBusy);
                    return SetStatus(srb, AspiConstants.StatusError);
                case (byte)AdapterStatus.SelectionTimeout:
                    WriteByte(srb, AspiConstants.HostStatusOffset,
                        AspiConstants.HostStatusSelectionTimeout);
                    return SetStatus(srb, AspiConstants.StatusNoDevice);
                default:
                    return SetStatus(srb, AspiConstants.StatusError);
            }
        }

        private static void SignalCompletionEvent(IntPtr srb)
        {
            byte flags = ReadByte(srb, AspiConstants.FlagsOffset);
            if ((flags & AspiConstants.FlagEventNotify) == 0)
            {
                return;
            }
            IntPtr eventHandle = ReadPointer32(srb,
                AspiConstants.PostProcedureOffset);
            if (eventHandle != IntPtr.Zero)
            {
                NativeMethods.SetEvent(eventHandle);
            }
        }

        private static uint SetStatus(IntPtr srb, byte status)
        {
            WriteByte(srb, AspiConstants.StatusOffset, status);
            return status;
        }

        private static byte ReadByte(IntPtr pointer, int offset)
        {
            return Marshal.ReadByte(pointer, offset);
        }

        private static void WriteByte(IntPtr pointer, int offset, byte value)
        {
            Marshal.WriteByte(pointer, offset, value);
        }

        private static uint ReadUInt32(IntPtr pointer, int offset)
        {
            return unchecked((uint)Marshal.ReadInt32(pointer, offset));
        }

        private static void WriteUInt32(IntPtr pointer, int offset,
            uint value)
        {
            Marshal.WriteInt32(pointer, offset, unchecked((int)value));
        }

        private static IntPtr ReadPointer32(IntPtr pointer, int offset)
        {
            return new IntPtr(Marshal.ReadInt32(pointer, offset));
        }

        private static byte[] ReadBytes(IntPtr pointer, int offset, int length)
        {
            byte[] result = new byte[length];
            Marshal.Copy(IntPtr.Add(pointer, offset), result, 0, length);
            return result;
        }

        private static void WriteFixedAscii(IntPtr pointer, int offset,
            int length, string value)
        {
            Zero(pointer, offset, length);
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            int count = Math.Min(length, bytes.Length);
            for (int i = 0; i < count; i++)
            {
                WriteByte(pointer, offset + i, bytes[i]);
            }
        }

        private static void Zero(IntPtr pointer, int offset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                WriteByte(pointer, offset + i, 0);
            }
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetEvent(IntPtr eventHandle);
        }
    }
}
