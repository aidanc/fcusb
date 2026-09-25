// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using Usb2Xchange.BrokerProtocol;
using Usb2Xchange.Protocol;
using Usb2Xchange.WinUsb;

namespace Usb2Xchange.Broker
{
    internal enum LoaderWriteBufferExperiment
    {
        Block = 0,
        ExecuteOnceAndOffline = 1,
        SimulateProtocolErrorOnce = 2,
        ExecuteFirstRecordOnceAndOffline = 3,
        ExecuteFirstTwoRecordsOnceAndOffline = 4,
        ExecuteFirstThreeRecordsOnceAndOffline = 5,
        ExecuteFirstFourRecordsOnceAndOffline = 6,
        ExecuteFirstFiveRecordsOnceAndOffline = 7,
        ExecuteFirstSixRecordsOnceAndOffline = 8,
        ExecuteFirstEightRecordsOnceAndOffline = 9,
        ExecuteFirstTenRecordsOnceAndOffline = 10,
        ExecuteFirstTwelveRecordsOnceAndOffline = 11,
        ExecuteFirstFourteenRecordsOnceAndOffline = 12,
        ExecuteAllSixteenRecordsOnceAndOffline = 13,
        ExecuteAllSixteenRecordsAndTerminalOnceAndOffline = 14
    }

    internal enum ReadBufferD8Experiment
    {
        Block = 0,
        LoaderOnce = 1,
        OperationalOnce = 2,
        OperationalD8AndScannerReadyOnce = 3,
        OperationalD8ScannerReadyAndFaultPixelsOnce = 4,
        OperationalInitializationSixCycles = 5
    }

    internal enum OperationalSetWindowExperiment
    {
        Block = 0,
        ExecuteCapturedPreviewOnceAndOffline = 1,
        ExecuteCapturedPreviewOnceThenCaptureNextBlocked = 2
    }

    internal static class OnlineBrokerSession
    {
        private const int InitializationTimeoutMilliseconds = 5000;

