// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Usb2Xchange.Protocol;
using Usb2Xchange.WinUsb;

namespace Usb2Xchange.AspiShim
{
    public sealed class AspiTransportResult
    {
        public AspiTransportResult(byte[] data, byte adapterStatus)
            : this(data, adapterStatus,
                checked((uint)(data == null ? 0 : data.Length)), 0)
        {
        }

        public AspiTransportResult(byte[] data, byte adapterStatus,
            uint requestedLength, uint residue)
        {
            if (residue > requestedLength)
            {
                throw new ArgumentOutOfRangeException("residue");
            }
            Data = data ?? new byte[0];
            AdapterStatus = adapterStatus;
            RequestedLength = requestedLength;
            Residue = residue;
        }

        public byte[] Data { get; private set; }
        public byte AdapterStatus { get; private set; }
        public uint RequestedLength { get; private set; }
        public uint Residue { get; private set; }
        public uint ActualLength { get { return RequestedLength - Residue; } }
    }

    public interface IAspiReadOnlyTransport
    {
        AspiTransportResult Execute(byte target, byte lun, byte[] cdb,
            uint requestedLength);
    }

    public interface IAspiDataOutTransport : IAspiReadOnlyTransport
    {
        AspiTransportResult ExecuteDataOut(byte target, byte lun, byte[] cdb,
            byte[] data);
    }

    // Marker for the separately trust-gated FlexColor pass-through. The SRB
    // processor uses this only to select structural ASPI validation instead
    // of the historical scanner-command research allowlists.
    internal interface IAspiTransparentPassThroughTransport :
        IAspiDataOutTransport
    {
    }

    internal interface IAspiPreviewSetWindowValidationPolicy
    {
        bool StatefulPreviewSetWindowValidationEnabled { get; }
    }

    internal interface IAspiStartupWriteFingerprintPolicy
    {
        void ObserveStartupWriteFingerprint(byte target, byte lun,
            byte[] cdb, byte[] data);
    }