        internal static int Run(int durationSeconds, int maximumRequests,
            bool stopOnBlocked,
            ReadBufferD8Experiment readBufferD8Experiment,
            LoaderWriteBufferExperiment loaderWriteBufferExperiment,
            PrecisionTwoLoaderSequenceManifest loaderManifest = null,
            OperationalSetWindowExperiment setWindowExperiment =
                OperationalSetWindowExperiment.Block)
        {
            if (durationSeconds < 10 || durationSeconds > 300)
            {
                throw new ArgumentOutOfRangeException("durationSeconds",
                    "The guarded session duration must be 10 through 300 seconds.");
            }
            if (maximumRequests < 1 || maximumRequests > 256)
            {
                throw new ArgumentOutOfRangeException("maximumRequests",
                    "The guarded request limit must be 1 through 256.");
            }
            if (!Enum.IsDefined(typeof(LoaderWriteBufferExperiment),
                loaderWriteBufferExperiment))
            {
                throw new ArgumentOutOfRangeException(
                    "loaderWriteBufferExperiment");
            }
            if (!Enum.IsDefined(typeof(ReadBufferD8Experiment),
                readBufferD8Experiment))
            {
                throw new ArgumentOutOfRangeException(
                    "readBufferD8Experiment");
            }
            if (!Enum.IsDefined(typeof(OperationalSetWindowExperiment),
                setWindowExperiment))
            {
                throw new ArgumentOutOfRangeException("setWindowExperiment");
            }
            if (setWindowExperiment != OperationalSetWindowExperiment.Block &&
                (readBufferD8Experiment != ReadBufferD8Experiment.
                        OperationalInitializationSixCycles ||
                    loaderWriteBufferExperiment !=
                        LoaderWriteBufferExperiment.Block))
            {
                throw new ArgumentException(
                    "Preview SET WINDOW requires the six-cycle operational " +
                    "initialization gate and cannot be combined with a loader " +
                    "write experiment.", "setWindowExperiment");
            }
            int allowedLoaderRecordCount = GetAllowedLoaderRecordCount(
                loaderWriteBufferExperiment);
            bool operationalD8Experiment =
                IsOperationalReadOnlyExperiment(readBufferD8Experiment);
            ExperimentSafety.ValidateD8AndLoaderWriteCombination(
                loaderWriteBufferExperiment !=
                    LoaderWriteBufferExperiment.Block,
                readBufferD8Experiment == ReadBufferD8Experiment.LoaderOnce,
                operationalD8Experiment);
            if ((allowedLoaderRecordCount != 0) !=
                (loaderManifest != null))
            {
                throw new ArgumentException(
                    "The loader-record experiment and manifest must " +
                    "be supplied together.", "loaderManifest");
            }
            ScannerIdentityRequirement identityRequirement =
                ScannerIdentityRequirement.KnownPrecisionTwo;
            if (operationalD8Experiment)
            {
                identityRequirement = ScannerIdentityRequirement.Operational;
            }
            else if (readBufferD8Experiment ==
                    ReadBufferD8Experiment.LoaderOnce ||
                loaderWriteBufferExperiment !=
                    LoaderWriteBufferExperiment.Block)
            {
                identityRequirement = ScannerIdentityRequirement.Loader;
            }

            IList<StoragePortDevice> adapters =
                StoragePortDiscovery.FindUsb2XchangeAdapters();
            if (adapters.Count != 1)
            {
                throw new InvalidOperationException(string.Format(
                    "Expected exactly one USB2Xchange virtual miniport; found {0}.",
                    adapters.Count));
            }

            ulong generation = checked((ulong)DateTime.UtcNow.Ticks);
            if (generation == UInt64.MaxValue)
            {
                throw new InvalidOperationException(
                    "Could not allocate an offline cleanup generation.");
            }

            BrokerUsbLog log = new BrokerUsbLog();
            Usb2XchangeDevice device = Usb2XchangeDevice.Find(
                UsbConstants.OperationalProductId, log,
                InitializationTimeoutMilliseconds);
            if (device == null)
            {
                throw new InvalidOperationException(
                    "Exactly one operational USB2Xchange PID-2003 device is required.");
            }

            using (device)
            using (MiniportConnection control = new MiniportConnection(
                adapters[0].DevicePath))
            using (ServiceRequestConnection service =
                new ServiceRequestConnection(adapters[0].DevicePath))
            {
                device.InitializeOperational();
                WinUsbScsiTransport transport = new WinUsbScsiTransport(device);
                byte[] inquiry = ReadOnlyScsiExecutor.ProbeScanner(transport,
                    InitializationTimeoutMilliseconds, identityRequirement);
                InquiryData identity = InquiryData.Parse(inquiry);
                Console.WriteLine(
                    "Verified physical target 5, LUN 0: {0} / {1} / {2} (type {3:X2}).",
                    identity.Vendor, identity.Product, identity.Revision,
                    identity.PeripheralDeviceType);

                OfflineController offline = new OfflineController(control,
                    generation + 1);
                bool onlineAccepted = false;
                Exception failure = null;
                try
                {
                    SendOnline(control, generation, inquiry);
                    onlineAccepted = true;
                    Console.WriteLine(
                        "Miniport online at generation {0}; serving only INQUIRY, " +
                        "TEST UNIT READY, REQUEST SENSE, and synthetic single-LUN " +
                        "REPORT LUNS.", generation);
                    if (readBufferD8Experiment !=
                        ReadBufferD8Experiment.Block)
                    {
                        Console.WriteLine(
                            "Capture extension: one exact {0}-identity READ " +
                            "BUFFER D8 may execute; a second or variant remains " +
                            "blocked.",
                            operationalD8Experiment ?
                                "operational" : "loader");
                    }
                    if (IncludesScannerReady(readBufferD8Experiment))
                    {
                        Console.WriteLine(
                            "Operational extension: after exact D8 success, " +
                            "one exact two-byte ScannerReady data-in poll may " +
                            "execute; a repeat or variant remains blocked.");
                    }
                    if (IncludesFaultPixelReadBuffer(readBufferD8Experiment))
                    {
                        Console.WriteLine(
                            "Operational extension: up to {0} strictly " +
                            "ordered read-only calibration iteration(s) may " +
                            "execute. Each requires the exact 22-byte " +
                            "fault-pixel header, ScannerReady poll, derived " +
                            "58-byte table, and 1,024-byte E0 block. A " +
                            "ten-byte D8 offset-55 identifier may appear " +
                            "before the next iteration's ScannerReady; " +
                            "variants remain blocked.",
                            GetOperationalInitializationCycleLimit(
                                readBufferD8Experiment));
                    }
                    if (setWindowExperiment ==
                        OperationalSetWindowExperiment.
                            ExecuteCapturedPreviewOnceAndOffline)
                    {
                        Console.WriteLine(
                            "APPROVED STATE-CHANGING EXPERIMENT: immediately " +
                            "after a successful initialization-cycle " +
                            "continuation poll, one exact 84-byte Preview SET " +
                            "WINDOW with the pinned payload SHA-256 may execute. " +
                            "No retry or automatic sense is allowed; forced " +
                            "offline follows the attempt.");
                    }
                    if (setWindowExperiment ==
                        OperationalSetWindowExperiment.
                            ExecuteCapturedPreviewOnceThenCaptureNextBlocked)
                    {
                        Console.WriteLine(
                            "APPROVED STATE-CHANGING CONTINUATION CAPTURE: " +
                            "one exact pinned Preview SET WINDOW may execute. " +
                            "The broker will then remain online only to log " +
                            "and block the next request outside the normal " +
                            "read-only allowlist; no second state change is " +
                            "reachable.");
                    }
                    if (loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.ExecuteOnceAndOffline)
                    {
                        Console.WriteLine(
                            "STATE-CHANGING EXPERIMENT: after D8, one exact " +
                            "zero-parameter WRITE BUFFER mode 1 may execute. " +
                            "The session will force offline immediately afterward.");
                    }
                    if (loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.SimulateProtocolErrorOnce)
                    {
                        Console.WriteLine(
                            "NO-WRITE SIMULATION: the exact zero-parameter " +
                            "WRITE BUFFER mode 1 will receive the previously " +
                            "observed ProtocolError without any USB submission.");
                    }
                    if (loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.
                            ExecuteFirstRecordOnceAndOffline)
                    {
                        Console.WriteLine(
                            "APPROVED STATE-CHANGING EXPERIMENT: after D8, " +
                            "one exact 4,106-byte first loader record may " +
                            "execute. No second or terminal record is allowed; " +
                            "forced offline follows the completion.");
                    }
                    if (loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.
                            ExecuteFirstTwoRecordsOnceAndOffline)
                    {
                        Console.WriteLine(
                            "APPROVED STATE-CHANGING EXPERIMENT: after D8, " +
                            "the exact first loader record may execute, and " +
                            "the exact second record may execute only after " +
                            "the first completes successfully. No third or " +
                            "terminal record is allowed; forced offline " +
                            "follows record 2 or any failure.");
                    }
                    if (loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.
                            ExecuteFirstThreeRecordsOnceAndOffline)
                    {
                        Console.WriteLine(
                            "APPROVED STATE-CHANGING EXPERIMENT: after D8, " +
                            "the exact first three loader records may execute " +
                            "in order, each only after the previous exact " +
                            "success. No fourth or terminal record is allowed; " +
                            "forced offline follows record 3 or any failure.");
                    }
                    if (loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.
                            ExecuteFirstFourRecordsOnceAndOffline)
                    {
                        Console.WriteLine(
                            "APPROVED STATE-CHANGING EXPERIMENT: after D8, " +
                            "the exact first four loader records may execute " +
                            "in order, each only after the previous exact " +
                            "success. No fifth or terminal record is allowed; " +
                            "forced offline follows record 4 or any failure.");
                    }
                    if (loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.
                            ExecuteFirstFiveRecordsOnceAndOffline)
                    {
                        Console.WriteLine(
                            "APPROVED STATE-CHANGING EXPERIMENT: after D8, " +
                            "the exact first five loader records may execute " +
                            "in order, each only after the previous exact " +
                            "success. No sixth or terminal record is allowed; " +
                            "forced offline follows record 5 or any failure.");
                    }
                    if (loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.
                            ExecuteFirstSixRecordsOnceAndOffline)
                    {
                        Console.WriteLine(
                            "APPROVED STATE-CHANGING EXPERIMENT: after D8, " +
                            "the exact first six loader records may execute " +
                            "in order, each only after the previous exact " +
                            "success. No seventh or terminal record is allowed; " +
                            "forced offline follows record 6 or any failure.");
                    }
                    if (loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.
                            ExecuteFirstEightRecordsOnceAndOffline)
                    {
                        Console.WriteLine(
                            "APPROVED STATE-CHANGING EXPERIMENT: after D8, " +
                            "the exact first eight loader records may execute " +
                            "in order, each only after the previous exact " +
                            "success. No ninth or terminal record is allowed; " +
                            "forced offline follows record 8 or any failure.");
                    }
                    if (loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.
                            ExecuteFirstTenRecordsOnceAndOffline)
                    {
                        Console.WriteLine(
                            "APPROVED STATE-CHANGING EXPERIMENT: after D8, " +
                            "the exact first ten loader records may execute " +
                            "in order, each only after the previous exact " +
                            "success. No eleventh or terminal record is allowed; " +
                            "forced offline follows record 10 or any failure.");
                    }
                    if (loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.
                            ExecuteFirstTwelveRecordsOnceAndOffline)
                    {
                        Console.WriteLine(
                            "APPROVED STATE-CHANGING EXPERIMENT: after D8, " +
                            "the exact first twelve loader records may execute " +
                            "in order, each only after the previous exact " +
                            "success. No thirteenth or terminal record is " +
                            "allowed; forced offline follows record 12 or any " +
                            "failure.");
                    }
                    if (loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.
                            ExecuteFirstFourteenRecordsOnceAndOffline)
                    {
                        Console.WriteLine(
                            "APPROVED STATE-CHANGING EXPERIMENT: after D8, " +
                            "the exact first fourteen loader records may " +
                            "execute in order, each only after the previous " +
                            "exact success. No fifteenth or terminal record is " +
                            "allowed; forced offline follows record 14 or any " +
                            "failure.");
                    }
                    if (loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.
                            ExecuteAllSixteenRecordsOnceAndOffline)
                    {
                        Console.WriteLine(
                            "APPROVED STATE-CHANGING EXPERIMENT: after D8, " +
                            "all sixteen exact loader records may execute in " +
                            "order, each only after the previous exact success. " +
                            "The terminal record is not allowed; forced offline " +
                            "follows record 16 or any failure.");
                    }
                    if (loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.
                            ExecuteAllSixteenRecordsAndTerminalOnceAndOffline)
                    {
                        Console.WriteLine(
                            "APPROVED STATE-CHANGING TERMINAL EXPERIMENT: after " +
                            "D8, all sixteen exact loader records may execute " +
                            "in order, and the exact ten-byte terminal may " +
                            "execute once only after all sixteen exact " +
                            "successes. No retry, sense, reset, or later write " +
                            "is allowed; forced offline follows the terminal " +
                            "attempt or any failure.");
                    }

                    RunLoop(control, service, transport, offline, generation,
                        durationSeconds, maximumRequests, stopOnBlocked,
                        readBufferD8Experiment,
                        loaderWriteBufferExperiment, loaderManifest,
                        setWindowExperiment);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    if (onlineAccepted && !offline.Accepted)
                    {
                        try
                        {
                            offline.Send();
                        }
                        catch (Exception offlineException)
                        {
                            if (failure == null)
                            {
                                failure = offlineException;
                            }
                            else
                            {
                                Console.Error.WriteLine(
                                    "OFFLINE CLEANUP ERROR: {0}",
                                    offlineException.Message);
                            }
                        }
                    }
                }

                if (failure != null)
                {
                    throw new InvalidOperationException(
                        "The guarded online broker session failed.", failure);
                }
                Console.WriteLine(
                    "Guarded broker session ended with the miniport offline.");
                return 0;
            }
        }

        private static void RunLoop(MiniportConnection control,
            ServiceRequestConnection service, IReadOnlyScsiTransport transport,
            OfflineController offline, ulong generation, int durationSeconds,
            int maximumRequests, bool stopOnBlocked,
            ReadBufferD8Experiment readBufferD8Experiment,
            LoaderWriteBufferExperiment loaderWriteBufferExperiment,
            PrecisionTwoLoaderSequenceManifest loaderManifest,
            OperationalSetWindowExperiment setWindowExperiment)
        {
            Stopwatch elapsed = Stopwatch.StartNew();
            int requestCount = 0;
            int allowedLoaderRecordCount = GetAllowedLoaderRecordCount(
                loaderWriteBufferExperiment);
            bool allowLoaderTerminal = loaderWriteBufferExperiment ==
                LoaderWriteBufferExperiment.
                    ExecuteAllSixteenRecordsAndTerminalOnceAndOffline;
            PrecisionTwoLoaderSequenceValidator loaderValidator =
                allowedLoaderRecordCount == 0 ? null :
                new PrecisionTwoLoaderSequenceValidator(loaderManifest);
            CaptureCommandGate captureGate = new CaptureCommandGate(
                readBufferD8Experiment != ReadBufferD8Experiment.Block,
                loaderWriteBufferExperiment ==
                    LoaderWriteBufferExperiment.ExecuteOnceAndOffline ||
                loaderWriteBufferExperiment ==
                    LoaderWriteBufferExperiment.SimulateProtocolErrorOnce,
                allowedLoaderRecordCount,
                loaderManifest);
            OperationalReadOnlyGate operationalGate =
                new OperationalReadOnlyGate(
                    IncludesScannerReady(readBufferD8Experiment),
                    IncludesFaultPixelReadBuffer(readBufferD8Experiment),
                    GetOperationalInitializationCycleLimit(
                        readBufferD8Experiment),
                    setWindowExperiment !=
                        OperationalSetWindowExperiment.Block);
            while (!offline.Accepted)
            {
                int remaining = checked((int)Math.Max(1,
                    TimeSpan.FromSeconds(durationSeconds).TotalMilliseconds -
                    elapsed.Elapsed.TotalMilliseconds));
                byte[] requestBytes;
                try
                {
                    requestBytes = service.Receive(generation, remaining,
                        offline.Send);
                }
                catch (Win32Exception)
                {
                    if (offline.Accepted)
                    {
                        break;
                    }
                    throw;
                }
                if (offline.Accepted)
                {
                    break;
                }

                BrokerRequest request = BrokerWireProtocol.ParseRequest(
                    requestBytes);
                string dataSha256 = ComputeDataSha256(request);
                Console.WriteLine(
                    "Request {0}: CDB={1}, direction={2}, transfer={3}, " +
                    "timeout={4}ms, data={5}, data-sha256={6}, sense={7}.",
                    request.RequestId,
                    BitConverter.ToString(request.Cdb).Replace('-', ' '),
                    request.Direction, request.TransferLength,
                    request.TimeoutMilliseconds,
                    request.DataLength, dataSha256,
                    request.SenseAllocationLength);

                bool executeReadBufferD8 =
                    captureGate.TryConsumeLoaderReadBufferD8(request);
                if (executeReadBufferD8)
                {
                    Console.WriteLine(
                        "AUTHORIZED request {0}: consuming the one-shot exact " +
                        "{1}-identity READ BUFFER D8 allowance.",
                        request.RequestId,
                        IsOperationalReadOnlyExperiment(
                            readBufferD8Experiment) ?
                            "operational" : "loader");
                }
                bool executeScannerReady =
                    operationalGate.TryConsumeScannerReady(request);
                if (executeScannerReady)
                {
                    Console.WriteLine(
                        "AUTHORIZED request {0}: consuming the one-shot exact " +
                        "operational ScannerReady data-in allowance.",
                        request.RequestId);
                }
                bool executeFaultPixelReadBuffer = operationalGate.
                    TryConsumeFaultPixelReadBuffer(request);
                if (executeFaultPixelReadBuffer)
                {
                    Console.WriteLine(
                        "AUTHORIZED request {0}: consuming the one-shot exact " +
                        "operational fault-pixel READ BUFFER allowance.",
                        request.RequestId);
                }
                bool executePostFaultPixelScannerReady = operationalGate.
                    TryConsumePostFaultPixelScannerReady(request);
                if (executePostFaultPixelScannerReady)
                {
                    Console.WriteLine(
                        "AUTHORIZED request {0}: consuming the one-shot exact " +
                        "post-fault-pixel ScannerReady data-in allowance.",
                        request.RequestId);
                }
                bool executeFaultPixelDataReadBuffer = operationalGate.
                    TryConsumeFaultPixelDataReadBuffer(request);
                if (executeFaultPixelDataReadBuffer)
                {
                    Console.WriteLine(
                        "AUTHORIZED request {0}: consuming the one-shot exact " +
                        "58-byte operational fault-pixel data READ BUFFER " +
                        "allowance.", request.RequestId);
                }
                bool executeCalibrationReadBuffer = operationalGate.
                    TryConsumeCalibrationReadBuffer(request);
                if (executeCalibrationReadBuffer)
                {
                    Console.WriteLine(
                        "AUTHORIZED request {0}: consuming the one-shot exact " +
                        "1,024-byte operational E0 calibration READ BUFFER " +
                        "allowance.", request.RequestId);
                }
                bool executeD8Offset55ReadBuffer = operationalGate.
                    TryConsumeD8Offset55ReadBuffer(request);
                if (executeD8Offset55ReadBuffer)
                {
                    Console.WriteLine(
                        "AUTHORIZED request {0}: consuming the one-shot exact " +
                        "operational D8 offset-55 READ BUFFER allowance.",
                        request.RequestId);
                }
                bool executePostCalibrationScannerReady = operationalGate.
                    TryConsumePostCalibrationScannerReady(request);
                if (executePostCalibrationScannerReady)
                {
                    Console.WriteLine(
                        "AUTHORIZED request {0}: consuming the one-shot exact " +
                        "post-calibration ScannerReady data-in allowance; " +
                        "the D8 offset-55 identifier read is optional.",
                        request.RequestId);
                }
                bool executePreviewSetWindow = operationalGate.
                    TryConsumePreviewSetWindow(request);
                if (executePreviewSetWindow)
                {
                    Console.WriteLine(
                        "AUTHORIZED STATE CHANGE request {0}: consuming the " +
                        "one-shot exact 84-byte Preview SET WINDOW allowance; {1}.",
                        request.RequestId,
                        setWindowExperiment ==
                            OperationalSetWindowExperiment.
                                ExecuteCapturedPreviewOnceAndOffline ?
                            "forced offline follows this attempt" :
                            "the next non-allowlisted request will be blocked");
                }
                bool predictedPreviewImageRead = operationalGate.
                    TryCapturePredictedPreviewImageRead(request);
                if (predictedPreviewImageRead)
                {
                    Console.WriteLine(
                        "STATIC PREDICTION MATCH request {0}: post-SET-WINDOW " +
                        "one-row READ(10), scan width {1} pixels, transfer {2} " +
                        "bytes. The request remains BLOCKED from hardware.",
                        request.RequestId,
                        BrokerWireProtocol.
                            ValidatePredictedOperationalPreviewImageRead(
                                request),
                        request.TransferLength);
                }
                bool recognizeLoaderWriteBufferModeOneZero = captureGate.
                    TryConsumeLoaderWriteBufferModeOneZero(request);
                bool executeLoaderWriteBufferModeOneZero =
                    recognizeLoaderWriteBufferModeOneZero &&
                    loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.ExecuteOnceAndOffline;
                bool simulateLoaderWriteBufferProtocolError =
                    recognizeLoaderWriteBufferModeOneZero &&
                    loaderWriteBufferExperiment ==
                        LoaderWriteBufferExperiment.SimulateProtocolErrorOnce;
                bool handleLoaderWriteBufferCandidate = captureGate.
                    ShouldHandleLoaderWriteBufferRecord(request);
                bool executeLoaderWriteBufferTerminal =
                    handleLoaderWriteBufferCandidate && allowLoaderTerminal &&
                    loaderValidator.AcceptedRecordCount == 16;
                bool executeLoaderWriteBufferRecord =
                    handleLoaderWriteBufferCandidate &&
                    !executeLoaderWriteBufferTerminal;
                if (executeLoaderWriteBufferModeOneZero)
                {
                    Console.WriteLine(
                        "AUTHORIZED STATE CHANGE request {0}: consuming the " +
                        "one-shot exact zero-parameter WRITE BUFFER mode 1 " +
                        "allowance; forced offline follows this completion.",
                        request.RequestId);
                }
                if (simulateLoaderWriteBufferProtocolError)
                {
                    Console.WriteLine(
                        "SIMULATED request {0}: returning the observed " +
                        "WRITE BUFFER ProtocolError without a USB submission.",
                        request.RequestId);
                }
                if (executeLoaderWriteBufferRecord)
                {
                    Console.WriteLine(
                        "GATED STATE-CHANGE candidate request {0}: expecting " +
                        "exact loader record {1} of the approved {2}-record " +
                        "prefix.", request.RequestId,
                        loaderValidator.AcceptedRecordCount + 1,
                        allowedLoaderRecordCount);
                }
                if (executeLoaderWriteBufferTerminal)
                {
                    Console.WriteLine(
                        "GATED TERMINAL candidate request {0}: all sixteen " +
                        "records completed exactly; validating the one-shot " +
                        "ten-byte terminal before transport. Forced offline " +
                        "follows this attempt.", request.RequestId);
                }
                ExecutionOutcome outcome = ExecuteSafely(request, transport,
                    executeReadBufferD8, readBufferD8Experiment,
                    executeScannerReady,
                    executeFaultPixelReadBuffer,
                    executePostFaultPixelScannerReady,
                    executeFaultPixelDataReadBuffer,
                    executeCalibrationReadBuffer,
                    executeD8Offset55ReadBuffer,
                    executePostCalibrationScannerReady,
                    executePreviewSetWindow,
                    executeLoaderWriteBufferModeOneZero,
                    simulateLoaderWriteBufferProtocolError,
                    executeLoaderWriteBufferRecord,
                    executeLoaderWriteBufferTerminal, loaderValidator,
                    allowedLoaderRecordCount);
                if (executeReadBufferD8 &&
                    IsOperationalReadOnlyExperiment(readBufferD8Experiment))
                {
                    operationalGate.RecordOperationalD8Completion(
                        outcome.Completion);
                }
                if (executePostCalibrationScannerReady)
                {
                    operationalGate.
                        RecordPostCalibrationScannerReadyCompletion(
                            outcome.Completion);
                }
                if (executeScannerReady)
                {
                    operationalGate.RecordScannerReadyCompletion(
                        outcome.Completion);
                }
                if (executeFaultPixelReadBuffer)
                {
                    operationalGate.RecordFaultPixelReadBufferCompletion(
                        outcome.Completion);
                }
                if (executePostFaultPixelScannerReady)
                {
                    operationalGate.
                        RecordPostFaultPixelScannerReadyCompletion(
                            outcome.Completion);
                }
                if (executeFaultPixelDataReadBuffer)
                {
                    operationalGate.RecordFaultPixelDataReadBufferCompletion(
                        outcome.Completion);
                }
                if (executeCalibrationReadBuffer)
                {
                    operationalGate.RecordCalibrationReadBufferCompletion(
                        outcome.Completion);
                }
                if (executeD8Offset55ReadBuffer)
                {
                    operationalGate.RecordD8Offset55ReadBufferCompletion(
                        outcome.Completion);
                }
                if (executePreviewSetWindow)
                {
                    operationalGate.RecordPreviewSetWindowCompletion(
                        outcome.Completion);
                }
                byte[] payload = BrokerWireProtocol.SerializeCompletion(
                    request, outcome.Completion);
                MiniportResponse response = control.Send(payload);
                if (response.ReturnCode != MiniportReturnCode.Success)
                {
                    if (offline.Accepted && response.ReturnCode ==
                            MiniportReturnCode.StaleGeneration)
                    {
                        break;
                    }
                    throw new InvalidOperationException(string.Format(
                        "Miniport rejected completion {0} with {1}.",
                        request.RequestId, response.ReturnCode));
                }

                requestCount++;
                Console.WriteLine(
                    "Completed request {0}: transport={1}, adapter={2:X2}, " +
                    "SCSI={3:X2}, actual={4}, residue={5}.",
                    request.RequestId,
                    outcome.Completion.TransportStatus,
                    outcome.Completion.AdapterStatus,
                    outcome.Completion.ScsiStatus,
                    outcome.Completion.TransferLength,
                    outcome.Completion.Residue);

                bool loaderPrefixComplete = executeLoaderWriteBufferRecord &&
                    loaderValidator.AcceptedRecordCount >=
                        allowedLoaderRecordCount && !allowLoaderTerminal;
                bool forceOfflineAfterPreviewSetWindow =
                    executePreviewSetWindow && setWindowExperiment ==
                        OperationalSetWindowExperiment.
                            ExecuteCapturedPreviewOnceAndOffline;
                if (outcome.Fatal || (stopOnBlocked && outcome.Blocked) ||
                    forceOfflineAfterPreviewSetWindow ||
                    executeLoaderWriteBufferModeOneZero ||
                    executeLoaderWriteBufferTerminal ||
                    loaderPrefixComplete ||
                    requestCount >= maximumRequests ||
                    elapsed.Elapsed >= TimeSpan.FromSeconds(durationSeconds))
                {
                    offline.Send();
                }
            }
        }