    public sealed class OfflineReplayAspiTransport :
        IAspiOperatorCycleTransport
    {
        private enum ReplayContinuationState
        {
            AwaitExtraScannerReady,
            AwaitStartupWriteBuffer1082,
            AwaitStartupReadBufferE01072,
            AwaitIdentifierOrCompletionScannerReady,
            AwaitPostIdentifierScannerReady,
            StartupConfigurationComplete
        }

        private readonly object sync = new object();
        private readonly IUsbLog log;
        private readonly AspiOperationalSequenceGate operationalGate;
        private readonly byte[] replayIdentifier;
        private ReplayContinuationState continuationState =
            ReplayContinuationState.AwaitExtraScannerReady;
        private byte[] startupVerificationData;
        private int completedStartupConfigurationCycles;
        private int completedStartupIdentifierReads;
        private int syntheticImageRows;
        private bool repeatScanInquiryObserved;
        private readonly int replayImageRowDelayMilliseconds;
        private readonly bool warmOperatorCycle;
        private bool disposed;

        public OfflineReplayAspiTransport()
            : this(null)
        {
        }

        internal OfflineReplayAspiTransport(IUsbLog log)
            : this(log, PrecisionTwoPreviewCommandManifest.SetWindowSha256)
        {
        }

        internal OfflineReplayAspiTransport(IUsbLog log,
            string expectedPreviewSetWindowSha256)
            : this(log, expectedPreviewSetWindowSha256, null)
        {
        }

        internal OfflineReplayAspiTransport(IUsbLog log,
            string expectedPreviewSetWindowSha256, string scannerIdentifier)
            : this(log, expectedPreviewSetWindowSha256, scannerIdentifier, 0)
        {
        }

        internal OfflineReplayAspiTransport(IUsbLog log,
            string expectedPreviewSetWindowSha256, string scannerIdentifier,
            int imageRowDelayMilliseconds)
            : this(log, expectedPreviewSetWindowSha256, scannerIdentifier,
                imageRowDelayMilliseconds, false)
        {
        }

        internal OfflineReplayAspiTransport(IUsbLog log,
            string expectedPreviewSetWindowSha256, string scannerIdentifier,
            int imageRowDelayMilliseconds, bool beginWarmOperatorCycle)
        {
            if (imageRowDelayMilliseconds < 0 ||
                imageRowDelayMilliseconds > 100)
            {
                throw new ArgumentOutOfRangeException(
                    "imageRowDelayMilliseconds");
            }
            this.log = log;
            replayImageRowDelayMilliseconds = imageRowDelayMilliseconds;
            warmOperatorCycle = beginWarmOperatorCycle;
            replayIdentifier = ParseReplayIdentifier(scannerIdentifier);
            operationalGate = new AspiOperationalSequenceGate(
                GetReplayMilliseconds, expectedPreviewSetWindowSha256, true);
            if (warmOperatorCycle)
            {
                operationalGate.BeginOfflineWarmOperatorCycle();
                Info("OFFLINE REPLAY seeded one bounded warm operator cycle " +
                    "with exactly three remaining bounded initialization " +
                    "and image-stream phases; no WinUSB call.");
            }
        }

        public AspiTransportResult Execute(byte target, byte lun, byte[] cdb,
            uint requestedLength)
        {
            lock (sync)
            {
                RequireNotDisposed();
                if (cdb == null)
                {
                    throw new ArgumentNullException("cdb");
                }
                if (log != null)
                {
                    log.Info(string.Format(
                        "OFFLINE REPLAY request: target={0}, lun={1}, " +
                        "length={2}, CDB={3}; no WinUSB call.", target, lun,
                        requestedLength,
                        BitConverter.ToString(cdb).Replace('-', ' ')));
                }
                if (target != 5 || lun != 0)
                {
                    return new AspiTransportResult(new byte[0],
                        (byte)AdapterStatus.SelectionTimeout);
                }
                if (cdb.Length == 6 && cdb[0] == 0x12 &&
                    requestedLength != 0 && requestedLength <= 255 &&
                    cdb[4] == requestedLength)
                {
                    byte[] inquiry = BuildSyntheticOperationalInquiry(
                        checked((int)requestedLength));
                    if (operationalGate.State ==
                            AspiOperationalSequenceState.Complete &&
                        (operationalGate.CompletedOfflineImageStreams == 1 ||
                         warmOperatorCycle && operationalGate.
                            CompletedOfflineImageStreams == 0))
                    {
                        if (repeatScanInquiryObserved)
                        {
                            throw new InvalidOperationException(
                                "Offline replay observed more than one INQUIRY " +
                                "before the bounded repeat-scan initialization.");
                        }
                        repeatScanInquiryObserved = true;
                        Info("OFFLINE REPLAY observed the exact INQUIRY " +
                            "prefix for one bounded repeat scan.");
                    }
                    Info("OFFLINE REPLAY synthetic INQUIRY returned; " +
                        "identity=Imacon/FlexTight II/M333.");
                    return Success(inquiry);
                }
                if (cdb.Length == 6 && cdb[0] == 0x00 &&
                    requestedLength == 0)
                {
                    return Success(new byte[0]);
                }
                if (cdb.Length == 6 && cdb[0] == 0x03 &&
                    requestedLength != 0 && requestedLength <= 255 &&
                    cdb[4] == requestedLength)
                {
                    byte[] sense = new byte[requestedLength];
                    sense[0] = 0x70;
                    return Success(sense);
                }
                if (Matches(cdb,
                        ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb()) &&
                    requestedLength ==
                        ScsiFraming.PrecisionTwoLoaderBufferD8Length)
                {
                    byte[] d8 = BuildSyntheticOperationalD8Response();
                    operationalGate.RecordOperationalD8Completion(
                        (byte)AdapterStatus.Success, 0, d8.Length, d8);
                    Info("OFFLINE REPLAY accepted operational D8 and armed " +
                        "the initialization state machine.");
                    return Success(d8);
                }
                if (Matches(cdb,
                        ScsiFraming.BuildPrecisionTwoScannerReadyCdb()) &&
                    requestedLength ==
                        ScsiFraming.PrecisionTwoScannerReadyLength &&
                    operationalGate.State ==
                        AspiOperationalSequenceState.Complete)
                {
                    if (!repeatScanInquiryObserved)
                    {
                        throw new InvalidOperationException(
                            "Offline repeat scan did not begin with its exact " +
                            "INQUIRY prefix.");
                    }
                    operationalGate.BeginOfflineRepeatScanInitialization();
                    repeatScanInquiryObserved = false;
                    syntheticImageRows = 0;
                    Info("OFFLINE REPLAY armed exactly one repeat scan after " +
                        "the completed Preview and exact INQUIRY prefix.");
                }
                if (Matches(cdb,
                        ScsiFraming.BuildPrecisionTwoScannerReadyCdb()) &&
                    requestedLength ==
                        ScsiFraming.PrecisionTwoScannerReadyLength &&
                    operationalGate.State == AspiOperationalSequenceState.
                        PreviewSetWindowReady &&
                    (continuationState == ReplayContinuationState.
                        AwaitExtraScannerReady ||
                     continuationState == ReplayContinuationState.
                        AwaitIdentifierOrCompletionScannerReady ||
                     continuationState == ReplayContinuationState.
                        AwaitPostIdentifierScannerReady))
                {
                    if (continuationState == ReplayContinuationState.
                            AwaitExtraScannerReady)
                    {
                        continuationState = ReplayContinuationState.
                            AwaitStartupWriteBuffer1082;
                        Info("OFFLINE REPLAY accepted the post-initialization " +
                            "ScannerReady observed before the 1,082-byte " +
                            "startup WRITE BUFFER.");
                    }
                    else
                    {
                        continuationState = ReplayContinuationState.
                            StartupConfigurationComplete;
                        Info("OFFLINE REPLAY accepted the startup " +
                            "configuration completion ScannerReady.");
                    }
                    return Success(new byte[requestedLength]);
                }
                if (Matches(cdb, ScsiFraming.
                        BuildPrecisionTwoD8Offset55ReadBufferCdb()) &&
                    requestedLength ==
                        ScsiFraming.PrecisionTwoD8Offset55Length &&
                    operationalGate.State == AspiOperationalSequenceState.
                        PreviewSetWindowReady &&
                    continuationState == ReplayContinuationState.
                        AwaitIdentifierOrCompletionScannerReady)
                {
                    ++completedStartupIdentifierReads;
                    if (completedStartupIdentifierReads == 2)
                    {
                        continuationState = ReplayContinuationState.
                            AwaitPostIdentifierScannerReady;
                    }
                    Info(string.Format(
                        "OFFLINE REPLAY returned synthetic ten-byte D8 " +
                        "offset-55 identifier read {0}/2, matching the " +
                        "initial D8 field.", completedStartupIdentifierReads));
                    return Success(BuildSyntheticScannerIdentifier());
                }
                if (Matches(cdb, ScsiFraming.
                        BuildPrecisionTwoStartupReadBufferE01072Cdb()) &&
                    requestedLength == ScsiFraming.
                        PrecisionTwoStartupReadBufferE01072Length &&
                    continuationState == ReplayContinuationState.
                        AwaitStartupReadBufferE01072)
                {
                    if (startupVerificationData == null ||
                        startupVerificationData.Length != requestedLength)
                    {
                        throw new InvalidOperationException(
                            "The startup verification read has no exact " +
                            "preceding write payload.");
                    }
                    byte[] response = startupVerificationData;
                    startupVerificationData = null;
                    continuationState = ReplayContinuationState.
                        AwaitIdentifierOrCompletionScannerReady;
                    ++completedStartupConfigurationCycles;
                    Info(string.Format(
                        "OFFLINE REPLAY completed startup E0 verification " +
                        "read {0} by echoing the preceding write payload " +
                        "after its ten-byte record header.",
                        completedStartupConfigurationCycles));
                    return Success(response);
                }
                if (IsOperationalRead(cdb))
                {
                    if (continuationState == ReplayContinuationState.
                            StartupConfigurationComplete &&
                        Matches(cdb, ScsiFraming.
                            BuildPrecisionTwoFaultPixelReadBufferCdb()))
                    {
                        if (replayImageRowDelayMilliseconds != 0 &&
                            operationalGate.State ==
                                AspiOperationalSequenceState.
                                    PreviewSetWindowReady &&
                            operationalGate.CompletedInitializationCycles ==
                                AspiOperationalSequenceGate.
                                    MaximumInitializationCycles)
                        {
                            operationalGate.
                                BeginOfflinePostCancellationRecoveryCycle();
                            Info("OFFLINE CANCEL REPLAY armed the observed " +
                                "post-cancellation seventh fault/calibration " +
                                "cycle after startup verification.");
                        }
                        continuationState = ReplayContinuationState.
                            AwaitExtraScannerReady;
                        completedStartupIdentifierReads = 0;
                        Info("OFFLINE REPLAY entered the next bounded " +
                            "fault/calibration and startup-configuration " +
                            "stabilization cycle.");
                    }
                    AspiOperationalReadCommand command =
                        operationalGate.BeginRead(target, lun, cdb,
                            requestedLength);
                    byte[] data = BuildOperationalResponse(command,
                        requestedLength);
                    operationalGate.CompleteRead(
                        (byte)AdapterStatus.Success, 0, data.Length, data);
                    Info(string.Format(
                        "OFFLINE REPLAY completed synthetic {0}; cycles={1}, " +
                        "next={2}.", command,
                        operationalGate.CompletedInitializationCycles,
                        operationalGate.State));
                    return Success(data);
                }
                if (cdb.Length == 10 && cdb[0] == 0x28)
                {
                    uint scanWidth = operationalGate.
                        BeginOfflinePreviewImageRead(target, lun, cdb,
                            requestedLength);
                    byte[] row = BuildSyntheticImageRow(requestedLength,
                        syntheticImageRows);
                    ++syntheticImageRows;
                    if (replayImageRowDelayMilliseconds != 0)
                    {
                        Thread.Sleep(replayImageRowDelayMilliseconds);
                        Info(string.Format(
                            "OFFLINE CANCEL REPLAY completed synthetic " +
                            "Preview image row {0}: stream={1}, " +
                            "selector=0x{2:X2}, width={3}, bytes={4}, " +
                            "delay-ms={5}; no WinUSB call.",
                            syntheticImageRows,
                            operationalGate.CompletedOfflineImageStreams + 1,
                            cdb[4], scanWidth, requestedLength,
                            replayImageRowDelayMilliseconds));
                    }
                    else if (syntheticImageRows == 1 ||
                        syntheticImageRows % 100 == 0)
                    {
                        Info(string.Format(
                            "OFFLINE REPLAY returned deterministic synthetic " +
                            "Preview image row {0}: selector=0x{1:X2}, " +
                            "width={2}, bytes={3}; no WinUSB call.",
                            syntheticImageRows, cdb[4], scanWidth,
                            requestedLength));
                    }
                    return Success(row);
                }
                throw new InvalidOperationException(
                    "Offline replay received a command outside its exact " +
                    "operational state machine.");
            }
        }

        public AspiTransportResult ExecuteDataOut(byte target, byte lun,
            byte[] cdb, byte[] data)
        {
            lock (sync)
            {
                RequireNotDisposed();
                if (target == 5 && lun == 0 && Matches(cdb, ScsiFraming.
                        BuildPrecisionTwoStartupWriteBuffer1082Cdb()) &&
                    data != null && data.Length == ScsiFraming.
                        PrecisionTwoStartupWriteBuffer1082Length &&
                    continuationState == ReplayContinuationState.
                        AwaitStartupWriteBuffer1082)
                {
                    startupVerificationData = new byte[
                        ScsiFraming.PrecisionTwoStartupReadBufferE01072Length];
                    Buffer.BlockCopy(data, 10, startupVerificationData, 0,
                        startupVerificationData.Length);
                    continuationState = ReplayContinuationState.
                        AwaitStartupReadBufferE01072;
                    Info("OFFLINE REPLAY simulated the observed 1,082-byte " +
                        "startup WRITE BUFFER without a WinUSB call.");
                    return Success(new byte[0]);
                }
                bool initialWindow = operationalGate.State ==
                    AspiOperationalSequenceState.PreviewSetWindowReady;
                operationalGate.ObservePreviewSetWindow(target, lun, cdb,
                    data);
                operationalGate.RecordPreviewSetWindowCompletion(
                    (byte)AdapterStatus.Success, 0, data.Length);
                if (initialWindow)
                {
                    Info(string.Format(
                        "OFFLINE REPLAY simulated structurally valid Preview " +
                        "SET WINDOW success without a WinUSB call: " +
                        "payload-sha256={0}; the two observed post-window " +
                        "readiness polls are armed.",
                        PrecisionTwoPreviewCommandManifest.
                            FingerprintSetWindow(cdb, data)));
                }
                else if (operationalGate.State ==
                    AspiOperationalSequenceState.Complete)
                {
                    Info(string.Format(
                        "OFFLINE REPLAY completed two post-image SET WINDOW " +
                        "cleanup requests without a WinUSB call; streams={0}.",
                        operationalGate.CompletedOfflineImageStreams));
                }
                else
                {
                    Info("OFFLINE REPLAY completed the first post-image SET " +
                        "WINDOW cleanup request without a WinUSB call.");
                }
                return Success(new byte[0]);
            }
        }

        bool IAspiOperatorCycleTransport.CycleCompleted
        {
            get
            {
                lock (sync)
                {
                    return !disposed && operationalGate.State ==
                            AspiOperationalSequenceState.Complete &&
                        operationalGate.CompletedOfflineImageStreams == 2;
                }
            }
        }

        bool IAspiPreviewSetWindowValidationPolicy.
            StatefulPreviewSetWindowValidationEnabled
        {
            get { return true; }
        }

        private void RequireNotDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    "OfflineReplayAspiTransport");
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                disposed = true;
            }
        }

        private static byte[] BuildSyntheticImageRow(uint requestedLength,
            int rowIndex)
        {
            byte[] data = new byte[checked((int)requestedLength)];
            int width = data.Length /
                checked((int)ScsiFraming.PrecisionTwoPreviewBytesPerPixel);
            for (int column = 0; column < width; ++column)
            {
                ushort red = width <= 1 ? (ushort)0 : checked((ushort)
                    ((uint)column * ushort.MaxValue / (uint)(width - 1)));
                ushort green = checked((ushort)
                    ((uint)rowIndex * 257U & ushort.MaxValue));
                ushort blue = checked((ushort)(red ^ green));
                int offset = column * 6;
                WriteBigEndianUInt16(data, offset, red);
                WriteBigEndianUInt16(data, offset + 2, green);
                WriteBigEndianUInt16(data, offset + 4, blue);
            }
            return data;
        }

        private static void WriteBigEndianUInt16(byte[] data, int offset,
            ushort value)
        {
            data[offset] = checked((byte)(value >> 8));
            data[offset + 1] = checked((byte)(value & 0xFF));
        }

        private static byte[] BuildSyntheticOperationalInquiry(int length)
        {
            byte[] data = new byte[length];
            data[0] = 0x06;
            if (length > 2)
            {
                data[2] = 0x02;
            }
            if (length > 3)
            {
                data[3] = 0x02;
            }
            if (length > 4)
            {
                data[4] = checked((byte)Math.Min(91, length - 5));
            }
            WriteAscii(data, 8, 8, "Imacon");
            WriteAscii(data, 16, 16, "FlexTight II");
            WriteAscii(data, 32, 4, "M333");
            return data;
        }

        private byte[] BuildSyntheticOperationalD8Response()
        {
            byte[] data = new byte[
                ScsiFraming.PrecisionTwoLoaderBufferD8Length];
            WriteAscii(data, 0, 9, "000000000");
            data[9] = 0;
            data[10] = 0xFC;
            WriteAscii(data, 11, 9, "000000000");
            data[20] = 0;
            data[21] = 0xFC;
            WriteAscii(data, 22, 10, "----------");
            data[32] = 0;
            WriteAscii(data, 33, 10, "0000000000");
            data[43] = 0;
            WriteAscii(data, 44, 10, "IMACON");
            data[54] = 0;
            Buffer.BlockCopy(BuildSyntheticScannerIdentifier(), 0, data, 55,
                checked((int)ScsiFraming.PrecisionTwoD8Offset55Length));
            data[65] = 0;
            return data;
        }

        private byte[] BuildSyntheticScannerIdentifier()
        {
            return (byte[])replayIdentifier.Clone();
        }

        private static byte[] ParseReplayIdentifier(string value)
        {
            // The fallback is deliberately fictional. A local profile marker
            // may supply the real field to the offline child process, but the
            // value is never embedded in source or written to the replay log.
            if (string.IsNullOrEmpty(value))
            {
                value = "FP00000000";
            }
            if (value.Length != 10 || value[0] != 'F' || value[1] != 'P')
            {
                throw new ArgumentException(
                    "Replay scanner identifier must be ten characters and " +
                    "start with FP.", "value");
            }
            for (int index = 2; index < value.Length; ++index)
            {
                char character = value[index];
                if (!((character >= 'A' && character <= 'Z') ||
                      (character >= '0' && character <= '9')))
                {
                    throw new ArgumentException(
                        "Replay scanner identifier must contain only " +
                        "uppercase ASCII letters and digits.", "value");
                }
            }
            return Encoding.ASCII.GetBytes(value);
        }

        private static void WriteAscii(byte[] destination, int offset,
            int fieldLength, string value)
        {
            if (offset >= destination.Length)
            {
                return;
            }
            int available = Math.Min(fieldLength, destination.Length - offset);
            for (int i = 0; i < available; ++i)
            {
                destination[offset + i] = (byte)' ';
            }
            byte[] source = Encoding.ASCII.GetBytes(value);
            Buffer.BlockCopy(source, 0, destination, offset,
                Math.Min(available, source.Length));
        }

        private static byte[] BuildOperationalResponse(
            AspiOperationalReadCommand command, uint requestedLength)
        {
            if (command == AspiOperationalReadCommand.FaultPixelHeader)
            {
                return new byte[]
                {
                    0x46, 0x61, 0x75, 0x6C, 0x20, 0x50, 0x69, 0x78,
                    0x73, 0x00, 0x20, 0x20, 0x30, 0x0A, 0x20, 0x20,
                    0x31, 0x0A, 0x20, 0x20, 0x30, 0x0A
                };
            }
            if (command ==
                AspiOperationalReadCommand.DynamicConfigurationD8)
            {
                return BuildSyntheticDynamicConfiguration();
            }
            return new byte[requestedLength];
        }

        private static byte[] BuildSyntheticDynamicConfiguration()
        {
            byte[] data = new byte[
                ScsiFraming.PrecisionTwoDynamicConfigurationLength];
            data[0] = (byte)'0';
            data[2] = (byte)'0';
            data[3] = (byte)'0';
            byte[] identity = Encoding.ASCII.GetBytes("IMACON    ");
            Buffer.BlockCopy(identity, 0, data, 5, identity.Length);
            for (int index = 0; index < 20; ++index)
            {
                byte[] record = Encoding.ASCII.GetBytes(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0,7},{1,7},{2,7}", 0, 0, 0));
                Buffer.BlockCopy(record, 0, data, 27 + index * 24,
                    record.Length);
            }
            return data;
        }

        private static AspiTransportResult Success(byte[] data)
        {
            return new AspiTransportResult(data,
                (byte)AdapterStatus.Success);
        }

        private void Info(string message)
        {
            if (log != null)
            {
                log.Info(message);
            }
        }

        private static bool IsOperationalRead(byte[] cdb)
        {
            return Matches(cdb,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb()) ||
                Matches(cdb,
                    ScsiFraming.BuildPrecisionTwoFaultPixelReadBufferCdb()) ||
                Matches(cdb, ScsiFraming.
                    BuildPrecisionTwoFaultPixelDataReadBufferCdb()) ||
                Matches(cdb,
                    ScsiFraming.BuildPrecisionTwoCalibrationReadBufferCdb()) ||
                Matches(cdb,
                    ScsiFraming.BuildPrecisionTwoD8Offset55ReadBufferCdb()) ||
                Matches(cdb, ScsiFraming.
                    BuildPrecisionTwoDynamicConfigurationD8ReadBufferCdb());
        }

        private static bool Matches(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }
            int difference = 0;
            for (int index = 0; index < left.Length; ++index)
            {
                difference |= left[index] ^ right[index];
            }
            return difference == 0;
        }

        private static long GetReplayMilliseconds()
        {
            return Environment.TickCount & 0x7FFFFFFF;
        }
    }

    internal sealed class WinUsbAspiTransport : IAspiDataOutTransport,
        IAspiPreviewSetWindowValidationPolicy,
        IAspiStartupWriteFingerprintPolicy, IAspiOperatorCycleTransport,
        IDisposable
    {
        private readonly object sync = new object();
        private readonly IUsbLog log;
        private readonly bool loaderDataOutEnabled;
        private readonly bool previewSequenceEnabled;
        private readonly bool previewSetWindowEnabled;
        private readonly int previewImageReadLimit;
        private readonly bool previewImageShortRetryEnabled;
        private readonly bool previewCleanupExecutionEnabled;
        private readonly bool previewCancellationExecutionEnabled;
        private readonly bool fullScanSuccessorObservationEnabled;
        private readonly bool fullScanSetWindowExecutionEnabled;
        private readonly bool fullScanImageExecutionEnabled;
        private readonly bool fullScanTerminalProbeExecutionEnabled;
        private readonly bool fullScanNaturalCompletionEnabled;
        private readonly bool fullScanRow997CompletionEnabled;
        private readonly bool fullScanProgressCompletionEnabled;
        private readonly bool operatorCycleEnabled;
        private readonly bool warmOperatorCycleEnabled;
        private readonly int previewMaximumReadSubmissions;
        private readonly int previewMaximumShortRetries;
        private readonly int previewMaximumConsecutiveShortRetries;
        private readonly int previewMaximumScannerReadyPolls;
        private readonly long previewMaximumStreamMilliseconds;
        private readonly string previewSetWindowSha256;
        private readonly AspiReadOnlyDiscoveryGate discoveryGate =
            new AspiReadOnlyDiscoveryGate();
        private readonly AspiLoaderSequenceGate loaderGate =
            new AspiLoaderSequenceGate();
        private readonly AspiFullScanInquiryGate fullScanInquiryGate =
            new AspiFullScanInquiryGate();
        private readonly AspiWarmOperatorInquiryGate warmOperatorInquiryGate =
            new AspiWarmOperatorInquiryGate();
        private readonly AspiOperationalSequenceGate operationalGate;
        private Usb2XchangeDevice device;
        private int livePreviewImageRowsCompleted;
        private int livePreviewCleanupCandidatesObserved;
        private bool liveFullScanRepeatArmed;
        private bool liveFullScanExtraScannerReadyCompleted;
        private bool liveFullScanSetWindowCompleted;
        private int liveFullScanCleanupCandidatesObserved;
        private bool liveFullScanCompleted;
        private bool liveOperatorCancellationCompleted;
        private bool liveWarmPreviewInitializationStarted;
        private bool liveWarmPreviewSetWindowCompleted;
        private int liveFullScanExtraScannerReadyAttempts;
        private long liveFullScanExtraScannerReadyStartedMilliseconds = -1;

        public WinUsbAspiTransport(IUsbLog log)
            : this(log, false, false)
        {
        }

        internal WinUsbAspiTransport(IUsbLog log, bool loaderDataOutEnabled)
            : this(log, loaderDataOutEnabled, false)
        {
        }

        internal static WinUsbAspiTransport CreateOperatorCycle(IUsbLog log)
        {
            return CreateOperatorCycle(log, false);
        }

        internal static WinUsbAspiTransport CreateWarmOperatorCycle(
            IUsbLog log)
        {
            return CreateOperatorCycle(log, true);
        }

        internal static AspiOperatorSessionTransport CreateOperatorSession(
            IUsbLog log)
        {
            bool firstCycle = true;
            return new AspiOperatorSessionTransport(delegate
            {
                bool warmCycle = !firstCycle;
                firstCycle = false;
                return CreateOperatorCycle(log, warmCycle);
            });
        }

        internal static AspiOperatorSessionTransport
            CreateFullScanRepeatSession(IUsbLog log)
        {
            bool firstCycle = true;
            return new AspiOperatorSessionTransport(delegate
            {
                bool warmCycle = !firstCycle;
                firstCycle = false;
                return CreateOperatorCycle(log, warmCycle, false);
            });
        }

        private static WinUsbAspiTransport CreateOperatorCycle(IUsbLog log,
            bool warmOperatorCycleEnabled)
        {
            return CreateOperatorCycle(log, warmOperatorCycleEnabled, true);
        }

        private static WinUsbAspiTransport CreateOperatorCycle(IUsbLog log,
            bool warmOperatorCycleEnabled, bool cancellationEnabled)
        {
            return new WinUsbAspiTransport(log, false, true, true,
                PrecisionTwoPreviewCommandManifest.AspiLiveSetWindowSha256,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows, true,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalMaximumShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredMaximumConsecutiveShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalMaximumScannerReadyPolls,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalMaximumMilliseconds,
                true, cancellationEnabled, true, true, true, false, false,
                false, true, cancellationEnabled,
                warmOperatorCycleEnabled);
        }

        internal WinUsbAspiTransport(IUsbLog log, bool loaderDataOutEnabled,
            bool previewSetWindowEnabled)
            : this(log, loaderDataOutEnabled, previewSetWindowEnabled,
                previewSetWindowEnabled)
        {
        }

        internal WinUsbAspiTransport(IUsbLog log, bool loaderDataOutEnabled,
            bool previewSequenceEnabled, bool previewSetWindowEnabled)
            : this(log, loaderDataOutEnabled, previewSequenceEnabled,
                previewSetWindowEnabled,
                PrecisionTwoPreviewCommandManifest.SetWindowSha256)
        {
        }

        internal WinUsbAspiTransport(IUsbLog log, bool loaderDataOutEnabled,
            bool previewSequenceEnabled, bool previewSetWindowEnabled,
            string expectedPreviewSetWindowSha256)
            : this(log, loaderDataOutEnabled, previewSequenceEnabled,
                previewSetWindowEnabled, expectedPreviewSetWindowSha256,
                false)
        {
        }

        internal WinUsbAspiTransport(IUsbLog log, bool loaderDataOutEnabled,
            bool previewSequenceEnabled, bool previewSetWindowEnabled,
            string expectedPreviewSetWindowSha256,
            bool previewFirstImageReadEnabled)
            : this(log, loaderDataOutEnabled, previewSequenceEnabled,
                previewSetWindowEnabled, expectedPreviewSetWindowSha256,
                previewFirstImageReadEnabled ? 1 : 0)
        {
        }

        internal WinUsbAspiTransport(IUsbLog log, bool loaderDataOutEnabled,
            bool previewSequenceEnabled, bool previewSetWindowEnabled,
            string expectedPreviewSetWindowSha256, int previewImageReadLimit)
            : this(log, loaderDataOutEnabled, previewSequenceEnabled,
                previewSetWindowEnabled, expectedPreviewSetWindowSha256,
                previewImageReadLimit, false)
        {
        }

        internal WinUsbAspiTransport(IUsbLog log, bool loaderDataOutEnabled,
            bool previewSequenceEnabled, bool previewSetWindowEnabled,
            string expectedPreviewSetWindowSha256, int previewImageReadLimit,
            bool previewImageShortRetryEnabled)
            : this(log, loaderDataOutEnabled, previewSequenceEnabled,
                previewSetWindowEnabled, expectedPreviewSetWindowSha256,
                previewImageReadLimit, previewImageShortRetryEnabled,
                previewImageReadLimit == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalRows
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumReadSubmissions
                    : PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamMaximumReadSubmissions,
                previewImageReadLimit == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalRows
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumShortRetries
                    : PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamMaximumShortRetries,
                previewImageReadLimit == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalRows
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumConsecutiveShortRetries
                    : PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamMaximumConsecutiveShortRetries,
                previewImageReadLimit == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalRows
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumScannerReadyPolls
                    : PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamMaximumScannerReadyPolls,
                previewImageReadLimit == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalRows
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumMilliseconds
                    : AspiOperationalSequenceGate.
                        MaximumLivePreviewStreamMilliseconds)
        {
        }

        internal WinUsbAspiTransport(IUsbLog log, bool loaderDataOutEnabled,
            bool previewSequenceEnabled, bool previewSetWindowEnabled,
            string expectedPreviewSetWindowSha256, int previewImageReadLimit,
            bool previewImageShortRetryEnabled,
            int maximumReadSubmissions, int maximumShortRetries,
            int maximumConsecutiveShortRetries,
            int maximumScannerReadyPolls, long maximumStreamMilliseconds)
            : this(log, loaderDataOutEnabled, previewSequenceEnabled,
                previewSetWindowEnabled, expectedPreviewSetWindowSha256,
                previewImageReadLimit, previewImageShortRetryEnabled,
                maximumReadSubmissions, maximumShortRetries,
                maximumConsecutiveShortRetries, maximumScannerReadyPolls,
                maximumStreamMilliseconds, false)
        {
        }

        internal WinUsbAspiTransport(IUsbLog log, bool loaderDataOutEnabled,
            bool previewSequenceEnabled, bool previewSetWindowEnabled,
            string expectedPreviewSetWindowSha256, int previewImageReadLimit,
            bool previewImageShortRetryEnabled,
            int maximumReadSubmissions, int maximumShortRetries,
            int maximumConsecutiveShortRetries,
            int maximumScannerReadyPolls, long maximumStreamMilliseconds,
            bool previewCleanupExecutionEnabled)
            : this(log, loaderDataOutEnabled, previewSequenceEnabled,
                previewSetWindowEnabled, expectedPreviewSetWindowSha256,
                previewImageReadLimit, previewImageShortRetryEnabled,
                maximumReadSubmissions, maximumShortRetries,
                maximumConsecutiveShortRetries, maximumScannerReadyPolls,
                maximumStreamMilliseconds, previewCleanupExecutionEnabled,
                false)
        {
        }

        internal WinUsbAspiTransport(IUsbLog log, bool loaderDataOutEnabled,
            bool previewSequenceEnabled, bool previewSetWindowEnabled,
            string expectedPreviewSetWindowSha256, int previewImageReadLimit,
            bool previewImageShortRetryEnabled,
            int maximumReadSubmissions, int maximumShortRetries,
            int maximumConsecutiveShortRetries,
            int maximumScannerReadyPolls, long maximumStreamMilliseconds,
            bool previewCleanupExecutionEnabled,
            bool previewCancellationExecutionEnabled)
            : this(log, loaderDataOutEnabled, previewSequenceEnabled,
                previewSetWindowEnabled, expectedPreviewSetWindowSha256,
                previewImageReadLimit, previewImageShortRetryEnabled,
                maximumReadSubmissions, maximumShortRetries,
                maximumConsecutiveShortRetries, maximumScannerReadyPolls,
                maximumStreamMilliseconds, previewCleanupExecutionEnabled,
                previewCancellationExecutionEnabled, false)
        {
        }

        internal WinUsbAspiTransport(IUsbLog log, bool loaderDataOutEnabled,
            bool previewSequenceEnabled, bool previewSetWindowEnabled,
            string expectedPreviewSetWindowSha256, int previewImageReadLimit,
            bool previewImageShortRetryEnabled,
            int maximumReadSubmissions, int maximumShortRetries,
            int maximumConsecutiveShortRetries,
            int maximumScannerReadyPolls, long maximumStreamMilliseconds,
            bool previewCleanupExecutionEnabled,
            bool previewCancellationExecutionEnabled,
            bool fullScanSuccessorObservationEnabled)
            : this(log, loaderDataOutEnabled, previewSequenceEnabled,
                previewSetWindowEnabled, expectedPreviewSetWindowSha256,
                previewImageReadLimit, previewImageShortRetryEnabled,
                maximumReadSubmissions, maximumShortRetries,
                maximumConsecutiveShortRetries, maximumScannerReadyPolls,
                maximumStreamMilliseconds, previewCleanupExecutionEnabled,
                previewCancellationExecutionEnabled,
                fullScanSuccessorObservationEnabled, false)
        {
        }

        internal WinUsbAspiTransport(IUsbLog log, bool loaderDataOutEnabled,
            bool previewSequenceEnabled, bool previewSetWindowEnabled,
            string expectedPreviewSetWindowSha256, int previewImageReadLimit,
            bool previewImageShortRetryEnabled,
            int maximumReadSubmissions, int maximumShortRetries,
            int maximumConsecutiveShortRetries,
            int maximumScannerReadyPolls, long maximumStreamMilliseconds,
            bool previewCleanupExecutionEnabled,
            bool previewCancellationExecutionEnabled,
            bool fullScanSuccessorObservationEnabled,
            bool fullScanSetWindowExecutionEnabled)
            : this(log, loaderDataOutEnabled, previewSequenceEnabled,
                previewSetWindowEnabled, expectedPreviewSetWindowSha256,
                previewImageReadLimit, previewImageShortRetryEnabled,
                maximumReadSubmissions, maximumShortRetries,
                maximumConsecutiveShortRetries, maximumScannerReadyPolls,
                maximumStreamMilliseconds, previewCleanupExecutionEnabled,
                previewCancellationExecutionEnabled,
                fullScanSuccessorObservationEnabled,
                fullScanSetWindowExecutionEnabled, false)
        {
        }

        internal WinUsbAspiTransport(IUsbLog log, bool loaderDataOutEnabled,
            bool previewSequenceEnabled, bool previewSetWindowEnabled,
            string expectedPreviewSetWindowSha256, int previewImageReadLimit,
            bool previewImageShortRetryEnabled,
            int maximumReadSubmissions, int maximumShortRetries,
            int maximumConsecutiveShortRetries,
            int maximumScannerReadyPolls, long maximumStreamMilliseconds,
            bool previewCleanupExecutionEnabled,
            bool previewCancellationExecutionEnabled,
            bool fullScanSuccessorObservationEnabled,
            bool fullScanSetWindowExecutionEnabled,
            bool fullScanImageExecutionEnabled)
            : this(log, loaderDataOutEnabled, previewSequenceEnabled,
                previewSetWindowEnabled, expectedPreviewSetWindowSha256,
                previewImageReadLimit, previewImageShortRetryEnabled,
                maximumReadSubmissions, maximumShortRetries,
                maximumConsecutiveShortRetries, maximumScannerReadyPolls,
                maximumStreamMilliseconds, previewCleanupExecutionEnabled,
                previewCancellationExecutionEnabled,
                fullScanSuccessorObservationEnabled,
                fullScanSetWindowExecutionEnabled,
                fullScanImageExecutionEnabled, false)
        {
        }

        internal WinUsbAspiTransport(IUsbLog log, bool loaderDataOutEnabled,
            bool previewSequenceEnabled, bool previewSetWindowEnabled,
            string expectedPreviewSetWindowSha256, int previewImageReadLimit,
            bool previewImageShortRetryEnabled,
            int maximumReadSubmissions, int maximumShortRetries,
            int maximumConsecutiveShortRetries,
            int maximumScannerReadyPolls, long maximumStreamMilliseconds,
            bool previewCleanupExecutionEnabled,
            bool previewCancellationExecutionEnabled,
            bool fullScanSuccessorObservationEnabled,
            bool fullScanSetWindowExecutionEnabled,
            bool fullScanImageExecutionEnabled,
            bool fullScanTerminalProbeExecutionEnabled,
            bool fullScanNaturalCompletionEnabled = false,
            bool fullScanRow997CompletionEnabled = false,
            bool fullScanProgressCompletionEnabled = false,
            bool operatorCycleEnabled = false,
            bool warmOperatorCycleEnabled = false)
        {
            if (previewSetWindowEnabled && !previewSequenceEnabled)
            {
                throw new ArgumentException(
                    "Preview SET WINDOW requires the Preview sequence gate.",
                    "previewSetWindowEnabled");
            }
            if (previewImageReadLimit != 0 && !previewSetWindowEnabled)
            {
                throw new ArgumentException(
                    "Preview image READ requires the exact SET WINDOW gate.",
                    "previewImageReadLimit");
            }
            if (previewImageReadLimit != 0 && previewImageReadLimit != 1 &&
                previewImageReadLimit != PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewBurstRows &&
                previewImageReadLimit != PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamRows &&
                previewImageReadLimit != PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalRows &&
                previewImageReadLimit != PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows)
            {
                throw new ArgumentOutOfRangeException(
                    "previewImageReadLimit",
                    "Live Preview image reads are disabled, one-shot, the " +
                    "exact eight-row burst, the exact 256-row stream, or " +
                    "an exact reviewed natural boundary; no other limit is " +
                    "accepted.");
            }
            if (previewImageShortRetryEnabled &&
                previewImageReadLimit != PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamRows &&
                previewImageReadLimit != PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalRows &&
                previewImageReadLimit != PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows)
            {
                throw new ArgumentException(
                    "Preview short retry is valid only for a separately " +
                    "approved 256-row or reviewed natural stream.",
                    "previewImageShortRetryEnabled");
            }
            if ((previewImageReadLimit == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalRows ||
                 previewImageReadLimit == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows) &&
                !previewImageShortRetryEnabled)
            {
                throw new ArgumentException(
                    "A natural Preview boundary requires exact bounded " +
                    "short-retry handling.",
                    "previewImageShortRetryEnabled");
            }
            if (previewCleanupExecutionEnabled &&
                (previewImageReadLimit != PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalRows ||
                 !previewImageShortRetryEnabled))
            {
                throw new ArgumentException(
                    "Preview cleanup execution requires the exact powered " +
                    "911-row short-retry policy.",
                    "previewCleanupExecutionEnabled");
            }
            if (previewCancellationExecutionEnabled &&
                ((previewCleanupExecutionEnabled && !operatorCycleEnabled) ||
                 previewImageReadLimit != PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalRows ||
                 !previewImageShortRetryEnabled))
            {
                throw new ArgumentException(
                    "Preview cancellation execution requires the exact " +
                    "powered 911-row short-retry policy and cannot share " +
                    "normal-completion cleanup authority.",
                    "previewCancellationExecutionEnabled");
            }
            if (fullScanSuccessorObservationEnabled &&
                (!previewCleanupExecutionEnabled ||
                 (previewCancellationExecutionEnabled &&
                    !operatorCycleEnabled)))
            {
                throw new ArgumentException(
                    "Full-scan successor observation requires the exact " +
                    "normal powered Preview cleanup policy.",
                    "fullScanSuccessorObservationEnabled");
            }
            if (fullScanSetWindowExecutionEnabled &&
                !fullScanSuccessorObservationEnabled)
            {
                throw new ArgumentException(
                    "Full-scan SET WINDOW execution requires the exact " +
                    "successor-observation policy.",
                    "fullScanSetWindowExecutionEnabled");
            }
            if (fullScanImageExecutionEnabled &&
                !fullScanSetWindowExecutionEnabled)
            {
                throw new ArgumentException(
                    "Full-scan image execution requires the exact full-scan " +
                    "SET WINDOW policy.", "fullScanImageExecutionEnabled");
            }
            if (fullScanTerminalProbeExecutionEnabled &&
                !fullScanImageExecutionEnabled)
            {
                throw new ArgumentException(
                    "Full-scan terminal probing requires the exact bounded " +
                    "full-scan image policy.",
                    "fullScanTerminalProbeExecutionEnabled");
            }
            if (fullScanNaturalCompletionEnabled &&
                (!fullScanImageExecutionEnabled ||
                 fullScanTerminalProbeExecutionEnabled))
            {
                throw new ArgumentException(
                    "Natural full-scan completion requires exact image " +
                    "execution and cannot share terminal-probe authority.",
                    "fullScanNaturalCompletionEnabled");
            }
            if (fullScanRow997CompletionEnabled &&
                (!fullScanImageExecutionEnabled ||
                 fullScanTerminalProbeExecutionEnabled ||
                 fullScanNaturalCompletionEnabled))
            {
                throw new ArgumentException(
                    "Row-997 full-scan completion requires exact image " +
                    "execution and cannot share terminal-probe or historical " +
                    "996-row authority.",
                    "fullScanRow997CompletionEnabled");
            }
            if (fullScanProgressCompletionEnabled &&
                (!fullScanImageExecutionEnabled ||
                 fullScanTerminalProbeExecutionEnabled ||
                 fullScanNaturalCompletionEnabled ||
                 fullScanRow997CompletionEnabled))
            {
                throw new ArgumentException(
                    "Progress-bounded full-scan completion requires exact " +
                    "image execution and cannot share any historical " +
                    "fixed-row authority.",
                    "fullScanProgressCompletionEnabled");
            }
            if (operatorCycleEnabled &&
                (!previewCleanupExecutionEnabled ||
                 !previewCancellationExecutionEnabled ||
                 !fullScanProgressCompletionEnabled))
            {
                throw new ArgumentException(
                    "An operator cycle requires exact normal Preview, " +
                    "cancellation, and hardware-proven full-scan completion " +
                    "policies.", "operatorCycleEnabled");
            }
            if (warmOperatorCycleEnabled &&
                (!previewCleanupExecutionEnabled ||
                 !fullScanSuccessorObservationEnabled ||
                 !fullScanSetWindowExecutionEnabled ||
                 !fullScanImageExecutionEnabled ||
                 !fullScanProgressCompletionEnabled))
            {
                throw new ArgumentException(
                    "A warm operator cycle requires the exact powered " +
                    "Preview and progress-bounded full-scan policy.",
                    "warmOperatorCycleEnabled");
            }
            this.log = log;
            this.loaderDataOutEnabled = loaderDataOutEnabled;
            this.previewSequenceEnabled = previewSequenceEnabled;
            this.previewSetWindowEnabled = previewSetWindowEnabled;
            this.previewImageReadLimit = previewImageReadLimit;
            this.previewImageShortRetryEnabled =
                previewImageShortRetryEnabled;
            this.previewCleanupExecutionEnabled =
                previewCleanupExecutionEnabled;
            this.previewCancellationExecutionEnabled =
                previewCancellationExecutionEnabled;
            this.fullScanSuccessorObservationEnabled =
                fullScanSuccessorObservationEnabled;
            this.fullScanSetWindowExecutionEnabled =
                fullScanSetWindowExecutionEnabled;
            this.fullScanImageExecutionEnabled =
                fullScanImageExecutionEnabled;
            this.fullScanTerminalProbeExecutionEnabled =
                fullScanTerminalProbeExecutionEnabled;
            this.fullScanNaturalCompletionEnabled =
                fullScanNaturalCompletionEnabled;
            this.fullScanRow997CompletionEnabled =
                fullScanRow997CompletionEnabled;
            this.fullScanProgressCompletionEnabled =
                fullScanProgressCompletionEnabled;
            this.operatorCycleEnabled = operatorCycleEnabled;
            this.warmOperatorCycleEnabled = warmOperatorCycleEnabled;
            bool naturalPreview = previewImageReadLimit ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows ||
                previewImageReadLimit == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows;
            bool streamPolicy =
                maximumReadSubmissions ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamMaximumReadSubmissions &&
                maximumShortRetries ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamMaximumShortRetries &&
                maximumConsecutiveShortRetries ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamMaximumConsecutiveShortRetries &&
                maximumScannerReadyPolls ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamMaximumScannerReadyPolls &&
                maximumStreamMilliseconds == AspiOperationalSequenceGate.
                    MaximumLivePreviewStreamMilliseconds;
            bool legacyNaturalPolicy = previewImageReadLimit ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows &&
                maximumReadSubmissions ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumReadSubmissions &&
                maximumShortRetries ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumShortRetries &&
                maximumConsecutiveShortRetries ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumConsecutiveShortRetries &&
                maximumScannerReadyPolls ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumScannerReadyPolls &&
                maximumStreamMilliseconds ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumMilliseconds;
            bool perRowNaturalPolicy = previewImageReadLimit ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows &&
                maximumReadSubmissions ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalPerRowMaximumReadSubmissions &&
                maximumShortRetries ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalPerRowMaximumShortRetries &&
                maximumConsecutiveShortRetries ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumConsecutiveShortRetries &&
                maximumScannerReadyPolls ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumScannerReadyPolls &&
                maximumStreamMilliseconds ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumMilliseconds;
            bool poweredNaturalPolicy = previewImageReadLimit ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalRows &&
                maximumReadSubmissions ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalMaximumReadSubmissions &&
                maximumShortRetries ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalMaximumShortRetries &&
                maximumConsecutiveShortRetries ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredMaximumConsecutiveShortRetries &&
                maximumScannerReadyPolls ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumScannerReadyPolls &&
                maximumStreamMilliseconds ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumMilliseconds;
            if (previewCancellationExecutionEnabled &&
                !poweredNaturalPolicy)
            {
                throw new ArgumentException(
                    "Preview cancellation limits do not match the exact " +
                    "powered-natural policy.", "maximumReadSubmissions");
            }
            if ((previewImageShortRetryEnabled &&
                    !(previewImageReadLimit ==
                            PrecisionTwoPreviewCommandManifest.
                                AspiLivePreviewStreamRows && streamPolicy) &&
                    !legacyNaturalPolicy && !perRowNaturalPolicy &&
                    !poweredNaturalPolicy) ||
                (!previewImageShortRetryEnabled && !streamPolicy))
            {
                throw new ArgumentException(
                    "Preview transport limits do not match one complete " +
                    "approved stream, legacy-natural, per-row-natural, or " +
                    "powered-natural " +
                    "policy.", "maximumReadSubmissions");
            }
            previewMaximumReadSubmissions = maximumReadSubmissions;
            previewMaximumShortRetries = maximumShortRetries;
            previewMaximumConsecutiveShortRetries =
                maximumConsecutiveShortRetries;
            previewMaximumScannerReadyPolls = maximumScannerReadyPolls;
            previewMaximumStreamMilliseconds = maximumStreamMilliseconds;
            previewSetWindowSha256 = expectedPreviewSetWindowSha256;
            operationalGate = new AspiOperationalSequenceGate(
                expectedPreviewSetWindowSha256, previewSetWindowEnabled,
                previewImageReadLimit == PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamRows || naturalPreview,
                previewImageShortRetryEnabled,
                previewImageShortRetryEnabled
                    ? previewImageReadLimit
                    : PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamRows,
                previewMaximumReadSubmissions,
                previewMaximumShortRetries,
                previewMaximumConsecutiveShortRetries,
                previewMaximumScannerReadyPolls,
                previewMaximumStreamMilliseconds);
            if (warmOperatorCycleEnabled)
            {
                operationalGate.BeginLiveWarmOperatorCycle();
                warmOperatorInquiryGate.Arm();
                if (log != null)
                {
                    log.Warning("LIVE ASPI warm operator cycle seeded; one " +
                        "exact operational INQUIRY/DF prefix, two " +
                        "counter-bound M333 revalidations within the two " +
                        "Preview stabilization cycles, and one later Scan " +
                        "cycle are the only reusable entry path.");
                }
            }
        }

        internal string PreviewSetWindowSha256
        {
            get { return previewSetWindowSha256; }
        }

        internal bool PreviewFirstImageReadEnabled
        {
            get { return previewImageReadLimit == 1; }
        }

        internal int PreviewImageReadLimit
        {
            get { return previewImageReadLimit; }
        }

        internal bool PreviewImageShortRetryEnabled
        {
            get { return previewImageShortRetryEnabled; }
        }

        internal bool PreviewCleanupExecutionEnabled
        {
            get { return previewCleanupExecutionEnabled; }
        }

        internal bool PreviewCancellationExecutionEnabled
        {
            get { return previewCancellationExecutionEnabled; }
        }

        internal bool FullScanSuccessorObservationEnabled
        {
            get { return fullScanSuccessorObservationEnabled; }
        }

        internal bool FullScanSetWindowExecutionEnabled
        {
            get { return fullScanSetWindowExecutionEnabled; }
        }

        internal bool FullScanImageExecutionEnabled
        {
            get { return fullScanImageExecutionEnabled; }
        }

        internal bool FullScanTerminalProbeExecutionEnabled
        {
            get { return fullScanTerminalProbeExecutionEnabled; }
        }

        internal bool FullScanNaturalCompletionEnabled
        {
            get { return fullScanNaturalCompletionEnabled; }
        }

        internal bool FullScanRow997CompletionEnabled
        {
            get { return fullScanRow997CompletionEnabled; }
        }

        internal bool FullScanProgressCompletionEnabled
        {
            get { return fullScanProgressCompletionEnabled; }
        }

        internal bool OperatorCycleEnabled
        {
            get { return operatorCycleEnabled; }
        }

        internal bool WarmOperatorCycleEnabled
        {
            get { return warmOperatorCycleEnabled; }
        }

        private int FullScanImageRowLimit
        {
            get
            {
                return fullScanProgressCompletionEnabled
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanProgressMaximumRows
                    : fullScanRow997CompletionEnabled
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanRow997CompletionRows
                    : fullScanNaturalCompletionEnabled
                        ? PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanNaturalRows
                        : PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanRows;
            }
        }

        private int FullScanMaximumReadSubmissions
        {
            get
            {
                return fullScanProgressCompletionEnabled
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanProgressMaximumReadSubmissions
                    : fullScanRow997CompletionEnabled
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanRow997CompletionMaximumReadSubmissions
                    : fullScanNaturalCompletionEnabled
                        ? PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanNaturalMaximumReadSubmissions
                        : PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanMaximumReadSubmissions;
            }
        }

        private int FullScanMaximumShortRetries
        {
            get
            {
                return fullScanProgressCompletionEnabled
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanProgressMaximumShortRetries
                    : fullScanRow997CompletionEnabled
                    ? PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanRow997CompletionMaximumShortRetries
                    : fullScanNaturalCompletionEnabled
                        ? PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanNaturalMaximumShortRetries
                        : PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanMaximumShortRetries;
            }
        }

        internal bool LiveFullScanCompleted
        {
            get { return liveFullScanCompleted; }
        }

        bool IAspiOperatorCycleTransport.CycleCompleted
        {
            get
            {
                return liveFullScanCompleted ||
                    liveOperatorCancellationCompleted;
            }
        }

        bool IAspiPreviewSetWindowValidationPolicy.
            StatefulPreviewSetWindowValidationEnabled
        {
            get
            {
                return previewCleanupExecutionEnabled ||
                    previewCancellationExecutionEnabled;
            }
        }

        void IAspiStartupWriteFingerprintPolicy.
            ObserveStartupWriteFingerprint(byte target, byte lun,
                byte[] cdb, byte[] data)
        {
            lock (sync)
            {
                if (!fullScanSuccessorObservationEnabled ||
                    !liveFullScanRepeatArmed ||
                    !fullScanInquiryGate.PrefixCompleted ||
                    !liveFullScanExtraScannerReadyCompleted)
                {
                    throw new InvalidOperationException(
                        "Startup WRITE BUFFER fingerprint authority is not " +
                        "armed at the full-scan successor boundary.");
                }
                operationalGate.ObserveLiveFullScanStartupWrite(target, lun,
                    cdb, data);
                DisposeDevice();
            }
        }

        internal int PreviewMaximumReadSubmissions
        {
            get { return previewMaximumReadSubmissions; }
        }

        internal int PreviewMaximumShortRetries
        {
            get { return previewMaximumShortRetries; }
        }

        public AspiTransportResult Execute(byte target, byte lun, byte[] cdb,
            uint requestedLength)
        {
            lock (sync)
            {
                bool warmInquiryAttempt = false;
                bool warmStabilizationInquiryAttempt = false;
                if (warmOperatorCycleEnabled &&
                    !warmOperatorInquiryGate.Completed)
                {
                    if (!IsInquiry(cdb, requestedLength))
                    {
                        FailWarmOperatorCycle(
                            "A warm operator cycle must begin with its one " +
                            "exact operational INQUIRY.");
                    }
                    warmOperatorInquiryGate.Begin(target, lun, cdb,
                        requestedLength);
                    warmInquiryAttempt = true;
                }
                if (warmOperatorCycleEnabled &&
                    warmOperatorInquiryGate.Completed &&
                    !liveWarmPreviewInitializationStarted)
                {
                    if (!Matches(cdb,
                            ScsiFraming.BuildPrecisionTwoScannerReadyCdb()) ||
                        target != 5 || lun != 0 || requestedLength !=
                            ScsiFraming.PrecisionTwoScannerReadyLength)
                    {
                        FailWarmOperatorCycle(
                            "The exact warm operational INQUIRY must be " +
                            "followed immediately by target-5/LUN-0 DF.");
                    }
                    warmOperatorInquiryGate.BeginInitialScannerReady(target,
                        lun, cdb, requestedLength);
                    operationalGate.
                        BeginLiveWarmOperatorPreviewInitialization();
                    liveWarmPreviewInitializationStarted = true;
                    if (log != null)
                    {
                        log.Warning("LIVE ASPI warm operator Preview " +
                            "initialization armed after exact M333 INQUIRY; " +
                            "the current DF is its first bounded readiness " +
                            "attempt.");
                    }
                }
                if (warmOperatorCycleEnabled &&
                    liveWarmPreviewInitializationStarted &&
                    !liveWarmPreviewSetWindowCompleted &&
                    IsInquiry(cdb, requestedLength))
                {
                    if (operationalGate.State !=
                            AspiOperationalSequenceState.
                                AwaitD8Offset55OrPostCalibrationScannerReady)
                    {
                        FailWarmOperatorCycle(
                            "Warm stabilization INQUIRY arrived outside " +
                            "the exact post-calibration state.");
                    }
                    warmOperatorInquiryGate.BeginStabilizationInquiry(target,
                        lun, cdb, requestedLength,
                        operationalGate.CompletedInitializationCycles);
                    warmStabilizationInquiryAttempt = true;
                }
                if (warmOperatorCycleEnabled &&
                    liveWarmPreviewInitializationStarted &&
                    !liveWarmPreviewSetWindowCompleted &&
                    !warmStabilizationInquiryAttempt &&
                    !IsWarmOperatorPreviewInitializationRead(cdb,
                        requestedLength))
                {
                    FailWarmOperatorCycle(
                        "Warm Preview initialization permits only exact " +
                        "bounded operational reads before SET WINDOW.");
                }
                AspiFullScanInquiryKind? fullScanInquiry = null;
                if (fullScanSuccessorObservationEnabled &&
                    liveFullScanRepeatArmed &&
                    !fullScanInquiryGate.PrefixCompleted &&
                    (target != 5 || lun != 0 ||
                     !IsInquiry(cdb, requestedLength)))
                {
                    operationalGate.FailClosed();
                    fullScanInquiryGate.FailClosed();
                    DisposeDevice();
                    throw new InvalidOperationException(
                        "The bounded full-scan successor must begin with " +
                        "one operational INQUIRY.");
                }
                if (fullScanSuccessorObservationEnabled &&
                    liveFullScanRepeatArmed &&
                    IsInquiry(cdb, requestedLength))
                {
                    fullScanInquiry = fullScanInquiryGate.Begin(target, lun,
                        cdb, requestedLength,
                        fullScanImageExecutionEnabled &&
                            liveFullScanSetWindowCompleted &&
                            operationalGate.State ==
                                AspiOperationalSequenceState.
                                    PreviewImageStreaming,
                        livePreviewImageRowsCompleted);
                }
                if (fullScanSuccessorObservationEnabled &&
                    liveFullScanRepeatArmed &&
                    liveFullScanExtraScannerReadyCompleted)
                {
                    operationalGate.FailClosed();
                    fullScanInquiryGate.FailClosed();
                    DisposeDevice();
                    throw new InvalidOperationException(
                        "The full-scan startup WRITE BUFFER or SET WINDOW " +
                        "fingerprint is the only successor after the extra " +
                        "ready completion.");
                }
                if (fullScanSuccessorObservationEnabled &&
                    liveFullScanRepeatArmed &&
                    fullScanInquiryGate.PrefixCompleted &&
                    !fullScanInquiry.HasValue &&
                    !Matches(cdb,
                        ScsiFraming.BuildPrecisionTwoScannerReadyCdb()) &&
                    !IsOperationalRead(cdb) && !IsRequestSense(cdb,
                        requestedLength) &&
                    !(fullScanSetWindowExecutionEnabled &&
                      liveFullScanSetWindowCompleted &&
                      IsPredictedImageRead(cdb, requestedLength)))
                {
                    operationalGate.FailClosed();
                    fullScanInquiryGate.FailClosed();
                    DisposeDevice();
                    throw new InvalidOperationException(
                        "The bounded full-scan successor permits only exact " +
                        "read-only repeat-initialization requests.");
                }
                if (previewImageReadLimit != 0 &&
                    operationalGate.State == AspiOperationalSequenceState.
                        PredictedPreviewImageReadObserved)
                {
                    throw new InvalidOperationException(
                        "The permitted Preview image READ boundary has already " +
                        "completed; every later command remains blocked before " +
                        "USB.");
                }
                if (previewSetWindowEnabled && cdb != null &&
                    cdb.Length == 10 && cdb[0] == 0x28)
                {
                    try
                    {
                        if (fullScanSetWindowExecutionEnabled &&
                            liveFullScanSetWindowCompleted)
                        {
                            if (fullScanImageExecutionEnabled)
                            {
                                if (fullScanTerminalProbeExecutionEnabled &&
                                    operationalGate.LivePreviewImageRows ==
                                        PrecisionTwoPreviewCommandManifest.
                                            AspiLiveFullScanRows)
                                {
                                    return ExecuteFullScanTerminalProbeRead(
                                        target, lun, cdb, requestedLength);
                                }
                                return ExecuteBoundedFullScanImageShortRetryRead(
                                    target, lun, cdb, requestedLength);
                            }
                            return ObserveAndBlockLiveFullScanFirstImageRead(
                                target, lun, cdb, requestedLength);
                        }
                        if (previewImageReadLimit == 1)
                        {
                            return ExecuteOnePredictedPreviewImageRead(target,
                                lun, cdb, requestedLength);
                        }
                        if (previewImageReadLimit ==
                                PrecisionTwoPreviewCommandManifest.
                                    AspiLivePreviewBurstRows ||
                            previewImageReadLimit ==
                                PrecisionTwoPreviewCommandManifest.
                                    AspiLivePreviewStreamRows ||
                            previewImageReadLimit ==
                                PrecisionTwoPreviewCommandManifest.
                                    AspiLivePreviewNaturalRows ||
                            previewImageReadLimit ==
                                PrecisionTwoPreviewCommandManifest.
                                    AspiLivePreviewPoweredNaturalRows)
                        {
                            if (previewImageShortRetryEnabled)
                            {
                                return ExecuteBoundedPreviewImageShortRetryRead(
                                    target, lun, cdb, requestedLength);
                            }
                            return ExecuteBoundedPreviewImageBurstRead(target,
                                lun, cdb, requestedLength);
                        }
                        return ObserveAndBlockPredictedPreviewImageRead(target,
                            lun, cdb, requestedLength);
                    }
                    catch
                    {
                        operationalGate.FailClosed();
                        DisposeDevice();
                        throw;
                    }
                }
                EnsureConnected();
                try
                {
                    if (Matches(cdb,
                            ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb()))
                    {
                        return ExecuteOperationalD8(target, lun,
                            requestedLength);
                    }
                    if (Matches(cdb,
                            ScsiFraming.BuildPrecisionTwoScannerReadyCdb()))
                    {
                        if (fullScanSuccessorObservationEnabled &&
                            liveFullScanRepeatArmed &&
                            fullScanInquiryGate.PrefixCompleted &&
                            operationalGate.State ==
                                AspiOperationalSequenceState.
                                    PreviewSetWindowReady)
                        {
                            return ExecuteLiveFullScanExtraScannerReady(
                                target, lun, requestedLength);
                        }
                        return ExecuteScannerReady(target, lun,
                            requestedLength);
                    }
                    if (OperationalSequenceEnabled && IsOperationalRead(cdb))
                    {
                        return ExecuteOperationalRead(target, lun, cdb,
                            requestedLength);
                    }

                    ScsiCommandResult result = device.ExecuteReadOnly(target,
                        lun, cdb, requestedLength);
                    if (target == 5 && lun == 0 && cdb.Length == 6 &&
                        cdb[0] == 0x12)
                    {
                        if (result.Status.RawStatus ==
                                (byte)AdapterStatus.Success)
                        {
                            RecordTargetFiveIdentity(result.Data);
                        }
                        if (fullScanInquiry.HasValue)
                        {
                            fullScanInquiryGate.Complete(
                                fullScanInquiry.Value,
                                result.Status.RawStatus,
                                result.Status.ActualLength,
                                result.Status.Residue,
                                discoveryGate.Identity ==
                                    PrecisionTwoIdentity.Operational);
                            if (fullScanInquiry.Value ==
                                    AspiFullScanInquiryKind.Prefix)
                            {
                                log.Info("LIVE ASPI full-scan successor " +
                                    "observed its exact operational INQUIRY " +
                                    "prefix.");
                            }
                            else
                            {
                                log.Info(string.Format(
                                    "LIVE ASPI full-scan in-stream " +
                                    "operational INQUIRY revalidation " +
                                    "completed: ordinal={0}/{1}, " +
                                    "after-rows={2}/{3}, status=0x{4:X2}, " +
                                    "actual={5}, residue={6}, " +
                                    "identity=M333.",
                                    fullScanInquiryGate.
                                        InStreamRevalidations,
                                    fullScanInquiryGate.
                                        MaximumAllowedInStreamRevalidations,
                                    livePreviewImageRowsCompleted,
                                    FullScanImageRowLimit,
                                    result.Status.RawStatus,
                                    result.Status.ActualLength,
                                    result.Status.Residue));
                            }
                        }
                        if (warmInquiryAttempt)
                        {
                            warmOperatorInquiryGate.Complete(
                                result.Status.RawStatus,
                                result.Status.ActualLength,
                                result.Status.Residue,
                                discoveryGate.Identity ==
                                    PrecisionTwoIdentity.Operational);
                            log.Warning("LIVE ASPI warm operator exact " +
                                "operational INQUIRY prefix completed; DF " +
                                "is now the only permitted successor.");
                        }
                        if (warmStabilizationInquiryAttempt)
                        {
                            warmOperatorInquiryGate.
                                CompleteStabilizationInquiry(
                                    result.Status.RawStatus,
                                    result.Status.ActualLength,
                                    result.Status.Residue,
                                    discoveryGate.Identity ==
                                        PrecisionTwoIdentity.Operational);
                            log.Warning(string.Format(
                                "LIVE ASPI warm operator stabilization " +
                                "M333 INQUIRY completed: ordinal={0}/{1}, " +
                                "counter={2}, status=0x{3:X2}, actual={4}, " +
                                "residue={5}.",
                                warmOperatorInquiryGate.
                                    StabilizationInquiriesCompleted,
                                AspiWarmOperatorInquiryGate.
                                    RequiredStabilizationInquiryCount,
                                operationalGate.
                                    CompletedInitializationCycles,
                                result.Status.RawStatus,
                                result.Status.ActualLength,
                                result.Status.Residue));
                        }
                    }
                    return Convert(result);
                }
                catch
                {
                    if (previewSequenceEnabled)
                    {
                        operationalGate.FailClosed();
                    }
                    if (fullScanSuccessorObservationEnabled &&
                        liveFullScanRepeatArmed)
                    {
                        fullScanInquiryGate.FailClosed();
                    }
                    if (warmOperatorCycleEnabled)
                    {
                        warmOperatorInquiryGate.FailClosed();
                    }
                    DisposeDevice();
                    throw;
                }
            }
        }

        public AspiTransportResult ExecuteDataOut(byte target, byte lun,
            byte[] cdb, byte[] data)
        {
            if (!loaderDataOutEnabled && !previewSetWindowEnabled)
            {
                throw new InvalidOperationException(
                    "ASPI data-out is not enabled for this process.");
            }
            if (cdb == null)
            {
                throw new ArgumentNullException("cdb");
            }
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            lock (sync)
            {
                try
                {
                    if (Matches(cdb,
                            ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb()))
                    {
                        if (fullScanSuccessorObservationEnabled &&
                            liveFullScanRepeatArmed &&
                            fullScanInquiryGate.PrefixCompleted &&
                            operationalGate.State ==
                                AspiOperationalSequenceState.
                                    PreviewSetWindowReady)
                        {
                            if (fullScanSetWindowExecutionEnabled)
                            {
                                operationalGate.
                                    BeginLiveFullScanSetWindowExecution(
                                        target, lun, cdb, data,
                                        PrecisionTwoPreviewCommandManifest.
                                            AspiLiveFullScanSetWindowSha256);
                                if (device == null ||
                                    discoveryGate.Identity !=
                                        PrecisionTwoIdentity.Operational)
                                {
                                    throw new InvalidOperationException(
                                        "Full-scan SET WINDOW requires the " +
                                        "retained exact Precision II " +
                                        "operational device session.");
                                }
                                ScsiCommandResult fullScanWindow = device.
                                    ExecutePrecisionTwoOperationalFullScanSetWindowOnce(
                                        data, livePreviewImageRowsCompleted,
                                        operationalGate.
                                            LivePreviewImageReadSubmissions);
                                operationalGate.
                                    RecordPreviewSetWindowCompletion(
                                        fullScanWindow.Status.RawStatus,
                                        fullScanWindow.Status.Residue,
                                        checked((int)fullScanWindow.Status.
                                            ActualLength));
                                if (fullScanWindow.Status.RawStatus !=
                                        (byte)AdapterStatus.Success ||
                                    fullScanWindow.Status.ActualLength !=
                                        ScsiFraming.
                                            PrecisionTwoPreviewSetWindowLength ||
                                    fullScanWindow.Status.Residue != 0)
                                {
                                    throw new ProtocolException(
                                        "Full-scan SET WINDOW did not " +
                                        "complete at its exact successful " +
                                        "84-byte boundary.");
                                }
                                liveFullScanSetWindowCompleted = true;
                                if (fullScanImageExecutionEnabled)
                                {
                                    operationalGate.
                                        ArmLiveFullScanImageStream(
                                            fullScanNaturalCompletionEnabled,
                                            fullScanRow997CompletionEnabled,
                                            fullScanProgressCompletionEnabled);
                                    livePreviewImageRowsCompleted = 0;
                                    liveFullScanCleanupCandidatesObserved = 0;
                                }
                                log.Warning(string.Format(
                                    "LIVE ASPI full-scan SET WINDOW completed " +
                                    "once: status=0x{0:X2}, actual={1}, " +
                                    "residue={2}, payload-sha256={3}.",
                                    fullScanWindow.Status.RawStatus,
                                    fullScanWindow.Status.ActualLength,
                                    fullScanWindow.Status.Residue,
                                    Sha256Hex(data)));
                                return Convert(fullScanWindow);
                            }
                            operationalGate.
                                ObserveLiveFullScanSetWindowSuccessor(target,
                                    lun, cdb, data);
                            log.Warning(
                                "LIVE ASPI full-scan SET WINDOW fingerprint " +
                                "captured and blocked before USB; " +
                                "payload-sha256=" + Sha256Hex(data) + ".");
                            DisposeDevice();
                            throw new InvalidOperationException(
                                "The full-scan SET WINDOW fingerprint was " +
                                "captured and remains blocked before USB.");
                        }
                        bool normalCleanupBoundary =
                            (!previewCancellationExecutionEnabled ||
                                operatorCycleEnabled) &&
                            previewImageShortRetryEnabled &&
                            livePreviewImageRowsCompleted ==
                                previewImageReadLimit &&
                            (operationalGate.State ==
                                AspiOperationalSequenceState.
                                    PredictedPreviewImageReadObserved ||
                             (!previewCleanupExecutionEnabled &&
                              operationalGate.State ==
                                AspiOperationalSequenceState.Failed));
                        bool cancellationCleanupBoundary =
                            previewCancellationExecutionEnabled &&
                            livePreviewImageRowsCompleted >=
                                PrecisionTwoPreviewCommandManifest.
                                    AspiLivePreviewCancellationTriggerRows &&
                            livePreviewImageRowsCompleted <=
                                PrecisionTwoPreviewCommandManifest.
                                    AspiLivePreviewCancellationMaximumRows &&
                            operationalGate.State ==
                                AspiOperationalSequenceState.
                                    PreviewImageStreaming;
                        bool fullScanCleanupBoundary =
                            fullScanImageExecutionEnabled &&
                            liveFullScanSetWindowCompleted &&
                            (fullScanProgressCompletionEnabled
                                ? livePreviewImageRowsCompleted >=
                                    PrecisionTwoPreviewCommandManifest.
                                        AspiLiveFullScanProgressMinimumCleanupRows &&
                                  livePreviewImageRowsCompleted <=
                                    FullScanImageRowLimit
                                : livePreviewImageRowsCompleted ==
                                    FullScanImageRowLimit) &&
                            operationalGate.State ==
                                AspiOperationalSequenceState.
                                    PreviewImageStreaming;
                        if (normalCleanupBoundary ||
                            cancellationCleanupBoundary ||
                            fullScanCleanupBoundary)
                        {
                            PrecisionTwoPreviewCommandManifest.
                                ValidateCleanupSetWindow(cdb, data,
                                    fullScanCleanupBoundary
                                        ? PrecisionTwoPreviewCommandManifest.
                                            AspiLiveFullScanCleanupSetWindowSha256
                                        : PrecisionTwoPreviewCommandManifest.
                                            AspiLiveCleanupSetWindowSha256);
                            int cleanupCandidatesObserved =
                                fullScanCleanupBoundary
                                    ? liveFullScanCleanupCandidatesObserved
                                    : livePreviewCleanupCandidatesObserved;
                            if (cleanupCandidatesObserved >=
                                    PrecisionTwoPreviewCommandManifest.
                                        AspiLivePreviewCleanupSetWindowCount)
                            {
                                throw new InvalidOperationException(
                                    "More than two post-image cleanup SET " +
                                    "WINDOW candidates were constructed.");
                            }
                            ++cleanupCandidatesObserved;
                            if (fullScanCleanupBoundary)
                            {
                                liveFullScanCleanupCandidatesObserved =
                                    cleanupCandidatesObserved;
                            }
                            else
                            {
                                livePreviewCleanupCandidatesObserved =
                                    cleanupCandidatesObserved;
                            }
                            if (!fullScanCleanupBoundary &&
                                !previewCleanupExecutionEnabled &&
                                !previewCancellationExecutionEnabled)
                            {
                                log.Warning(string.Format(
                                    "LIVE ASPI exact post-image cleanup SET " +
                                    "WINDOW successor observed and blocked " +
                                    "before USB: ordinal={0}/2, " +
                                    "payload-sha256={1}. Cleanup permission " +
                                    "is not enabled.",
                                    cleanupCandidatesObserved,
                                    Sha256Hex(data)));
                                operationalGate.FailClosed();
                                DisposeDevice();
                                throw new InvalidOperationException(
                                    "The exact post-image cleanup SET WINDOW " +
                                    "was observed but remains blocked before " +
                                    "USB.");
                            }
                            if (device == null || discoveryGate.Identity !=
                                    PrecisionTwoIdentity.Operational)
                            {
                                throw new InvalidOperationException(
                                    "Image cleanup SET WINDOW requires the " +
                                    "retained exact Precision II operational " +
                                    "device session.");
                            }
                            ScsiCommandResult cleanupResult =
                                fullScanCleanupBoundary
                                ? device.
                                    ExecutePrecisionTwoOperationalFullScanCleanupSetWindowOnce(
                                        data, cleanupCandidatesObserved - 1,
                                        livePreviewImageRowsCompleted,
                                        operationalGate.
                                            LivePreviewImageReadSubmissions,
                                        FullScanImageRowLimit,
                                        FullScanMaximumReadSubmissions,
                                        FullScanMaximumShortRetries,
                                        fullScanProgressCompletionEnabled
                                            ? PrecisionTwoPreviewCommandManifest.
                                                AspiLiveFullScanProgressMinimumCleanupRows
                                            : FullScanImageRowLimit)
                                : cancellationCleanupBoundary
                                ? device.
                                    ExecutePrecisionTwoOperationalPreviewCancellationCleanupOnce(
                                        data,
                                        cleanupCandidatesObserved - 1,
                                        livePreviewImageRowsCompleted,
                                        operationalGate.
                                            LivePreviewImageReadSubmissions)
                                : device.
                                    ExecutePrecisionTwoOperationalPreviewCleanupSetWindowOnce(
                                        data,
                                        cleanupCandidatesObserved - 1,
                                        livePreviewImageRowsCompleted,
                                        operationalGate.
                                            LivePreviewImageReadSubmissions);
                            if (cleanupResult.Status.RawStatus !=
                                    (byte)AdapterStatus.Success ||
                                cleanupResult.Status.ActualLength !=
                                    ScsiFraming.
                                        PrecisionTwoPreviewSetWindowLength ||
                                cleanupResult.Status.Residue != 0)
                            {
                                throw new ProtocolException(string.Format(
                                    "Image cleanup SET WINDOW {0}/2 did " +
                                    "not complete exactly: status=0x{1:X2}, " +
                                    "actual={2}, residue={3}.",
                                    cleanupCandidatesObserved,
                                    cleanupResult.Status.RawStatus,
                                    cleanupResult.Status.ActualLength,
                                    cleanupResult.Status.Residue));
                            }
                            log.Warning(string.Format(
                                fullScanCleanupBoundary
                                ? "LIVE ASPI exact full-scan cleanup SET " +
                                    "WINDOW completed: ordinal={0}/2, " +
                                    "status=0x{1:X2}, actual={2}, residue={3}, " +
                                    "payload-sha256={4}."
                                : cancellationCleanupBoundary
                                ? "LIVE ASPI exact cancellation cleanup SET " +
                                    "WINDOW completed: ordinal={0}/2, " +
                                    "status=0x{1:X2}, actual={2}, residue={3}, " +
                                    "payload-sha256={4}."
                                : "LIVE ASPI exact post-image cleanup SET " +
                                "WINDOW completed: ordinal={0}/2, " +
                                "status=0x{1:X2}, actual={2}, residue={3}, " +
                                "payload-sha256={4}.",
                                cleanupCandidatesObserved,
                                cleanupResult.Status.RawStatus,
                                cleanupResult.Status.ActualLength,
                                cleanupResult.Status.Residue,
                                Sha256Hex(data)));
                            if (fullScanCleanupBoundary &&
                                fullScanProgressCompletionEnabled &&
                                cleanupCandidatesObserved == 1)
                            {
                                log.Warning(string.Format(
                                    "LIVE ASPI exact bounded full-scan image " +
                                    "stream completed: rows={0}/{1}, " +
                                    "submissions={2}/{3}, short-retries={4}/{5}.",
                                    livePreviewImageRowsCompleted,
                                    FullScanImageRowLimit,
                                    operationalGate.
                                        LivePreviewImageReadSubmissions,
                                    FullScanMaximumReadSubmissions,
                                    operationalGate.LivePreviewShortRetries,
                                    FullScanMaximumShortRetries));
                            }
                            if (cleanupCandidatesObserved ==
                                    PrecisionTwoPreviewCommandManifest.
                                        AspiLivePreviewCleanupSetWindowCount)
                            {
                                if (fullScanCleanupBoundary)
                                {
                                    liveFullScanCompleted = true;
                                    log.Warning(string.Format(
                                        "LIVE ASPI exact full-scan transport " +
                                        "completed after {0} rows and two " +
                                        "cleanup SET WINDOW completions.",
                                        livePreviewImageRowsCompleted));
                                    DisposeDevice();
                                }
                                else if (cancellationCleanupBoundary &&
                                    operatorCycleEnabled)
                                {
                                    liveOperatorCancellationCompleted = true;
                                    log.Warning(
                                        "LIVE ASPI operator cycle completed " +
                                        "after exact cancellation cleanup; " +
                                        "a session wrapper may create a " +
                                        "fresh cycle for the next command.");
                                    DisposeDevice();
                                }
                                else if (fullScanSuccessorObservationEnabled)
                                {
                                    operationalGate.
                                        BeginLiveFullScanSuccessorObservation();
                                    fullScanInquiryGate.Arm(
                                        livePreviewImageRowsCompleted,
                                        FullScanImageRowLimit);
                                    liveFullScanRepeatArmed = true;
                                    log.Warning(
                                        "LIVE ASPI full-scan successor " +
                                        "observer armed after two exact " +
                                        "Preview cleanup completions; the " +
                                        "retained session now permits one " +
                                        "operational INQUIRY prefix, bounded " +
                                        "read-only repeat initialization, and " +
                                        "exact in-stream M333 revalidation.");
                                }
                                else
                                {
                                    DisposeDevice();
                                }
                            }
                            return Convert(cleanupResult);
                        }
                        if (warmOperatorCycleEnabled &&
                            liveWarmPreviewInitializationStarted &&
                            !liveWarmPreviewSetWindowCompleted &&
                            (!warmOperatorInquiryGate.
                                StabilizationSequenceCompleted ||
                             operationalGate.CompletedInitializationCycles !=
                                AspiOperationalSequenceGate.
                                    MaximumInitializationCycles - 1))
                        {
                            throw new InvalidOperationException(
                                "Warm Preview SET WINDOW requires two exact " +
                                "counter-bound M333 revalidations and two " +
                                "completed stabilization cycles after its " +
                                "seeded counter.");
                        }
                        operationalGate.ObservePreviewSetWindow(target, lun,
                            cdb, data);
                        if (!previewSetWindowEnabled)
                        {
                            log.Warning(
                                "LIVE ASPI recognized the exact Preview SET " +
                                "WINDOW successor and blocked it before USB; " +
                                "scan-changing data-out is not enabled.");
                            throw new InvalidOperationException(
                                "Exact Preview SET WINDOW was observed but " +
                                "remains blocked before USB pending a " +
                                "separate hardware-test approval.");
                        }
                        if (device == null || discoveryGate.Identity !=
                                PrecisionTwoIdentity.Operational)
                        {
                            throw new InvalidOperationException(
                                "Preview SET WINDOW requires the active exact " +
                                "Precision II operational device session.");
                        }
                        ScsiCommandResult previewResult = device.
                            ExecutePrecisionTwoOperationalPreviewSetWindowOnce(
                                data);
                        operationalGate.RecordPreviewSetWindowCompletion(
                            previewResult.Status.RawStatus,
                            previewResult.Status.Residue,
                            checked((int)previewResult.Status.ActualLength));
                        if (warmOperatorCycleEnabled &&
                            liveWarmPreviewInitializationStarted)
                        {
                            liveWarmPreviewSetWindowCompleted = true;
                        }
                        log.Warning(string.Format(
                            "LIVE ASPI exact Preview SET WINDOW completed once: " +
                            "status=0x{0:X2}, actual={1}, residue={2}; two " +
                            "ScannerReady successors are now the only enabled " +
                            "commands before the configured bounded image-read " +
                            "boundary.",
                            previewResult.Status.RawStatus,
                            previewResult.Status.ActualLength,
                            previewResult.Status.Residue));
                        return Convert(previewResult);
                    }

                    if (!loaderDataOutEnabled)
                    {
                        throw new InvalidOperationException(
                            "Only the exact one-shot Preview SET WINDOW is " +
                            "enabled in this process; loader writes remain " +
                            "disabled.");
                    }
                    int recordIndex = loaderGate.Begin(target, lun, cdb, data,
                        discoveryGate.LoaderSequenceReady);
                    if (device == null)
                    {
                        throw new InvalidOperationException(
                            "The loader D8 device session is no longer active.");
                    }
                    ScsiCommandResult result = recordIndex >= 0
                        ? device.ExecutePrecisionTwoLoaderWriteBufferRecordOnce(
                            data, recordIndex)
                        : device.ExecutePrecisionTwoLoaderWriteBufferTerminalOnce(
                            data);
                    loaderGate.Complete(result.Status.RawStatus,
                        result.Status.Residue,
                        checked((int)result.Status.ActualLength));
                    log.Info(string.Format(
                        "LIVE ASPI loader {0} completed: status=0x{1:X2}, " +
                        "actual={2}, residue={3}, accepted-records={4}.",
                        recordIndex >= 0 ?
                            "record " + (recordIndex + 1).ToString() :
                            "terminal",
                        result.Status.RawStatus, result.Status.ActualLength,
                        result.Status.Residue,
                        loaderGate.AcceptedRecordCount));
                    if (recordIndex < 0)
                    {
                        discoveryGate.RecordLoaderTransitionCompleted();
                    }
                    return Convert(result);
                }
                catch
                {
                    if (loaderDataOutEnabled)
                    {
                        loaderGate.FailClosed();
                    }
                    operationalGate.FailClosed();
                    if (warmOperatorCycleEnabled)
                    {
                        warmOperatorInquiryGate.FailClosed();
                    }
                    DisposeDevice();
                    throw;
                }
            }
        }

        private void FailWarmOperatorCycle(string message)
        {
            operationalGate.FailClosed();
            warmOperatorInquiryGate.FailClosed();
            DisposeDevice();
            throw new InvalidOperationException(message);
        }

        private AspiTransportResult ExecuteOperationalD8(byte target,
            byte lun, uint requestedLength)
        {
            if (discoveryGate.Identity == PrecisionTwoIdentity.Unknown)
            {
                RecordTargetFiveIdentity(device.Inquiry(5, 0));
            }
            if (previewSequenceEnabled && discoveryGate.Identity !=
                    PrecisionTwoIdentity.Operational)
            {
                throw new InvalidOperationException(
                    "Live Preview observation requires exact operational " +
                    "Imacon/FlexTight II/M333 identity; loader-state D8 is " +
                    "not enabled in this mode.");
            }
            discoveryGate.BeginD8(target, lun, requestedLength);

            ScsiCommandResult result;
            if (discoveryGate.Identity == PrecisionTwoIdentity.Loader)
            {
                result = device.ReadPrecisionTwoLoaderBufferD8Once();
            }
            else if (discoveryGate.Identity == PrecisionTwoIdentity.Operational)
            {
                result = device.ReadPrecisionTwoOperationalBufferD8Once();
            }
            else
            {
                throw new InvalidOperationException(
                    "READ BUFFER D8 requires the exact Precision II identity.");
            }

            discoveryGate.RecordD8(result.Status.RawStatus,
                result.Status.Residue, result.Data.Length);
            if (OperationalSequenceEnabled &&
                discoveryGate.Identity == PrecisionTwoIdentity.Operational)
            {
                operationalGate.RecordOperationalD8Completion(
                    result.Status.RawStatus, result.Status.Residue,
                    checked((int)result.Status.ActualLength), result.Data);
            }
            log.Info(string.Format(
                "LIVE ASPI READ BUFFER D8 completed: identity={0}, " +
                "status=0x{1:X2}, length={2}, residue={3}.",
                discoveryGate.Identity, result.Status.RawStatus,
                result.Data.Length, result.Status.Residue));
            return Convert(result);
        }

        private AspiTransportResult ExecuteScannerReady(byte target,
            byte lun, uint requestedLength)
        {
            if (loaderDataOutEnabled &&
                discoveryGate.PostLoaderScannerReadyRequired)
            {
                discoveryGate.BeginPostLoaderScannerReady(target, lun,
                    requestedLength);
                ScsiCommandResult postLoaderResult =
                    device.ReadPrecisionTwoOperationalScannerReadyOnce();
                discoveryGate.RecordPostLoaderScannerReady(
                    postLoaderResult.Status.RawStatus,
                    postLoaderResult.Status.Residue,
                    checked((int)postLoaderResult.Status.ActualLength),
                    postLoaderResult.Data);
                log.Info(string.Format(
                    "LIVE ASPI post-loader ScannerReady completed: " +
                    "status=0x{0:X2}, length={1}, residue={2}.",
                    postLoaderResult.Status.RawStatus,
                    postLoaderResult.Status.ActualLength,
                    postLoaderResult.Status.Residue));
                return Convert(postLoaderResult);
            }
            if (OperationalSequenceEnabled &&
                discoveryGate.Identity == PrecisionTwoIdentity.Operational)
            {
                return ExecuteOperationalRead(target, lun,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                    requestedLength);
            }

            discoveryGate.BeginScannerReady(target, lun, requestedLength);
            ScsiCommandResult result =
                device.ReadPrecisionTwoOperationalScannerReadyOnce();
            log.Info(string.Format(
                "LIVE ASPI ScannerReady completed: status=0x{0:X2}, " +
                "length={1}.", result.Status.RawStatus, result.Data.Length));
            return Convert(result);
        }

        private AspiTransportResult ExecuteLiveFullScanExtraScannerReady(
            byte target, byte lun, uint requestedLength)
        {
            if (target != 5 || lun != 0 || requestedLength !=
                    ScsiFraming.PrecisionTwoScannerReadyLength ||
                liveFullScanExtraScannerReadyCompleted)
            {
                throw new InvalidOperationException(
                    "The extra full-scan ScannerReady request is outside " +
                    "its exact target, length, or one-phase boundary.");
            }
            long now = GetMonotonicMilliseconds();
            if (liveFullScanExtraScannerReadyStartedMilliseconds < 0)
            {
                liveFullScanExtraScannerReadyStartedMilliseconds = now;
            }
            long elapsed = now -
                liveFullScanExtraScannerReadyStartedMilliseconds;
            if (liveFullScanExtraScannerReadyAttempts >=
                    AspiOperationalSequenceGate.
                        MaximumPostWindowScannerReadyAttemptsPerPhase ||
                elapsed >= AspiOperationalSequenceGate.
                    MaximumPostWindowScannerReadyPhaseMilliseconds)
            {
                throw new ProtocolException(
                    "The extra full-scan ScannerReady phase exceeded its " +
                    "bounded attempt or time allowance.");
            }
            ++liveFullScanExtraScannerReadyAttempts;
            ScsiCommandResult result =
                device.ReadPrecisionTwoOperationalScannerReadyOnce();
            if (result.Status.RawStatus != (byte)AdapterStatus.Success ||
                result.Status.Residue != 0 || result.Status.ActualLength !=
                    ScsiFraming.PrecisionTwoScannerReadyLength ||
                result.Data.Length !=
                    ScsiFraming.PrecisionTwoScannerReadyLength ||
                (result.Data[0] != 0 && result.Data[0] != 8) ||
                result.Data[1] != 0)
            {
                throw new ProtocolException(
                    "The extra full-scan ScannerReady response was not an " +
                    "exact 08 00 active or 00 00 ready completion.");
            }
            liveFullScanExtraScannerReadyCompleted = result.Data[0] == 0;
            log.Info(string.Format(
                "LIVE ASPI full-scan extra ScannerReady response: " +
                "attempt={0}/{1}, elapsed-ms={2}/{3}, first=0x{4:X2}, " +
                "second=0x{5:X2}, ready={6}.",
                liveFullScanExtraScannerReadyAttempts,
                AspiOperationalSequenceGate.
                    MaximumPostWindowScannerReadyAttemptsPerPhase,
                elapsed, AspiOperationalSequenceGate.
                    MaximumPostWindowScannerReadyPhaseMilliseconds,
                result.Data[0], result.Data[1],
                liveFullScanExtraScannerReadyCompleted));
            return Convert(result);
        }

        private AspiTransportResult ExecuteOperationalRead(byte target,
            byte lun, byte[] cdb, uint requestedLength)
        {
            AspiOperationalSequenceState originState = operationalGate.State;
            AspiOperationalReadCommand command = operationalGate.BeginRead(
                target, lun, cdb, requestedLength);
            int postWindowScannerReadyAttempt = operationalGate.
                PendingPostWindowScannerReadyAttempt;
            int inStreamScannerReadyPoll = operationalGate.
                PendingLivePreviewScannerReadyPoll;
            int fullScanInitialScannerReadyAttempt = operationalGate.
                PendingLiveFullScanInitialScannerReadyAttempt;
            long fullScanInitialScannerReadyElapsed = operationalGate.
                PendingLiveFullScanInitialScannerReadyElapsedMilliseconds;
            int inStreamRows = operationalGate.LivePreviewImageRows;
            int inStreamSubmissions = previewImageShortRetryEnabled
                ? operationalGate.LivePreviewImageReadSubmissions
                : inStreamRows;
            ScsiCommandResult result;
            try
            {
                switch (command)
                {
                    case AspiOperationalReadCommand.ScannerReady:
                        if (originState == AspiOperationalSequenceState.
                                PreviewImageStreaming &&
                            fullScanImageExecutionEnabled &&
                            operationalGate.LiveFullScanImageStreamArmed)
                        {
                            result = device.
                                ReadPrecisionTwoOperationalFullScanStreamScannerReadyOnce(
                                    inStreamRows, inStreamSubmissions,
                                    inStreamScannerReadyPoll - 1,
                                    FullScanImageRowLimit,
                                    FullScanMaximumReadSubmissions,
                                    FullScanMaximumShortRetries,
                                    PrecisionTwoPreviewCommandManifest.
                                        AspiLiveFullScanMaximumScannerReadyPolls);
                        }
                        else if (originState == AspiOperationalSequenceState.
                                PreviewImageStreaming &&
                            (previewImageReadLimit ==
                                PrecisionTwoPreviewCommandManifest.
                                    AspiLivePreviewStreamRows ||
                             previewImageReadLimit ==
                                PrecisionTwoPreviewCommandManifest.
                                    AspiLivePreviewNaturalRows ||
                             previewImageReadLimit ==
                                PrecisionTwoPreviewCommandManifest.
                                    AspiLivePreviewPoweredNaturalRows))
                        {
                            result = device.
                                ReadPrecisionTwoOperationalPreviewStreamScannerReadyOnce(
                                    inStreamRows, inStreamSubmissions,
                                    inStreamScannerReadyPoll - 1,
                                    previewImageReadLimit,
                                    previewMaximumReadSubmissions,
                                    previewMaximumShortRetries,
                                    previewMaximumScannerReadyPolls);
                        }
                        else
                        {
                            result = device.
                                ReadPrecisionTwoOperationalScannerReadyOnce();
                        }
                        break;
                    case AspiOperationalReadCommand.FaultPixelHeader:
                        result = device.
                            ReadPrecisionTwoOperationalFaultPixelBufferOnce();
                        break;
                    case AspiOperationalReadCommand.FaultPixelData:
                        result = device.
                            ReadPrecisionTwoOperationalFaultPixelDataBufferOnce();
                        break;
                    case AspiOperationalReadCommand.Calibration:
                        result = device.
                            ReadPrecisionTwoOperationalCalibrationBufferOnce();
                        break;
                    case AspiOperationalReadCommand.D8Offset55:
                        result = device.
                            ReadPrecisionTwoOperationalD8Offset55BufferOnce();
                        break;
                    case AspiOperationalReadCommand.DynamicConfigurationD8:
                        result = device.
                            ReadPrecisionTwoOperationalDynamicConfigurationD8Once();
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Unknown operational ASPI read command.");
                }
            }
            catch
            {
                LogInStreamScannerReadyFailure(originState, inStreamRows,
                    inStreamSubmissions, inStreamScannerReadyPoll);
                throw;
            }

            if (command == AspiOperationalReadCommand.ScannerReady &&
                fullScanInitialScannerReadyAttempt != 0)
            {
                if (result.Data != null && result.Data.Length ==
                        ScsiFraming.PrecisionTwoScannerReadyLength)
                {
                    log.Info(string.Format(
                        "LIVE ASPI full-scan initial ScannerReady response: " +
                        "attempt={0}/{1}, phase-elapsed-ms={2}/{3}, " +
                        "status=0x{4:X2}, actual={5}, residue={6}, " +
                        "first=0x{7:X2}, second=0x{8:X2}, ready={9}.",
                        fullScanInitialScannerReadyAttempt,
                        AspiOperationalSequenceGate.
                            MaximumPostWindowScannerReadyAttemptsPerPhase,
                        fullScanInitialScannerReadyElapsed,
                        AspiOperationalSequenceGate.
                            MaximumPostWindowScannerReadyPhaseMilliseconds,
                        result.Status.RawStatus,
                        result.Status.ActualLength,
                        result.Status.Residue, result.Data[0], result.Data[1],
                        result.Status.RawStatus ==
                                (byte)AdapterStatus.Success &&
                            result.Status.Residue == 0 &&
                            result.Status.ActualLength ==
                                ScsiFraming.PrecisionTwoScannerReadyLength &&
                            result.Data[0] == 0 && result.Data[1] == 0));
                }
                else
                {
                    log.Warning(string.Format(
                        "LIVE ASPI full-scan initial ScannerReady returned " +
                        "an invalid raw envelope: attempt={0}/{1}, " +
                        "phase-elapsed-ms={2}/{3}, status=0x{4:X2}, " +
                        "actual={5}, residue={6}, data-length={7}.",
                        fullScanInitialScannerReadyAttempt,
                        AspiOperationalSequenceGate.
                            MaximumPostWindowScannerReadyAttemptsPerPhase,
                        fullScanInitialScannerReadyElapsed,
                        AspiOperationalSequenceGate.
                            MaximumPostWindowScannerReadyPhaseMilliseconds,
                        result.Status.RawStatus,
                        result.Status.ActualLength,
                        result.Status.Residue,
                        result.Data == null ? -1 : result.Data.Length));
                }
            }

            bool advanced;
            try
            {
                advanced = operationalGate.CompleteRead(
                    result.Status.RawStatus,
                    result.Status.Residue,
                    checked((int)result.Status.ActualLength), result.Data);
            }
            catch
            {
                LogInStreamScannerReadyFailure(originState, inStreamRows,
                    inStreamSubmissions, inStreamScannerReadyPoll);
                throw;
            }
            long postWindowScannerReadyElapsed = operationalGate.
                LastPostWindowScannerReadyCompletionElapsedMilliseconds;
            if (command == AspiOperationalReadCommand.ScannerReady &&
                result.Status.RawStatus == (byte)AdapterStatus.Success &&
                result.Status.Residue == 0 &&
                result.Status.ActualLength ==
                    ScsiFraming.PrecisionTwoScannerReadyLength &&
                result.Data.Length ==
                    ScsiFraming.PrecisionTwoScannerReadyLength)
            {
                log.Info(string.Format(
                    "LIVE ASPI operational ScannerReady response: " +
                    "origin={0}, attempt={1}/{2}, phase-elapsed-ms={3}/{4}, " +
                    "first=0x{5:X2}, second=0x{6:X2}.", originState,
                    postWindowScannerReadyAttempt,
                    postWindowScannerReadyAttempt == 0 ? 0 :
                        AspiOperationalSequenceGate.
                            MaximumPostWindowScannerReadyAttemptsPerPhase,
                    postWindowScannerReadyElapsed,
                    postWindowScannerReadyAttempt == 0 ? 0 :
                        AspiOperationalSequenceGate.
                            MaximumPostWindowScannerReadyPhaseMilliseconds,
                    result.Data[0], result.Data[1]));
            }
            if (command == AspiOperationalReadCommand.ScannerReady &&
                postWindowScannerReadyAttempt != 0 && !advanced)
            {
                log.Warning(string.Format(
                    "LIVE ASPI post-window ScannerReady was not ready; " +
                    "FlexColor remains in {0} after attempt {1}/{2} at " +
                    "phase-elapsed-ms={3}/{4}.",
                    originState, postWindowScannerReadyAttempt,
                    AspiOperationalSequenceGate.
                        MaximumPostWindowScannerReadyAttemptsPerPhase,
                    postWindowScannerReadyElapsed,
                    AspiOperationalSequenceGate.
                        MaximumPostWindowScannerReadyPhaseMilliseconds));
                if (postWindowScannerReadyAttempt ==
                    AspiOperationalSequenceGate.
                        MaximumPostWindowScannerReadyAttemptsPerPhase ||
                    postWindowScannerReadyElapsed >=
                        AspiOperationalSequenceGate.
                            MaximumPostWindowScannerReadyPhaseMilliseconds)
                {
                    log.Warning(
                        "LIVE ASPI post-window ScannerReady retry limit " +
                        "reached at the bounded attempt/time boundary; " +
                        "the next poll will be blocked before USB.");
                }
            }
            if (command == AspiOperationalReadCommand.ScannerReady &&
                fullScanInitialScannerReadyAttempt != 0 && !advanced)
            {
                log.Warning(string.Format(
                    "LIVE ASPI full-scan initial ScannerReady was active; " +
                    "FlexColor remains at repeat initialization after " +
                    "attempt {0}/{1}, phase-elapsed-ms={2}/{3}.",
                    fullScanInitialScannerReadyAttempt,
                    AspiOperationalSequenceGate.
                        MaximumPostWindowScannerReadyAttemptsPerPhase,
                    fullScanInitialScannerReadyElapsed,
                    AspiOperationalSequenceGate.
                        MaximumPostWindowScannerReadyPhaseMilliseconds));
            }
            if (command == AspiOperationalReadCommand.ScannerReady &&
                inStreamScannerReadyPoll != 0)
            {
                log.Info(string.Format(
                    "LIVE ASPI in-stream ScannerReady response: " +
                    "after-rows={0}, poll={1}/{2}, status=0x{3:X2}, " +
                    "actual={4}, residue={5}, first=0x{6:X2}, " +
                    "second=0x{7:X2}.",
                    inStreamRows, inStreamScannerReadyPoll,
                    previewMaximumScannerReadyPolls,
                    result.Status.RawStatus, result.Status.ActualLength,
                    result.Status.Residue, result.Data[0], result.Data[1]));
            }
            log.Info(string.Format(
                "LIVE ASPI operational {0} completed: status=0x{1:X2}, " +
                "length={2}, residue={3}, cycles={4}, advanced={5}, " +
                "next={6}.",
                command, result.Status.RawStatus, result.Data.Length,
                result.Status.Residue,
                operationalGate.CompletedInitializationCycles,
                advanced, operationalGate.State));
            return Convert(result);
        }

        private void LogInStreamScannerReadyFailure(
            AspiOperationalSequenceState originState, int rows,
            int submissions, int poll)
        {
            if (originState != AspiOperationalSequenceState.
                    PreviewImageStreaming || log == null)
            {
                return;
            }
            if (previewImageShortRetryEnabled)
            {
                log.Warning(string.Format(
                    "LIVE ASPI bounded Preview short-retry stream failed " +
                    "closed: after-rows={0}/{1}, submissions={2}/{3}, " +
                    "short-retries={4}/{5}, ScannerReady-poll={6}/{7}. " +
                    "Every later image, poll, or cleanup command remains " +
                    "blocked before USB.", rows, previewImageReadLimit,
                    submissions, previewMaximumReadSubmissions,
                    operationalGate.LivePreviewShortRetries,
                    previewMaximumShortRetries, poll,
                    previewMaximumScannerReadyPolls));
                return;
            }
            log.Warning(string.Format(
                "LIVE ASPI bounded Preview stream failed closed: " +
                "after-rows={0}, ScannerReady-poll={1}. Every later image, " +
                "poll, or cleanup command remains blocked before USB.",
                rows, poll));
        }

        private AspiTransportResult ObserveAndBlockPredictedPreviewImageRead(
            byte target, byte lun, byte[] cdb, uint requestedLength)
        {
            uint scanWidth = operationalGate.ObservePredictedPreviewImageRead(
                target, lun, cdb, requestedLength);
            log.Warning(string.Format(
                "LIVE ASPI observed the predicted first Preview image READ " +
                "after SET WINDOW and two ScannerReady phases: width={0}, " +
                "length={1}, selector=0x{2:X2}. It was blocked before USB.",
                scanWidth, requestedLength, cdb[4]));
            throw new InvalidOperationException(
                "The first Preview image READ was captured and blocked before " +
                "USB by the live-preview-observe safety boundary.");
        }

        private AspiTransportResult
            ObserveAndBlockLiveFullScanFirstImageRead(byte target, byte lun,
                byte[] cdb, uint requestedLength)
        {
            uint scanWidth = operationalGate.
                ObserveLiveFullScanFirstImageRead(target, lun, cdb,
                    requestedLength);
            log.Warning(string.Format(
                "LIVE ASPI full-scan first image READ fingerprint captured " +
                "and blocked before USB: width={0}, length={1}, " +
                "selector=0x{2:X2}, CDB={3}.", scanWidth, requestedLength,
                cdb[4], ScsiFraming.ToHex(cdb)));
            DisposeDevice();
            throw new InvalidOperationException(
                "The full-scan first image READ was fingerprinted and " +
                "remains blocked before USB.");
        }

        private AspiTransportResult
            ExecuteBoundedFullScanImageShortRetryRead(byte target, byte lun,
                byte[] cdb, uint requestedLength)
        {
            int rowIndex = operationalGate.LivePreviewImageRows;
            int submissionIndex = operationalGate.
                LivePreviewImageReadSubmissions;
            try
            {
                uint scanWidth = operationalGate.
                    BeginLivePreviewImageShortRetryRead(target, lun, cdb,
                        requestedLength);
                EnsureConnected();
                ScsiCommandResult result = device.
                    ReadPrecisionTwoOperationalFullScanImageShortRetryAttempt(
                        cdb, requestedLength, rowIndex, submissionIndex,
                        FullScanImageRowLimit,
                        FullScanMaximumReadSubmissions,
                        FullScanMaximumShortRetries);
                AspiLivePreviewImageCompletion completion = operationalGate.
                    CompleteLivePreviewImageShortRetryRead(
                        result.Status.RawStatus, requestedLength,
                        result.Status.Residue,
                        checked((int)result.Status.ActualLength), result.Data);
                if (completion ==
                        AspiLivePreviewImageCompletion.RetryShortRow)
                {
                    log.Warning(string.Format(
                        "LIVE ASPI exact full-scan short completion converted " +
                        "to bounded target BUSY: row={0}/{1}, " +
                        "short-retry={2}/{3}, consecutive={4}/{5}, " +
                        "submission={6}/{7}, status=0x{8:X2}, " +
                        "requested={9}, actual={10}, residue={11}, width={12}, " +
                        "selector=0x{13:X2}, payload-sha256={14}. No short " +
                        "payload bytes were copied to FlexColor.",
                        rowIndex + 1,
                        FullScanImageRowLimit,
                        operationalGate.LivePreviewShortRetries,
                        FullScanMaximumShortRetries,
                        operationalGate.LivePreviewConsecutiveShortRetries,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanMaximumConsecutiveShortRetries,
                        operationalGate.LivePreviewImageReadSubmissions,
                        FullScanMaximumReadSubmissions,
                        result.Status.RawStatus, requestedLength,
                        result.Data.Length, result.Status.Residue, scanWidth,
                        cdb[4], Sha256Hex(result.Data)));
                    BackoffAfterLiveImageShortCompletion();
                    return new AspiTransportResult(new byte[0],
                        (byte)AdapterStatus.Busy);
                }

                livePreviewImageRowsCompleted = operationalGate.
                    LivePreviewImageRows;
                log.Warning(string.Format(
                    "LIVE ASPI exact bounded full-scan image READ completed: " +
                    "row={0}/{1}, status=0x{2:X2}, requested={3}, actual={4}, " +
                    "residue={5}, width={6}, selector=0x{7:X2}, " +
                    "submission={8}/{9}, short-retries={10}/{11}, " +
                    "payload-sha256={12}.",
                    livePreviewImageRowsCompleted,
                    FullScanImageRowLimit,
                    result.Status.RawStatus, requestedLength,
                    result.Data.Length, result.Status.Residue, scanWidth,
                    cdb[4], operationalGate.LivePreviewImageReadSubmissions,
                    FullScanMaximumReadSubmissions,
                    operationalGate.LivePreviewShortRetries,
                    FullScanMaximumShortRetries,
                    Sha256Hex(result.Data)));
                if (livePreviewImageRowsCompleted ==
                        FullScanImageRowLimit)
                {
                    log.Warning(string.Format(
                        "LIVE ASPI exact bounded full-scan image stream " +
                        "completed: rows={0}/{0}, submissions={1}/{2}, " +
                        "short-retries={3}/{4}. " +
                        (fullScanTerminalProbeExecutionEnabled
                            ? "One separately approved quarantined row {5} " +
                              "probe is now eligible; cleanup remains " +
                              "blocked."
                            : "Row {5} is blocked before USB; only bounded " +
                              "ScannerReady and two hash-pinned full-scan " +
                              "cleanup SET WINDOW successors remain."),
                        FullScanImageRowLimit,
                        operationalGate.LivePreviewImageReadSubmissions,
                        FullScanMaximumReadSubmissions,
                        operationalGate.LivePreviewShortRetries,
                        FullScanMaximumShortRetries,
                        FullScanImageRowLimit + 1));
                }
                return Convert(result);
            }
            catch
            {
                log.Warning(string.Format(
                    "LIVE ASPI bounded full-scan image stream failed closed: " +
                    "row={0}/{1}, submissions={2}/{3}, short-retries={4}/{5}, " +
                    "consecutive={6}/{7}.", rowIndex + 1,
                    FullScanImageRowLimit,
                    operationalGate.LivePreviewImageReadSubmissions,
                    FullScanMaximumReadSubmissions,
                    operationalGate.LivePreviewShortRetries,
                    FullScanMaximumShortRetries,
                    operationalGate.LivePreviewConsecutiveShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanMaximumConsecutiveShortRetries));
                throw;
            }
            finally
            {
                if (operationalGate.State ==
                        AspiOperationalSequenceState.Failed)
                {
                    DisposeDevice();
                }
            }
        }

        private AspiTransportResult ExecuteFullScanTerminalProbeRead(
            byte target, byte lun, byte[] cdb, uint requestedLength)
        {
            int submissionIndex = operationalGate.
                LivePreviewImageReadSubmissions;
            uint scanWidth = operationalGate.
                BeginLiveFullScanTerminalProbeRead(target, lun, cdb,
                    requestedLength);
            try
            {
                EnsureConnected();
                ScsiCommandResult result = device.
                    ReadPrecisionTwoOperationalFullScanTerminalProbeOnce(
                        cdb, requestedLength,
                        operationalGate.LivePreviewImageRows,
                        submissionIndex);
                operationalGate.RecordLiveFullScanTerminalProbeCompletion(
                    result.Status.RawStatus, requestedLength,
                    result.Status.Residue,
                    checked((int)result.Status.ActualLength), result.Data);
                log.Warning(string.Format(
                    "LIVE ASPI exact full-scan terminal probe observed and " +
                    "quarantined: row=763, status=0x{0:X2}, requested={1}, " +
                    "actual={2}, residue={3}, width={4}, selector=0x{5:X2}, " +
                    "submission={6}, CDB={7}, payload-sha256={8}. No probe " +
                    "payload bytes were copied to FlexColor; every " +
                    "successor is blocked before USB.",
                    result.Status.RawStatus, requestedLength,
                    result.Status.ActualLength, result.Status.Residue,
                    scanWidth, cdb[4],
                    operationalGate.LivePreviewImageReadSubmissions,
                    ScsiFraming.ToHex(cdb), Sha256Hex(result.Data)));
                throw new InvalidOperationException(
                    "The one-shot full-scan terminal probe was quarantined " +
                    "after USB observation.");
            }
            catch
            {
                operationalGate.FailClosed();
                DisposeDevice();
                throw;
            }
        }

        private static bool IsPredictedImageRead(byte[] cdb,
            uint requestedLength)
        {
            try
            {
                PrecisionTwoPreviewCommandManifest.ValidatePredictedImageRead(
                    cdb, requestedLength);
                return true;
            }
            catch (ProtocolException)
            {
                return false;
            }
        }

        private AspiTransportResult ExecuteOnePredictedPreviewImageRead(
            byte target, byte lun, byte[] cdb, uint requestedLength)
        {
            uint scanWidth = operationalGate.ObservePredictedPreviewImageRead(
                target, lun, cdb, requestedLength);
            EnsureConnected();
            try
            {
                ScsiCommandResult result = device.
                    ReadPrecisionTwoOperationalPreviewFirstImageOnce(cdb,
                        requestedLength);
                if (log != null)
                {
                    log.Warning(string.Format(
                        "LIVE ASPI exact first Preview image READ completed " +
                        "once: status=0x{0:X2}, requested={1}, actual={2}, " +
                        "residue={3}, width={4}, selector=0x{5:X2}, " +
                        "payload-sha256={6}. All later image and cleanup " +
                        "commands remain blocked.",
                        result.Status.RawStatus, requestedLength,
                        result.Data.Length, result.Status.Residue, scanWidth,
                        cdb[4], Sha256Hex(result.Data)));
                }
                return Convert(result);
            }
            finally
            {
                DisposeDevice();
            }
        }

        private AspiTransportResult ExecuteBoundedPreviewImageBurstRead(
            byte target, byte lun, byte[] cdb, uint requestedLength)
        {
            int maximumRows = previewImageReadLimit;
            bool longStream = maximumRows ==
                PrecisionTwoPreviewCommandManifest.AspiLivePreviewStreamRows;
            int rowIndex = livePreviewImageRowsCompleted;
            try
            {
                uint scanWidth = operationalGate.
                    BeginLivePreviewImageBurstRead(target, lun, cdb,
                        requestedLength, maximumRows);
                EnsureConnected();
                ScsiCommandResult result = device.
                    ReadPrecisionTwoOperationalPreviewImageBurstRow(cdb,
                        requestedLength, rowIndex, maximumRows);
                if (result.Status.RawStatus !=
                        (byte)AdapterStatus.Success ||
                    result.Status.Residue != 0 ||
                    result.Status.ActualLength != requestedLength ||
                    result.Data == null || result.Data.Length != requestedLength)
                {
                    throw new InvalidOperationException(
                        "A bounded Preview image row did not complete with " +
                        "status 00, full length, and zero residue; the burst " +
                        "is terminal and cannot retry.");
                }
                ++livePreviewImageRowsCompleted;
                log.Warning(string.Format(
                    longStream
                    ? "LIVE ASPI exact bounded Preview stream image READ " +
                        "completed: row={0}/{1}, status=0x{2:X2}, " +
                        "requested={3}, actual={4}, residue={5}, width={6}, " +
                        "selector=0x{7:X2}, payload-sha256={8}."
                    : "LIVE ASPI exact bounded Preview image READ completed: " +
                    "row={0}/{1}, status=0x{2:X2}, requested={3}, actual={4}, " +
                    "residue={5}, width={6}, selector=0x{7:X2}, " +
                    "payload-sha256={8}.",
                    livePreviewImageRowsCompleted, maximumRows,
                    result.Status.RawStatus, requestedLength,
                    result.Data.Length, result.Status.Residue, scanWidth,
                    cdb[4], Sha256Hex(result.Data)));
                if (livePreviewImageRowsCompleted == maximumRows)
                {
                    log.Warning(longStream
                        ? "LIVE ASPI exact bounded Preview stream completed: " +
                            "rows=256/256. Row 257 and cleanup remain " +
                            "blocked before USB."
                        : "LIVE ASPI exact bounded Preview image burst " +
                            "completed: rows=8/8. Row 9 and cleanup remain " +
                            "blocked before USB.");
                }
                return Convert(result);
            }
            catch
            {
                if (log != null)
                {
                    log.Warning(string.Format(longStream
                        ? "LIVE ASPI bounded Preview stream failed closed: " +
                            "row={0}/{1}. The row cannot retry and every " +
                            "later image, poll, or cleanup command remains " +
                            "blocked before USB."
                        : "LIVE ASPI bounded Preview image burst failed closed: " +
                            "row={0}/{1}. The row cannot retry and every later " +
                            "image or cleanup command remains blocked before USB.",
                        rowIndex + 1, maximumRows));
                }
                throw;
            }
            finally
            {
                if (livePreviewImageRowsCompleted == maximumRows ||
                    operationalGate.State ==
                        AspiOperationalSequenceState.Failed)
                {
                    DisposeDevice();
                }
            }
        }

        private AspiTransportResult
            ExecuteBoundedPreviewImageShortRetryRead(byte target, byte lun,
                byte[] cdb, uint requestedLength)
        {
            int rowIndex = operationalGate.LivePreviewImageRows;
            int submissionIndex = operationalGate.
                LivePreviewImageReadSubmissions;
            try
            {
                if (previewCancellationExecutionEnabled &&
                    livePreviewImageRowsCompleted >=
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewCancellationMaximumRows)
                {
                    throw new InvalidOperationException(
                        "Preview cancellation did not arrive by the exact " +
                        "96-row safety boundary; row 97 is blocked before " +
                        "USB.");
                }
                uint scanWidth = operationalGate.
                    BeginLivePreviewImageShortRetryRead(target, lun, cdb,
                        requestedLength);
                EnsureConnected();
                ScsiCommandResult result = device.
                    ReadPrecisionTwoOperationalPreviewImageShortRetryAttempt(
                        cdb, requestedLength, rowIndex, submissionIndex,
                        previewImageReadLimit,
                        previewMaximumReadSubmissions);
                AspiLivePreviewImageCompletion completion = operationalGate.
                    CompleteLivePreviewImageShortRetryRead(
                        result.Status.RawStatus, requestedLength,
                        result.Status.Residue,
                        checked((int)result.Status.ActualLength), result.Data);
                if (completion ==
                        AspiLivePreviewImageCompletion.RetryShortRow)
                {
                    log.Warning(string.Format(
                        "LIVE ASPI exact Preview short completion converted " +
                        "to bounded target BUSY: row={0}/{1}, " +
                        "short-retry={2}/{3}, consecutive={4}/{5}, " +
                        "submission={6}/{7}, status=0x{8:X2}, " +
                        "requested={9}, actual={10}, residue={11}, width={12}, " +
                        "selector=0x{13:X2}, payload-sha256={14}. No short " +
                        "payload bytes were copied to FlexColor.",
                        rowIndex + 1, previewImageReadLimit,
                        operationalGate.LivePreviewShortRetries,
                        previewMaximumShortRetries,
                        operationalGate.LivePreviewConsecutiveShortRetries,
                        previewMaximumConsecutiveShortRetries,
                        operationalGate.LivePreviewImageReadSubmissions,
                        previewMaximumReadSubmissions,
                        result.Status.RawStatus, requestedLength,
                        result.Data.Length, result.Status.Residue, scanWidth,
                        cdb[4], Sha256Hex(result.Data)));
                    BackoffAfterLiveImageShortCompletion();
                    return new AspiTransportResult(new byte[0],
                        (byte)AdapterStatus.Busy);
                }

                livePreviewImageRowsCompleted = operationalGate.
                    LivePreviewImageRows;
                log.Warning(string.Format(
                    "LIVE ASPI exact bounded Preview short-retry stream " +
                    "image READ completed: row={0}/{1}, status=0x{2:X2}, " +
                    "requested={3}, actual={4}, residue={5}, width={6}, " +
                    "selector=0x{7:X2}, submission={8}/{9}, " +
                    "short-retries={10}/{11}, payload-sha256={12}.",
                    livePreviewImageRowsCompleted, previewImageReadLimit,
                    result.Status.RawStatus, requestedLength,
                    result.Data.Length, result.Status.Residue, scanWidth,
                    cdb[4], operationalGate.LivePreviewImageReadSubmissions,
                    previewMaximumReadSubmissions,
                    operationalGate.LivePreviewShortRetries,
                    previewMaximumShortRetries,
                    Sha256Hex(result.Data)));
                if (livePreviewImageRowsCompleted ==
                        previewImageReadLimit)
                {
                    log.Warning(BuildPreviewStreamCompletedMessage(
                        previewCleanupExecutionEnabled,
                        previewImageReadLimit,
                        operationalGate.LivePreviewImageReadSubmissions,
                        previewMaximumReadSubmissions,
                        operationalGate.LivePreviewShortRetries,
                        previewMaximumShortRetries));
                }
                return Convert(result);
            }
            catch
            {
                if (log != null)
                {
                    log.Warning(string.Format(
                        "LIVE ASPI bounded Preview short-retry stream failed " +
                        "closed: row={0}/{1}, submissions={2}/{3}, " +
                        "short-retries={4}/{5}, consecutive={6}/{7}. Every " +
                        "later image, poll, or cleanup command remains " +
                        "blocked before USB.",
                        rowIndex + 1, previewImageReadLimit,
                        operationalGate.LivePreviewImageReadSubmissions,
                        previewMaximumReadSubmissions,
                        operationalGate.LivePreviewShortRetries,
                        previewMaximumShortRetries,
                        operationalGate.LivePreviewConsecutiveShortRetries,
                        previewMaximumConsecutiveShortRetries));
                }
                throw;
            }
            finally
            {
                if ((operationalGate.State == AspiOperationalSequenceState.
                        PredictedPreviewImageReadObserved &&
                     !previewCleanupExecutionEnabled) ||
                    operationalGate.State ==
                        AspiOperationalSequenceState.Failed)
                {
                    DisposeDevice();
                }
            }
        }

        internal static string BuildPreviewStreamCompletedMessage(
            bool cleanupExecutionEnabled, int imageReadLimit,
            int imageReadSubmissions, int maximumReadSubmissions,
            int shortRetries, int maximumShortRetries)
        {
            string successor = cleanupExecutionEnabled
                ? "Row {5} remains blocked before USB; exactly two " +
                    "hash-pinned cleanup SET WINDOW successors are now " +
                    "eligible."
                : "Row {5} and cleanup remain blocked before USB.";
            return string.Format(
                "LIVE ASPI exact bounded Preview short-retry stream " +
                "completed: rows={0}/{0}, submissions={1}/{2}, " +
                "short-retries={3}/{4}. " + successor,
                imageReadLimit, imageReadSubmissions,
                maximumReadSubmissions, shortRetries,
                maximumShortRetries, imageReadLimit + 1);
        }

        private static string Sha256Hex(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(
                    data ?? new byte[0])).Replace("-", string.Empty);
            }
        }

        private static void BackoffAfterLiveImageShortCompletion()
        {
            Thread.Sleep(PrecisionTwoPreviewCommandManifest.
                AspiLiveImageShortRetryBackoffMilliseconds);
        }

        private static long GetMonotonicMilliseconds()
        {
            return Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency;
        }

        private bool OperationalSequenceEnabled
        {
            get { return loaderDataOutEnabled || previewSequenceEnabled; }
        }

        private void RecordTargetFiveIdentity(byte[] data)
        {
            RecordTargetFiveIdentity(InquiryData.Parse(data));
        }

        private void RecordTargetFiveIdentity(InquiryData identity)
        {
            discoveryGate.RecordIdentity(identity);
            if (loaderDataOutEnabled && discoveryGate.
                    PostLoaderOperationalInitializationRequired)
            {
                discoveryGate.ArmPostLoaderOperationalInitialization();
                operationalGate.BeginPostLoaderInitialization();
                log.Info(
                    "LIVE ASPI armed post-loader initialization directly " +
                    "at ScannerReady; no second operational D8 is expected.");
            }
            log.Info(string.Format(
                "LIVE ASPI target-5 identity: {0}/{1}/{2}, type=0x{3:X2}, " +
                "classification={4}.", identity.Vendor, identity.Product,
                identity.Revision, identity.PeripheralDeviceType,
                discoveryGate.Identity));
        }

        private static AspiTransportResult Convert(ScsiCommandResult result)
        {
            return new AspiTransportResult(result.Data,
                result.Status.RawStatus);
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

        private static bool IsInquiry(byte[] cdb, uint requestedLength)
        {
            return cdb != null && cdb.Length == 6 && cdb[0] == 0x12 &&
                requestedLength != 0 && requestedLength <= 255 &&
                cdb[1] == 0 && cdb[2] == 0 && cdb[3] == 0 &&
                cdb[4] == requestedLength && cdb[5] == 0;
        }

        private static bool IsRequestSense(byte[] cdb, uint requestedLength)
        {
            return cdb != null && cdb.Length == 6 && cdb[0] == 0x03 &&
                requestedLength != 0 && requestedLength <= 255 &&
                cdb[1] == 0 && cdb[2] == 0 && cdb[3] == 0 &&
                cdb[4] == requestedLength && cdb[5] == 0;
        }

        internal static bool IsWarmOperatorPreviewInitializationRead(
            byte[] cdb, uint requestedLength)
        {
            return Matches(cdb,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb()) ||
                IsOperationalRead(cdb) ||
                IsRequestSense(cdb, requestedLength);
        }

        private static bool IsOperationalRead(byte[] cdb)
        {
            return Matches(cdb,
                    ScsiFraming.BuildPrecisionTwoFaultPixelReadBufferCdb()) ||
                Matches(cdb, ScsiFraming.
                    BuildPrecisionTwoFaultPixelDataReadBufferCdb()) ||
                Matches(cdb,
                    ScsiFraming.BuildPrecisionTwoCalibrationReadBufferCdb()) ||
                Matches(cdb,
                    ScsiFraming.BuildPrecisionTwoD8Offset55ReadBufferCdb()) ||
                Matches(cdb, ScsiFraming.
                    BuildPrecisionTwoDynamicConfigurationD8ReadBufferCdb());
        }

        private void EnsureConnected()
        {
            if (device != null)
            {
                return;
            }

            device = Usb2XchangeDevice.Find(UsbConstants.OperationalProductId,
                log, 5000, previewSetWindowSha256);
            if (device == null)
            {
                throw new InvalidOperationException(
                    "USB2Xchange operational PID 2003 was not found. " +
                    "The ASPI shim does not load adapter firmware.");
            }

            try
            {
                device.InitializeOperational();
            }
            catch
            {
                DisposeDevice();
                throw;
            }
        }

        private void DisposeDevice()
        {
            if (device != null)
            {
                device.Dispose();
                device = null;
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                DisposeDevice();
            }
        }

    }
}