        private static ExecutionOutcome ExecuteSafely(BrokerRequest request,
            IReadOnlyScsiTransport transport,
            bool executeReadBufferD8,
            ReadBufferD8Experiment readBufferD8Experiment,
            bool executeScannerReady,
            bool executeFaultPixelReadBuffer,
            bool executePostFaultPixelScannerReady,
            bool executeFaultPixelDataReadBuffer,
            bool executeCalibrationReadBuffer,
            bool executeD8Offset55ReadBuffer,
            bool executePostCalibrationScannerReady,
            bool executePreviewSetWindow,
            bool executeLoaderWriteBufferModeOneZero,
            bool simulateLoaderWriteBufferProtocolError,
            bool executeLoaderWriteBufferRecord,
            bool executeLoaderWriteBufferTerminal,
            PrecisionTwoLoaderSequenceValidator loaderValidator,
            int allowedLoaderRecordCount)
        {
            try
            {
                int experimentalActions =
                    (executeReadBufferD8 ? 1 : 0) +
                    (executeScannerReady ? 1 : 0) +
                    (executeFaultPixelReadBuffer ? 1 : 0) +
                    (executePostFaultPixelScannerReady ? 1 : 0) +
                    (executeFaultPixelDataReadBuffer ? 1 : 0) +
                    (executeCalibrationReadBuffer ? 1 : 0) +
                    (executeD8Offset55ReadBuffer ? 1 : 0) +
                    (executePostCalibrationScannerReady ? 1 : 0) +
                    (executePreviewSetWindow ? 1 : 0) +
                    (executeLoaderWriteBufferModeOneZero ? 1 : 0) +
                    (simulateLoaderWriteBufferProtocolError ? 1 : 0) +
                    (executeLoaderWriteBufferRecord ? 1 : 0) +
                    (executeLoaderWriteBufferTerminal ? 1 : 0);
                if (experimentalActions > 1)
                {
                    throw new InvalidOperationException(
                        "A request cannot consume multiple experimental actions.");
                }
                return new ExecutionOutcome(
                    executeReadBufferD8 ?
                        (IsOperationalReadOnlyExperiment(
                                readBufferD8Experiment) ?
                            ReadOnlyScsiExecutor.
                                ExecuteCapturedOperationalReadBufferD8Once(
                                    request, transport) :
                            ReadOnlyScsiExecutor.
                                ExecuteCapturedLoaderReadBufferD8Once(
                                    request, transport)) :
                    executeScannerReady ?
                        ReadOnlyScsiExecutor.
                             ExecuteCapturedOperationalScannerReadyOnce(
                                 request, transport) :
                    executeFaultPixelReadBuffer ?
                        ReadOnlyScsiExecutor.
                            ExecuteCapturedOperationalFaultPixelReadBufferOnce(
                                request, transport) :
                    executePostFaultPixelScannerReady ?
                        ReadOnlyScsiExecutor.
                            ExecuteCapturedOperationalScannerReadyOnce(
                                request, transport) :
                    executeFaultPixelDataReadBuffer ?
                        ReadOnlyScsiExecutor.
                            ExecuteCapturedOperationalFaultPixelDataReadBufferOnce(
                                request, transport) :
                    executeCalibrationReadBuffer ?
                        ReadOnlyScsiExecutor.
                            ExecuteCapturedOperationalCalibrationReadBufferOnce(
                                request, transport) :
                    executeD8Offset55ReadBuffer ?
                        ReadOnlyScsiExecutor.
                            ExecuteCapturedOperationalD8Offset55ReadBufferOnce(
                                request, transport) :
                    executePostCalibrationScannerReady ?
                        ReadOnlyScsiExecutor.
                            ExecuteCapturedOperationalScannerReadyOnce(
                                request, transport) :
                    executePreviewSetWindow ?
                        ReadOnlyScsiExecutor.
                            ExecuteCapturedOperationalPreviewSetWindowOnce(
                                request, transport) :
                    executeLoaderWriteBufferModeOneZero ?
                        ReadOnlyScsiExecutor.
                            ExecuteCapturedLoaderWriteBufferModeOneZeroOnce(
                                request, transport) :
                    simulateLoaderWriteBufferProtocolError ?
                        ReadOnlyScsiExecutor.
                            SimulateCapturedLoaderWriteBufferModeOneZeroProtocolError(
                                request) :
                    executeLoaderWriteBufferRecord ?
                        ReadOnlyScsiExecutor.
                            ExecuteCapturedLoaderWriteBufferRecordOnce(
                                request, transport, loaderValidator,
                                allowedLoaderRecordCount) :
                    executeLoaderWriteBufferTerminal ?
                        ReadOnlyScsiExecutor.
                            ExecuteCapturedLoaderWriteBufferTerminalOnce(
                                request, transport, loaderValidator) :
                        ReadOnlyScsiExecutor.Execute(request, transport), false,
                    false);
            }
            catch (BrokerProtocolException ex)
            {
                Console.Error.WriteLine("BLOCKED request {0}: {1}",
                    request.RequestId, ex.Message);
                return new ExecutionOutcome(ReadOnlyScsiExecutor.Failure(
                    request, BrokerTransportStatus.Blocked), false, true);
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLine("TIMEOUT request {0}: {1}",
                    request.RequestId, ex.Message);
                return new ExecutionOutcome(ReadOnlyScsiExecutor.Failure(
                    request, BrokerTransportStatus.Timeout), true, false);
            }
            catch (Win32Exception ex)
            {
                BrokerTransportStatus status = IsDeviceDisconnect(
                    ex.NativeErrorCode) ? BrokerTransportStatus.NoDevice :
                    BrokerTransportStatus.IoError;
                Console.Error.WriteLine("USB ERROR request {0}: {1}",
                    request.RequestId, ex.Message);
                return new ExecutionOutcome(ReadOnlyScsiExecutor.Failure(
                    request, status), true, false);
            }
            catch (ProtocolException ex)
            {
                Console.Error.WriteLine("PROTOCOL ERROR request {0}: {1}",
                    request.RequestId, ex.Message);
                return new ExecutionOutcome(ReadOnlyScsiExecutor.Failure(
                    request, BrokerTransportStatus.ProtocolError), true,
                    false);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine("BLOCKED request {0}: {1}",
                    request.RequestId, ex.Message);
                return new ExecutionOutcome(ReadOnlyScsiExecutor.Failure(
                    request, BrokerTransportStatus.Blocked), false, true);
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine("BLOCKED request {0}: {1}",
                    request.RequestId, ex.Message);
                return new ExecutionOutcome(ReadOnlyScsiExecutor.Failure(
                    request, BrokerTransportStatus.Blocked), false, true);
            }
        }

        private static bool IsDeviceDisconnect(int error)
        {
            return error == 31 || error == 433 || error == 995 ||
                error == 1167;
        }

        private static bool IsOperationalReadOnlyExperiment(
            ReadBufferD8Experiment experiment)
        {
            return experiment == ReadBufferD8Experiment.OperationalOnce ||
                experiment == ReadBufferD8Experiment.
                    OperationalD8AndScannerReadyOnce ||
                experiment == ReadBufferD8Experiment.
                    OperationalD8ScannerReadyAndFaultPixelsOnce ||
                experiment == ReadBufferD8Experiment.
                    OperationalInitializationSixCycles;
        }

        private static bool IncludesScannerReady(
            ReadBufferD8Experiment experiment)
        {
            return experiment == ReadBufferD8Experiment.
                    OperationalD8AndScannerReadyOnce ||
                experiment == ReadBufferD8Experiment.
                    OperationalD8ScannerReadyAndFaultPixelsOnce ||
                experiment == ReadBufferD8Experiment.
                    OperationalInitializationSixCycles;
        }

        private static bool IncludesFaultPixelReadBuffer(
            ReadBufferD8Experiment experiment)
        {
            return experiment == ReadBufferD8Experiment.
                    OperationalD8ScannerReadyAndFaultPixelsOnce ||
                experiment == ReadBufferD8Experiment.
                    OperationalInitializationSixCycles;
        }

        private static int GetOperationalInitializationCycleLimit(
            ReadBufferD8Experiment experiment)
        {
            return experiment == ReadBufferD8Experiment.
                OperationalInitializationSixCycles ? 6 : 1;
        }

        private static int GetAllowedLoaderRecordCount(
            LoaderWriteBufferExperiment experiment)
        {
            if (experiment == LoaderWriteBufferExperiment.
                    ExecuteFirstRecordOnceAndOffline)
            {
                return 1;
            }
            if (experiment == LoaderWriteBufferExperiment.
                    ExecuteFirstTwoRecordsOnceAndOffline)
            {
                return 2;
            }
            if (experiment == LoaderWriteBufferExperiment.
                    ExecuteFirstThreeRecordsOnceAndOffline)
            {
                return 3;
            }
            if (experiment == LoaderWriteBufferExperiment.
                    ExecuteFirstFourRecordsOnceAndOffline)
            {
                return 4;
            }
            if (experiment == LoaderWriteBufferExperiment.
                    ExecuteFirstFiveRecordsOnceAndOffline)
            {
                return 5;
            }
            if (experiment == LoaderWriteBufferExperiment.
                    ExecuteFirstSixRecordsOnceAndOffline)
            {
                return 6;
            }
            if (experiment == LoaderWriteBufferExperiment.
                    ExecuteFirstEightRecordsOnceAndOffline)
            {
                return 8;
            }
            if (experiment == LoaderWriteBufferExperiment.
                    ExecuteFirstTenRecordsOnceAndOffline)
            {
                return 10;
            }
            if (experiment == LoaderWriteBufferExperiment.
                    ExecuteFirstTwelveRecordsOnceAndOffline)
            {
                return 12;
            }
            if (experiment == LoaderWriteBufferExperiment.
                    ExecuteFirstFourteenRecordsOnceAndOffline)
            {
                return 14;
            }
            if (experiment == LoaderWriteBufferExperiment.
                    ExecuteAllSixteenRecordsOnceAndOffline ||
                experiment == LoaderWriteBufferExperiment.
                    ExecuteAllSixteenRecordsAndTerminalOnceAndOffline)
            {
                return 16;
            }
            return 0;
        }

        private static string ComputeDataSha256(BrokerRequest request)
        {
            if (request.DataLength == 0)
            {
                return "none";
            }
            byte[] data = request.Data;
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(data)).
                    Replace("-", string.Empty);
            }
        }

        private static void SendOnline(MiniportConnection connection,
            ulong generation, byte[] inquiry)
        {
            BrokerAdapterState state = new BrokerAdapterState(generation,
                BrokerAdapterStateKind.Online,
                ScannerIdentity.PhysicalTargetId,
                ScannerIdentity.PhysicalLun, 0x06, inquiry);
            MiniportResponse response = connection.Send(
                BrokerWireProtocol.SerializeAdapterState(state));
            if (response.ReturnCode != MiniportReturnCode.Success)
            {
                throw new InvalidOperationException(string.Format(
                    "Miniport rejected online generation {0} with {1}.",
                    generation, response.ReturnCode));
            }
        }

        private sealed class ExecutionOutcome
        {
            internal ExecutionOutcome(BrokerCompletion completion, bool fatal,
                bool blocked)
            {
                Completion = completion;
                Fatal = fatal;
                Blocked = blocked;
            }

            internal BrokerCompletion Completion { get; private set; }
            internal bool Fatal { get; private set; }
            internal bool Blocked { get; private set; }
        }

        private sealed class OfflineController
        {
            private readonly MiniportConnection connection;
            private ulong nextGeneration;

            internal OfflineController(MiniportConnection connection,
                ulong generation)
            {
                this.connection = connection;
                nextGeneration = generation;
            }

            internal int AttemptCount { get; private set; }
            internal bool Accepted { get; private set; }

            internal void Send()
            {
                if (Accepted)
                {
                    return;
                }

                for (int attempt = 0; attempt < 2; attempt++)
                {
                    ulong generation = nextGeneration;
                    nextGeneration = generation == UInt64.MaxValue ?
                        generation : generation + 1;
                    AttemptCount++;
                    BrokerAdapterState state = new BrokerAdapterState(
                        generation, BrokerAdapterStateKind.Offline,
                        0, 0, 0, new byte[0]);
                    MiniportResponse response = connection.Send(
                        BrokerWireProtocol.SerializeAdapterState(state));
                    if (response.ReturnCode == MiniportReturnCode.Success)
                    {
                        Accepted = true;
                        Console.WriteLine(
                            "Miniport accepted offline generation {0}.",
                            generation);
                        return;
                    }
                    if (response.ReturnCode !=
                            MiniportReturnCode.StaleGeneration ||
                        generation == UInt64.MaxValue)
                    {
                        throw new InvalidOperationException(string.Format(
                            "Miniport rejected offline generation {0} with {1}.",
                            generation, response.ReturnCode));
                    }

                    ulong currentTicks = checked((ulong)DateTime.UtcNow.Ticks);
                    if (currentTicks > nextGeneration)
                    {
                        nextGeneration = currentTicks;
                    }
                }
                throw new InvalidOperationException(
                    "The miniport rejected both guarded offline generations.");
            }
        }
    }
}
