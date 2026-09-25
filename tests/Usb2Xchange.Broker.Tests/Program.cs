// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Usb2Xchange.Broker;
using Usb2Xchange.BrokerProtocol;
using Usb2Xchange.Protocol;

namespace Usb2Xchange.Broker.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                TestDiscoveryFilter();
                TestScannerIdentity();
                TestExperimentSeparation();
                TestSuccessfulInquiry();
                TestCheckConditionSense();
                TestWrongPhysicalTargetBlocked();
                TestVendorAndDataOutBlocked();
                TestFullLoaderDataOutBlocked();
                TestSyntheticReportLuns();
                TestExactLoaderReadBufferD8();
                TestLoaderReadBufferD8OneShotGate();
                TestExactOperationalScannerReady();
                TestExactOperationalFaultPixelReadBuffer();
                TestExactOperationalFaultPixelDataReadBuffer();
                TestExactOperationalCalibrationReadBuffer();
                TestExactOperationalD8Offset55ReadBuffer();
                TestOperationalReadOnlyGate();
                TestExactOperationalPreviewSetWindow();
                TestOperationalPreviewSetWindowGate();
                TestExactLoaderWriteBufferModeOneZero();
                TestSimulatedLoaderWriteBufferProtocolError();
                TestLoaderWriteBufferModeOneZeroOneShotGate();
                TestExactLoaderFirstRecordOneShot();
                TestExactLoaderFirstTwoRecordPrefix();
                TestExactLoaderFirstThreeRecordPrefix();
                TestExactLoaderFirstFourRecordPrefix();
                TestExactLoaderFirstFiveRecordPrefix();
                TestExactLoaderFirstSixRecordPrefix();
                TestExactLoaderFirstEightRecordPrefix();
                TestExactLoaderFirstTenRecordPrefix();
                TestExactLoaderFirstTwelveRecordPrefix();
                TestExactLoaderFirstFourteenRecordPrefix();
                TestExactLoaderAllSixteenRecordPrefix();
                TestExactLoaderCompleteSequence();
                Console.WriteLine("PASS guarded read-only broker integration helpers");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: {0}", ex.Message);
                return 1;
            }
        }

        private static void TestDiscoveryFilter()
        {
            string[] expectedIds = new string[] {
                "ROOT\\USB2XCHANGEVMINIPORT"
            };
            Require(StoragePortDiscovery.IsUsb2XchangeDevice(
                "ROOT\\SCSIADAPTER\\0000", "usb2xchange-vminiport",
                expectedIds), "exact StoragePort device was not accepted");
            Require(StoragePortDiscovery.IsUsb2XchangeDevice(
                "root\\scsiadapter\\0001", "USB2XCHANGE-VMINIPORT",
                expectedIds), "case-insensitive StoragePort device was rejected");
            Require(!StoragePortDiscovery.IsUsb2XchangeDevice(
                "ROOT\\SCSIADAPTER\\0000", "spaceport", expectedIds),
                "wrong service was accepted");
            Require(!StoragePortDiscovery.IsUsb2XchangeDevice(
                "ROOT\\SCSIADAPTER\\0000", "usb2xchange-vminiport",
                new string[] { "ROOT\\OTHER" }),
                "wrong hardware ID was accepted");
            Require(!StoragePortDiscovery.IsUsb2XchangeDevice(
                "PCI\\VEN_1234&DEV_5678\\0", "usb2xchange-vminiport",
                expectedIds), "non-root instance was accepted");
            Require(!StoragePortDiscovery.IsUsb2XchangeDevice(
                null, "usb2xchange-vminiport", expectedIds),
                "null instance was accepted");
        }

        private static void TestScannerIdentity()
        {
            byte[] inquiry = KnownInquiry();
            byte[] accepted = ScannerIdentity.RequireKnownPrecisionTwo(inquiry);
            Require(accepted.Length == 36 && !object.ReferenceEquals(
                accepted, inquiry), "scanner identity was not cloned");
            ScannerIdentity.RequireKnownPrecisionTwoLoader(inquiry);

            byte[] operational = KnownInquiry();
            PutAscii(operational, 16, 16, "FlexTight II");
            PutAscii(operational, 32, 4, "M333");
            accepted = ScannerIdentity.RequireKnownPrecisionTwo(operational);
            Require(accepted.Length == 36 && !object.ReferenceEquals(
                accepted, operational),
                "operational scanner identity was not cloned");
            ScannerIdentity.RequireKnownPrecisionTwoOperational(operational);
            Expect<ProtocolException>(delegate
            {
                ScannerIdentity.RequireKnownPrecisionTwoLoader(operational);
            }, "operational identity was accepted for a loader experiment");
            Expect<ProtocolException>(delegate
            {
                ScannerIdentity.RequireKnownPrecisionTwoOperational(inquiry);
            }, "loader identity was accepted for an operational experiment");

            byte[] wrong = (byte[])inquiry.Clone();
            PutAscii(wrong, 16, 16, "Another Scanner");
            Expect<ProtocolException>(delegate
            {
                ScannerIdentity.RequireKnownPrecisionTwo(wrong);
            }, "unexpected scanner identity was accepted");
        }

        private static void TestExperimentSeparation()
        {
            Expect<ArgumentException>(delegate
            {
                ExperimentSafety.ValidateD8AndLoaderWriteCombination(
                    true, false, true);
            }, "operational D8 gate unlocked a loader write experiment");
            Expect<ArgumentException>(delegate
            {
                ExperimentSafety.ValidateD8AndLoaderWriteCombination(
                    true, false, false);
            }, "loader write experiment ran without its loader D8 gate");
            Expect<ArgumentException>(delegate
            {
                ExperimentSafety.ValidateD8AndLoaderWriteCombination(
                    false, true, true);
            }, "loader and operational D8 gates were enabled together");
            ExperimentSafety.ValidateD8AndLoaderWriteCombination(
                true, true, false);
            ExperimentSafety.ValidateD8AndLoaderWriteCombination(
                false, false, true);
        }

        private static void TestSuccessfulInquiry()
        {
            byte[] inquiry = KnownInquiry();
            FakeTransport transport = new FakeTransport(
                new RawScsiResult(0x00, 36, 0, inquiry));
            BrokerRequest request = InquiryRequest(5);
            BrokerCompletion completion = ReadOnlyScsiExecutor.Execute(
                request, transport);
            Require(completion.TransportStatus == BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 36 && completion.Residue == 0 &&
                completion.Data.Length == 36 && transport.CallCount == 1,
                "successful INQUIRY completion was mapped incorrectly");
        }

        private static void TestCheckConditionSense()
        {
            byte[] sense = new byte[18];
            sense[0] = 0x70;
            sense[2] = 0x02;
            FakeTransport transport = new FakeTransport(
                new RawScsiResult(0x02, 0, 0, new byte[0]),
                new RawScsiResult(0x00, 18, 0, sense));
            BrokerRequest request = new BrokerRequest(2, 3, 0, 0, 0, 5, 0,
                BrokerDirection.None, 0, 5000, 18,
                new byte[] { 0, 0, 0, 0, 0, 0 });
            BrokerCompletion completion = ReadOnlyScsiExecutor.Execute(
                request, transport);
            Require(completion.AdapterStatus == 0x02 &&
                completion.ScsiStatus == 0x02 &&
                completion.Sense.Length == 18 && transport.CallCount == 2,
                "CHECK CONDITION did not return bounded REQUEST SENSE data");
        }

        private static void TestWrongPhysicalTargetBlocked()
        {
            FakeTransport transport = new FakeTransport(
                new RawScsiResult(0x00, 36, 0, KnownInquiry()));
            BrokerRequest request = InquiryRequest(4);
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.Execute(request, transport);
            }, "unverified physical target was accepted");
            Require(transport.CallCount == 0,
                "blocked request reached the transport");
        }

        private static void TestVendorAndDataOutBlocked()
        {
            FakeTransport transport = new FakeTransport();
            BrokerRequest vendor = new BrokerRequest(3, 3, 0, 0, 0, 5, 0,
                BrokerDirection.None, 0, 5000, 18,
                new byte[] { 0xE1, 0, 0, 0, 0, 0 });
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.Execute(vendor, transport);
            }, "vendor CDB was accepted");

            BrokerRequest dataOut = new BrokerRequest(4, 3, 0, 0, 0, 5, 0,
                BrokerDirection.Out, 1, 5000, 18,
                new byte[] { 0x0A, 0, 0, 0, 1, 0 },
                new byte[] { 0xA5 });
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.Execute(dataOut, transport);
            }, "data-out CDB was accepted");
            Require(transport.CallCount == 0,
                "blocked request reached the hardware transport");
        }

        private static void TestFullLoaderDataOutBlocked()
        {
            byte[] data = new byte[0x100A];
            data[0] = 0x01;
            data[1] = 0x30;
            BrokerRequest request = new BrokerRequest(5, 3,
                0, 0, 0, 5, 0, BrokerDirection.Out, 0x100A, 60000, 32,
                new byte[] {
                    0x3B, 0x01, 0, 0, 0, 0, 0, 0x10, 0x0A, 0
                }, data);
            FakeTransport transport = new FakeTransport();
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.Execute(request, transport);
            }, "full loader data-out request was accepted");
            Require(transport.CallCount == 0,
                "full loader data-out request reached the hardware transport");
        }

        private static void TestSyntheticReportLuns()
        {
            FakeTransport transport = new FakeTransport();
            BrokerRequest request = new BrokerRequest(5, 3, 0, 0, 0, 5, 0,
                BrokerDirection.In, 16, 5000, 18,
                new byte[] {
                    0xA0, 0, 0, 0, 0, 0, 0, 0, 0, 16, 0, 0
                });
            BrokerCompletion completion = ReadOnlyScsiExecutor.Execute(
                request, transport);
            Require(completion.TransportStatus == BrokerTransportStatus.Success &&
                completion.TransferLength == 16 && completion.Residue == 0 &&
                completion.Data.Length == 16 && completion.Data[3] == 8,
                "synthetic REPORT LUNS response is invalid");
            for (int index = 0; index < completion.Data.Length; index++)
            {
                if (index != 3)
                {
                    Require(completion.Data[index] == 0,
                        "synthetic REPORT LUNS exposed a nonzero extra field");
                }
            }
            Require(transport.CallCount == 0,
                "synthetic REPORT LUNS reached the hardware transport");
        }

        private static void TestExactLoaderReadBufferD8()
        {
            byte[] data = new byte[66];
            FakeTransport transport = new FakeTransport(
                new RawScsiResult(0x00, 66, 0, data));
            BrokerRequest request = LoaderReadBufferD8Request(6);

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.Execute(request, transport);
            }, "normal read-only executor accepted READ BUFFER D8");
            Require(transport.CallCount == 0,
                "normal executor sent READ BUFFER D8 to hardware");

            BrokerCompletion completion =
                ReadOnlyScsiExecutor.ExecuteCapturedLoaderReadBufferD8Once(
                    request, transport);
            Require(completion.TransportStatus == BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 66 && completion.Residue == 0 &&
                completion.Data.Length == 66 && transport.CallCount == 1 &&
                transport.LoaderReadBufferD8CallCount == 1,
                "exact READ BUFFER D8 completion was mapped incorrectly");

            FakeTransport operationalTransport = new FakeTransport(
                new RawScsiResult(0x00, 66, 0, data));
            completion = ReadOnlyScsiExecutor.
                ExecuteCapturedOperationalReadBufferD8Once(request,
                    operationalTransport);
            Require(completion.TransportStatus ==
                    BrokerTransportStatus.Success &&
                completion.TransferLength == 66 && completion.Residue == 0 &&
                operationalTransport.CallCount == 1 &&
                operationalTransport.OperationalReadBufferD8CallCount == 1 &&
                operationalTransport.LoaderReadBufferD8CallCount == 0,
                "operational READ BUFFER D8 used the wrong transport gate");

            byte[] wrongCdb = ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb();
            wrongCdb[8] = 0x41;
            BrokerRequest wrong = new BrokerRequest(7, 3, 0, 0, 0, 5, 0,
                BrokerDirection.In, 66, 60000, 32, wrongCdb);
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.ExecuteCapturedLoaderReadBufferD8Once(
                    wrong, transport);
            }, "variant READ BUFFER D8 was accepted");

            ExpectExactLoaderReadBufferD8Blocked(new BrokerRequest(
                9, 3, 0, 0, 0, 4, 0, BrokerDirection.In, 66, 60000, 32,
                ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb()),
                transport, "wrong physical target was accepted");
            ExpectExactLoaderReadBufferD8Blocked(new BrokerRequest(
                10, 3, 0, 0, 0, 5, 0, BrokerDirection.Out, 66, 60000, 32,
                ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb()),
                transport, "data-out direction was accepted");
            ExpectExactLoaderReadBufferD8Blocked(new BrokerRequest(
                11, 3, 0, 0, 0, 5, 0, BrokerDirection.In, 65, 60000, 32,
                ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb()),
                transport, "wrong transfer length was accepted");
            ExpectExactLoaderReadBufferD8Blocked(new BrokerRequest(
                12, 3, 0, 0, 0, 5, 0, BrokerDirection.In, 66, 59999, 32,
                ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb()),
                transport, "wrong timeout was accepted");
            ExpectExactLoaderReadBufferD8Blocked(new BrokerRequest(
                13, 3, 0, 0, 0, 5, 0, BrokerDirection.In, 66, 60000, 31,
                ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb()),
                transport, "wrong sense length was accepted");
            Require(transport.CallCount == 1,
                "variant READ BUFFER D8 reached hardware");
        }

        private static void ExpectExactLoaderReadBufferD8Blocked(
            BrokerRequest request, FakeTransport transport, string message)
        {
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.ExecuteCapturedLoaderReadBufferD8Once(
                    request, transport);
            }, message);
        }

        private static void TestLoaderReadBufferD8OneShotGate()
        {
            BrokerRequest request = LoaderReadBufferD8Request(8);
            CaptureCommandGate disabled = new CaptureCommandGate(false, false);
            Require(!disabled.TryConsumeLoaderReadBufferD8(request) &&
                !disabled.LoaderReadBufferD8Consumed,
                "disabled D8 gate authorized a request");

            CaptureCommandGate enabled = new CaptureCommandGate(true, false);
            Require(!enabled.TryConsumeLoaderReadBufferD8(InquiryRequest(5)) &&
                !enabled.LoaderReadBufferD8Consumed,
                "unrelated request consumed the D8 allowance");
            Require(enabled.TryConsumeLoaderReadBufferD8(request) &&
                enabled.LoaderReadBufferD8Consumed,
                "exact request did not consume the D8 allowance");
            Require(!enabled.TryConsumeLoaderReadBufferD8(request),
                "a second D8 request was authorized");
        }

        private static void TestExactOperationalScannerReady()
        {
            byte[] data = new byte[] { 8, 0 };
            FakeTransport transport = new FakeTransport(
                new RawScsiResult(0x00, 2, 0, data));
            BrokerRequest request = ScannerReadyRequest(14);

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.Execute(request, transport);
            }, "normal read-only executor accepted ScannerReady");
            Require(transport.CallCount == 0,
                "normal executor sent ScannerReady to hardware");

            BrokerCompletion completion = ReadOnlyScsiExecutor.
                ExecuteCapturedOperationalScannerReadyOnce(request,
                    transport);
            Require(completion.TransportStatus ==
                    BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 2 && completion.Residue == 0 &&
                completion.Data.Length == 2 && transport.CallCount == 1 &&
                transport.OperationalScannerReadyCallCount == 1,
                "exact ScannerReady completion was mapped incorrectly");

            byte[] wrongCdb = ScsiFraming.BuildPrecisionTwoScannerReadyCdb();
            wrongCdb[4] = 1;
            BrokerRequest wrong = new BrokerRequest(15, 3, 0, 0, 0, 5, 0,
                BrokerDirection.In, 2, 60000, 32, wrongCdb);
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedOperationalScannerReadyOnce(wrong,
                        transport);
            }, "variant ScannerReady was accepted");
            Require(transport.CallCount == 1,
                "variant ScannerReady reached hardware");
        }

        private static void TestOperationalReadOnlyGate()
        {
            BrokerRequest scannerReady = ScannerReadyRequest(16);
            BrokerRequest faultPixels = FaultPixelReadBufferRequest(17);
            BrokerRequest faultPixelData = FaultPixelDataReadBufferRequest(18);
            BrokerRequest calibration = CalibrationReadBufferRequest(19);
            BrokerRequest d8Offset55 = D8Offset55ReadBufferRequest(20);
            OperationalReadOnlyGate disabled =
                new OperationalReadOnlyGate(false);
            disabled.RecordOperationalD8Completion(new BrokerCompletion(
                1, 3, BrokerTransportStatus.Success, 0, 0, 66, 0,
                new byte[0], new byte[66]));
            Require(!disabled.TryConsumeScannerReady(scannerReady),
                "disabled ScannerReady gate authorized a request");

            OperationalReadOnlyGate gate =
                new OperationalReadOnlyGate(true, true, 6);
            Require(!gate.TryConsumeScannerReady(scannerReady),
                "ScannerReady ran before operational D8 success");
            gate.RecordOperationalD8Completion(new BrokerCompletion(
                1, 3, BrokerTransportStatus.Success, 0, 0, 65, 1,
                new byte[0], new byte[65]));
            Require(!gate.OperationalD8Succeeded &&
                !gate.TryConsumeScannerReady(scannerReady),
                "short operational D8 unlocked ScannerReady");
            gate.RecordOperationalD8Completion(new BrokerCompletion(
                1, 3, BrokerTransportStatus.Success, 0, 0, 66, 0,
                new byte[0], new byte[66]));
            Require(gate.OperationalD8Succeeded &&
                gate.TryConsumeScannerReady(scannerReady) &&
                gate.ScannerReadyConsumed,
                "exact operational D8 did not unlock ScannerReady once");
            Require(!gate.TryConsumeScannerReady(scannerReady),
                "ScannerReady gate accepted a repeated request");
            Require(!gate.TryConsumeFaultPixelReadBuffer(faultPixels),
                "fault-pixel read ran before ScannerReady success");
            gate.RecordScannerReadyCompletion(new BrokerCompletion(
                1, 3, BrokerTransportStatus.Success, 0, 0, 1, 1,
                new byte[0], new byte[1]));
            Require(!gate.ScannerReadySucceeded &&
                !gate.TryConsumeFaultPixelReadBuffer(faultPixels),
                "short ScannerReady unlocked the fault-pixel read");
            gate.RecordScannerReadyCompletion(new BrokerCompletion(
                1, 3, BrokerTransportStatus.Success, 0, 0, 2, 0,
                new byte[0], new byte[2]));
            Require(gate.ScannerReadySucceeded &&
                gate.TryConsumeFaultPixelReadBuffer(faultPixels) &&
                gate.FaultPixelReadBufferConsumed,
                "exact ScannerReady did not unlock the fault-pixel read once");
            Require(!gate.TryConsumeFaultPixelReadBuffer(faultPixels),
                "fault-pixel gate accepted a repeated request");
            Require(!gate.TryConsumePostFaultPixelScannerReady(scannerReady),
                "post-E0 ScannerReady ran before E0 success");
            gate.RecordFaultPixelReadBufferCompletion(new BrokerCompletion(
                1, 3, BrokerTransportStatus.Success, 0, 0, 21, 1,
                new byte[0], new byte[21]));
            Require(!gate.FaultPixelReadBufferSucceeded &&
                !gate.TryConsumePostFaultPixelScannerReady(scannerReady),
                "short E0 read unlocked post-E0 ScannerReady");
            gate.RecordFaultPixelReadBufferCompletion(new BrokerCompletion(
                1, 3, BrokerTransportStatus.Success, 0, 0, 22, 0,
                new byte[0], KnownFaultPixelHeader()));
            Require(gate.FaultPixelReadBufferSucceeded &&
                gate.TryConsumePostFaultPixelScannerReady(scannerReady) &&
                gate.PostFaultPixelScannerReadyConsumed,
                "exact E0 read did not unlock post-E0 ScannerReady once");
            Require(!gate.TryConsumePostFaultPixelScannerReady(scannerReady),
                "post-E0 ScannerReady gate accepted a repeated request");
            Require(!gate.TryConsumeFaultPixelDataReadBuffer(faultPixelData),
                "fault-pixel data read ran before post-E0 ScannerReady success");
            gate.RecordPostFaultPixelScannerReadyCompletion(
                new BrokerCompletion(1, 3, BrokerTransportStatus.Success,
                    0, 0, 1, 1, new byte[0], new byte[1]));
            Require(!gate.PostFaultPixelScannerReadySucceeded &&
                !gate.TryConsumeFaultPixelDataReadBuffer(faultPixelData),
                "short post-E0 ScannerReady unlocked fault-pixel data");
            gate.RecordPostFaultPixelScannerReadyCompletion(
                new BrokerCompletion(1, 3, BrokerTransportStatus.Success,
                    0, 0, 2, 0, new byte[0], new byte[2]));
            Require(gate.PostFaultPixelScannerReadySucceeded &&
                gate.TryConsumeFaultPixelDataReadBuffer(faultPixelData) &&
                gate.FaultPixelDataReadBufferConsumed,
                "post-E0 ScannerReady did not unlock fault-pixel data once");
            Require(!gate.TryConsumeFaultPixelDataReadBuffer(faultPixelData),
                "fault-pixel data gate accepted a repeated request");
            Require(!gate.TryConsumeCalibrationReadBuffer(calibration),
                "calibration read ran before fault-pixel data success");
            gate.RecordFaultPixelDataReadBufferCompletion(
                new BrokerCompletion(1, 3, BrokerTransportStatus.Success,
                    0, 0, 57, 1, new byte[0], new byte[57]));
            Require(!gate.FaultPixelDataReadBufferSucceeded &&
                !gate.TryConsumeCalibrationReadBuffer(calibration),
                "short fault-pixel data unlocked calibration read");
            gate.RecordFaultPixelDataReadBufferCompletion(
                new BrokerCompletion(1, 3, BrokerTransportStatus.Success,
                    0, 0, 58, 0, new byte[0], new byte[58]));
            Require(gate.FaultPixelDataReadBufferSucceeded &&
                gate.TryConsumeCalibrationReadBuffer(calibration) &&
                gate.CalibrationReadBufferConsumed,
                "fault-pixel data did not unlock calibration read once");
            Require(!gate.TryConsumeCalibrationReadBuffer(calibration),
                "calibration gate accepted a repeated request");
            Require(!gate.TryConsumeD8Offset55ReadBuffer(d8Offset55),
                "D8 offset-55 read ran before calibration success");
            gate.RecordCalibrationReadBufferCompletion(
                new BrokerCompletion(1, 3, BrokerTransportStatus.Success,
                    0, 0, 1023, 1, new byte[0], new byte[1023]));
            Require(!gate.CalibrationReadBufferSucceeded &&
                !gate.TryConsumeD8Offset55ReadBuffer(d8Offset55),
                "short calibration read unlocked D8 offset-55 read");
            gate.RecordCalibrationReadBufferCompletion(
                new BrokerCompletion(1, 3, BrokerTransportStatus.Success,
                    0, 0, 1024, 0, new byte[0], new byte[1024]));
            Require(gate.CalibrationReadBufferSucceeded &&
                gate.TryConsumeD8Offset55ReadBuffer(d8Offset55) &&
                gate.D8Offset55ReadBufferConsumed,
                "calibration read did not unlock D8 offset-55 read once");
            Require(!gate.TryConsumeD8Offset55ReadBuffer(d8Offset55),
                "D8 offset-55 gate accepted a repeated request");
            Require(!gate.TryConsumePostCalibrationScannerReady(scannerReady),
                "post-D8 ScannerReady ran before D8 offset-55 success");
            gate.RecordD8Offset55ReadBufferCompletion(
                new BrokerCompletion(1, 3, BrokerTransportStatus.Success,
                    0, 0, 9, 1, new byte[0], new byte[9]));
            Require(!gate.D8Offset55ReadBufferSucceeded &&
                !gate.TryConsumePostCalibrationScannerReady(scannerReady),
                "short D8 offset-55 read unlocked ScannerReady");
            gate.RecordD8Offset55ReadBufferCompletion(
                new BrokerCompletion(1, 3, BrokerTransportStatus.Success,
                    0, 0, 10, 0, new byte[0], new byte[10]));
            Require(gate.D8Offset55ReadBufferSucceeded &&
                gate.TryConsumePostCalibrationScannerReady(scannerReady) &&
                gate.PostCalibrationScannerReadyConsumed,
                "D8 offset-55 read did not unlock ScannerReady once");
            Require(!gate.TryConsumePostCalibrationScannerReady(scannerReady),
                "post-D8 ScannerReady gate accepted a repeated request");
            gate.RecordPostCalibrationScannerReadyCompletion(
                new BrokerCompletion(1, 3, BrokerTransportStatus.Success,
                    0, 0, 2, 0, new byte[0], new byte[2]));
            Require(gate.CompletedInitializationCycles == 1 &&
                gate.TryConsumeFaultPixelReadBuffer(faultPixels),
                "successful cycle did not unlock the next exact header");

            gate.RecordFaultPixelReadBufferCompletion(new BrokerCompletion(
                1, 3, BrokerTransportStatus.Success, 0, 0, 22, 0,
                new byte[0], KnownFaultPixelHeader()));
            Require(gate.TryConsumePostFaultPixelScannerReady(scannerReady),
                "second-cycle header did not unlock ScannerReady");
            gate.RecordPostFaultPixelScannerReadyCompletion(
                new BrokerCompletion(1, 3, BrokerTransportStatus.Success,
                    0, 0, 2, 0, new byte[0], new byte[2]));
            Require(gate.TryConsumeFaultPixelDataReadBuffer(faultPixelData),
                "second-cycle ScannerReady did not unlock fault-pixel data");
            gate.RecordFaultPixelDataReadBufferCompletion(
                new BrokerCompletion(1, 3, BrokerTransportStatus.Success,
                    0, 0, 58, 0, new byte[0], new byte[58]));
            Require(gate.TryConsumeCalibrationReadBuffer(calibration),
                "second-cycle fault-pixel data did not unlock calibration");
            gate.RecordCalibrationReadBufferCompletion(
                new BrokerCompletion(1, 3, BrokerTransportStatus.Success,
                    0, 0, 1024, 0, new byte[0], new byte[1024]));
            Require(gate.TryConsumePostCalibrationScannerReady(scannerReady) &&
                !gate.D8Offset55ReadBufferConsumed,
                "calibration did not permit the observed direct ScannerReady branch");
            Require(!gate.TryConsumeD8Offset55ReadBuffer(d8Offset55),
                "identifier read remained reachable after direct ScannerReady consumption");
            gate.RecordPostCalibrationScannerReadyCompletion(
                new BrokerCompletion(1, 3, BrokerTransportStatus.Success,
                    0, 0, 2, 0, new byte[0], new byte[2]));
            Require(gate.CompletedInitializationCycles == 2 &&
                gate.TryConsumeFaultPixelReadBuffer(faultPixels),
                "direct ScannerReady branch did not complete and reset the cycle");

            Expect<ArgumentOutOfRangeException>(delegate
            {
                new OperationalReadOnlyGate(true, true, 7);
            }, "gate accepted more than six initialization cycles");
        }

        private static void TestExactOperationalPreviewSetWindow()
        {
            byte[] data = PreviewSetWindowData();
            string expectedHash = Sha256Hex(data);
            BrokerRequest request = PreviewSetWindowRequest(21, data);
            FakeTransport transport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00, 84, 0));

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.Execute(request, transport);
            }, "normal read-only executor accepted Preview SET WINDOW");
            Require(transport.CallCount == 0,
                "normal executor sent Preview SET WINDOW to hardware");

            BrokerCompletion completion = ReadOnlyScsiExecutor.
                ExecuteCapturedOperationalPreviewSetWindowOnce(request,
                    transport, expectedHash);
            Require(completion.TransportStatus ==
                    BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 84 && completion.Residue == 0 &&
                completion.Data.Length == 0 && transport.CallCount == 1 &&
                transport.OperationalPreviewSetWindowCallCount == 1,
                "exact Preview SET WINDOW completion was mapped incorrectly");

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedOperationalPreviewSetWindowOnce(request,
                        transport);
            }, "non-captured Preview SET WINDOW payload hash was accepted");
            Require(transport.CallCount == 1,
                "wrong-hash Preview SET WINDOW reached hardware");
        }

        private static void TestOperationalPreviewSetWindowGate()
        {
            byte[] data = PreviewSetWindowData();
            string expectedHash = Sha256Hex(data);
            BrokerRequest setWindow = PreviewSetWindowRequest(22, data);
            OperationalReadOnlyGate gate = new OperationalReadOnlyGate(
                true, true, 6, true, expectedHash);
            Require(!gate.TryConsumePreviewSetWindow(setWindow),
                "Preview SET WINDOW ran before operational initialization");

            CompleteOneOperationalInitializationCycle(gate, 100);
            Require(gate.CompletedInitializationCycles == 1 &&
                gate.PreviewSetWindowReady &&
                gate.TryConsumePreviewSetWindow(setWindow) &&
                gate.PreviewSetWindowConsumed &&
                !gate.PreviewSetWindowReady,
                "completed initialization did not unlock exact SET WINDOW once");
            Require(!gate.TryConsumePreviewSetWindow(setWindow),
                "Preview SET WINDOW gate accepted a repeated request");
            gate.RecordPreviewSetWindowCompletion(new BrokerCompletion(
                setWindow.RequestId, setWindow.Generation,
                BrokerTransportStatus.Success, 0, 0, 84, 0,
                new byte[0], new byte[0]));
            BrokerRequest imageRead = PreviewImageReadRequest(24, 640);
            Require(gate.PreviewSetWindowSucceeded &&
                gate.TryCapturePredictedPreviewImageRead(imageRead) &&
                gate.PredictedPreviewImageReadCaptured &&
                !gate.PreviewSetWindowSucceeded &&
                !gate.TryCapturePredictedPreviewImageRead(imageRead),
                "successful SET WINDOW did not capture the predicted next " +
                "one-row READ exactly once");

            OperationalReadOnlyGate invalidated = new OperationalReadOnlyGate(
                true, true, 6, true, expectedHash);
            CompleteOneOperationalInitializationCycle(invalidated, 200);
            byte[] wrong = (byte[])data.Clone();
            wrong[83] = 1;
            Require(!invalidated.TryConsumePreviewSetWindow(
                    PreviewSetWindowRequest(23, wrong)) &&
                !invalidated.PreviewSetWindowReady &&
                !invalidated.TryConsumePreviewSetWindow(setWindow),
                "a variant immediate request did not invalidate the SET WINDOW " +
                "allowance");

            OperationalReadOnlyGate disabled = new OperationalReadOnlyGate(
                true, true, 6, false, expectedHash);
            CompleteOneOperationalInitializationCycle(disabled, 300);
            Require(!disabled.TryConsumePreviewSetWindow(setWindow),
                "disabled Preview SET WINDOW gate authorized a request");

            OperationalReadOnlyGate mismatch = new OperationalReadOnlyGate(
                true, true, 6, true, expectedHash);
            CompleteOneOperationalInitializationCycle(mismatch, 400);
            Require(mismatch.TryConsumePreviewSetWindow(setWindow),
                "mismatch test could not consume SET WINDOW");
            mismatch.RecordPreviewSetWindowCompletion(new BrokerCompletion(
                setWindow.RequestId, setWindow.Generation,
                BrokerTransportStatus.Success, 0, 0, 84, 0,
                new byte[0], new byte[0]));
            BrokerRequest wrongImageRead = PreviewImageReadRequest(25, 640);
            byte[] wrongCdb = wrongImageRead.Cdb;
            wrongCdb[4] = 0x50;
            wrongImageRead = new BrokerRequest(25, 3, 0, 0, 0, 5, 0,
                BrokerDirection.In, 3840, 60000, 32, wrongCdb);
            Require(!mismatch.TryCapturePredictedPreviewImageRead(
                    wrongImageRead) &&
                !mismatch.PreviewSetWindowSucceeded &&
                !mismatch.TryCapturePredictedPreviewImageRead(imageRead),
                "a mismatched immediate request did not consume the static " +
                "observation point");

            OperationalReadOnlyGate failed = new OperationalReadOnlyGate(
                true, true, 6, true, expectedHash);
            CompleteOneOperationalInitializationCycle(failed, 500);
            Require(failed.TryConsumePreviewSetWindow(setWindow),
                "failed-completion test could not consume SET WINDOW");
            failed.RecordPreviewSetWindowCompletion(new BrokerCompletion(
                setWindow.RequestId, setWindow.Generation,
                BrokerTransportStatus.Success, 2, 2, 0, 84,
                new byte[0], new byte[0]));
            Require(!failed.PreviewSetWindowSucceeded &&
                !failed.TryCapturePredictedPreviewImageRead(imageRead),
                "failed SET WINDOW completion armed the image-read observer");
        }

        private static void TestExactOperationalFaultPixelReadBuffer()
        {
            byte[] data = new byte[22];
            FakeTransport transport = new FakeTransport(
                new RawScsiResult(0x00, 22, 0, data));
            BrokerRequest request = FaultPixelReadBufferRequest(18);

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.Execute(request, transport);
            }, "normal read-only executor accepted fault-pixel READ BUFFER");
            Require(transport.CallCount == 0,
                "normal executor sent fault-pixel READ BUFFER to hardware");

            BrokerCompletion completion = ReadOnlyScsiExecutor.
                ExecuteCapturedOperationalFaultPixelReadBufferOnce(request,
                    transport);
            Require(completion.TransportStatus ==
                    BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 22 && completion.Residue == 0 &&
                completion.Data.Length == 22 && transport.CallCount == 1 &&
                transport.OperationalFaultPixelReadBufferCallCount == 1,
                "exact fault-pixel READ BUFFER completion was mapped incorrectly");

            byte[] wrongCdb =
                ScsiFraming.BuildPrecisionTwoFaultPixelReadBufferCdb();
            wrongCdb[4] = 3;
            BrokerRequest wrong = new BrokerRequest(19, 3, 0, 0, 0, 5, 0,
                BrokerDirection.In, 22, 60000, 32, wrongCdb);
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedOperationalFaultPixelReadBufferOnce(wrong,
                        transport);
            }, "variant fault-pixel READ BUFFER was accepted");
            Require(transport.CallCount == 1,
                "variant fault-pixel READ BUFFER reached hardware");
        }

        private static void TestExactOperationalFaultPixelDataReadBuffer()
        {
            FakeTransport transport = new FakeTransport(
                new RawScsiResult(0x00, 58, 0, new byte[58]));
            BrokerRequest request = FaultPixelDataReadBufferRequest(20);

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.Execute(request, transport);
            }, "normal executor accepted fault-pixel data READ BUFFER");
            Require(transport.CallCount == 0,
                "normal executor sent fault-pixel data READ BUFFER");

            BrokerCompletion completion = ReadOnlyScsiExecutor.
                ExecuteCapturedOperationalFaultPixelDataReadBufferOnce(request,
                    transport);
            Require(completion.TransportStatus ==
                    BrokerTransportStatus.Success &&
                completion.TransferLength == 58 && completion.Residue == 0 &&
                completion.Data.Length == 58 && transport.CallCount == 1 &&
                transport.OperationalFaultPixelDataReadBufferCallCount == 1,
                "exact fault-pixel data completion was mapped incorrectly");

            byte[] wrongCdb =
                ScsiFraming.BuildPrecisionTwoFaultPixelDataReadBufferCdb();
            wrongCdb[8] = 0x39;
            BrokerRequest wrong = new BrokerRequest(21, 3, 0, 0, 0, 5, 0,
                BrokerDirection.In, 58, 60000, 32, wrongCdb);
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedOperationalFaultPixelDataReadBufferOnce(
                        wrong, transport);
            }, "variant fault-pixel data READ BUFFER was accepted");
            Require(transport.CallCount == 1,
                "variant fault-pixel data READ BUFFER reached hardware");
        }

        private static void TestExactOperationalCalibrationReadBuffer()
        {
            FakeTransport transport = new FakeTransport(
                new RawScsiResult(0x00, 1024, 0, new byte[1024]));
            BrokerRequest request = CalibrationReadBufferRequest(22);

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.Execute(request, transport);
            }, "normal executor accepted calibration READ BUFFER");
            Require(transport.CallCount == 0,
                "normal executor sent calibration READ BUFFER");

            BrokerCompletion completion = ReadOnlyScsiExecutor.
                ExecuteCapturedOperationalCalibrationReadBufferOnce(request,
                    transport);
            Require(completion.TransportStatus ==
                    BrokerTransportStatus.Success &&
                completion.TransferLength == 1024 &&
                completion.Residue == 0 &&
                completion.Data.Length == 1024 && transport.CallCount == 1 &&
                transport.OperationalCalibrationReadBufferCallCount == 1,
                "exact calibration completion was mapped incorrectly");

            byte[] wrongCdb =
                ScsiFraming.BuildPrecisionTwoCalibrationReadBufferCdb();
            wrongCdb[7] = 3;
            BrokerRequest wrong = new BrokerRequest(23, 3, 0, 0, 0, 5, 0,
                BrokerDirection.In, 1024, 60000, 32, wrongCdb);
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedOperationalCalibrationReadBufferOnce(
                        wrong, transport);
            }, "variant calibration READ BUFFER was accepted");
            Require(transport.CallCount == 1,
                "variant calibration READ BUFFER reached hardware");
        }

        private static void TestExactOperationalD8Offset55ReadBuffer()
        {
            FakeTransport transport = new FakeTransport(
                new RawScsiResult(0x00, 10, 0, new byte[10]));
            BrokerRequest request = D8Offset55ReadBufferRequest(24);

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.Execute(request, transport);
            }, "normal executor accepted D8 offset-55 READ BUFFER");
            Require(transport.CallCount == 0,
                "normal executor sent D8 offset-55 READ BUFFER");

            BrokerCompletion completion = ReadOnlyScsiExecutor.
                ExecuteCapturedOperationalD8Offset55ReadBufferOnce(request,
                    transport);
            Require(completion.TransportStatus ==
                    BrokerTransportStatus.Success &&
                completion.TransferLength == 10 && completion.Residue == 0 &&
                completion.Data.Length == 10 && transport.CallCount == 1 &&
                transport.OperationalD8Offset55ReadBufferCallCount == 1,
                "exact D8 offset-55 completion was mapped incorrectly");

            byte[] wrongCdb =
                ScsiFraming.BuildPrecisionTwoD8Offset55ReadBufferCdb();
            wrongCdb[5] = 0x36;
            BrokerRequest wrong = new BrokerRequest(25, 3, 0, 0, 0, 5, 0,
                BrokerDirection.In, 10, 60000, 32, wrongCdb);
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedOperationalD8Offset55ReadBufferOnce(
                        wrong, transport);
            }, "variant D8 offset-55 READ BUFFER was accepted");
            Require(transport.CallCount == 1,
                "variant D8 offset-55 READ BUFFER reached hardware");
        }

        private static void TestExactLoaderWriteBufferModeOneZero()
        {
            FakeTransport transport = new FakeTransport(
                new RawScsiResult(0x00, 0, 0, new byte[0]));
            BrokerRequest request = LoaderWriteBufferModeOneZeroRequest(14);

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.Execute(request, transport);
            }, "normal read-only executor accepted WRITE BUFFER");
            Require(transport.CallCount == 0,
                "normal executor sent WRITE BUFFER to hardware");

            BrokerCompletion completion = ReadOnlyScsiExecutor.
                ExecuteCapturedLoaderWriteBufferModeOneZeroOnce(
                    request, transport);
            Require(completion.TransportStatus == BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 0 && completion.Residue == 0 &&
                completion.Data.Length == 0 && completion.Sense.Length == 0 &&
                transport.CallCount == 1 &&
                transport.LoaderWriteBufferModeOneZeroCallCount == 1,
                "exact zero-length WRITE BUFFER completion was mapped incorrectly");

            byte[] wrongCdb = ScsiFraming.
                BuildPrecisionTwoLoaderWriteBufferModeOneZeroCdb();
            wrongCdb[1] = 0x00;
            ExpectExactLoaderWriteBufferModeOneZeroBlocked(new BrokerRequest(
                15, 3, 0, 0, 0, 5, 0, BrokerDirection.Out, 0, 60000, 32,
                wrongCdb), transport, "WRITE BUFFER mode variant was accepted");
            ExpectExactLoaderWriteBufferModeOneZeroBlocked(new BrokerRequest(
                16, 3, 0, 0, 0, 4, 0, BrokerDirection.Out, 0, 60000, 32,
                ScsiFraming.BuildPrecisionTwoLoaderWriteBufferModeOneZeroCdb()),
                transport, "wrong WRITE BUFFER target was accepted");
            ExpectExactLoaderWriteBufferModeOneZeroBlocked(new BrokerRequest(
                17, 3, 0, 0, 0, 5, 0, BrokerDirection.None, 0, 60000, 32,
                ScsiFraming.BuildPrecisionTwoLoaderWriteBufferModeOneZeroCdb()),
                transport, "wrong WRITE BUFFER direction was accepted");
            ExpectExactLoaderWriteBufferModeOneZeroBlocked(new BrokerRequest(
                18, 3, 0, 0, 0, 5, 0, BrokerDirection.Out, 1, 60000, 32,
                ScsiFraming.BuildPrecisionTwoLoaderWriteBufferModeOneZeroCdb()),
                transport, "nonzero WRITE BUFFER length was accepted");
            ExpectExactLoaderWriteBufferModeOneZeroBlocked(new BrokerRequest(
                19, 3, 0, 0, 0, 5, 0, BrokerDirection.Out, 0, 59999, 32,
                ScsiFraming.BuildPrecisionTwoLoaderWriteBufferModeOneZeroCdb()),
                transport, "wrong WRITE BUFFER timeout was accepted");
            ExpectExactLoaderWriteBufferModeOneZeroBlocked(new BrokerRequest(
                20, 3, 0, 0, 0, 5, 0, BrokerDirection.Out, 0, 60000, 31,
                ScsiFraming.BuildPrecisionTwoLoaderWriteBufferModeOneZeroCdb()),
                transport, "wrong WRITE BUFFER sense length was accepted");
            Require(transport.CallCount == 1,
                "variant WRITE BUFFER reached hardware");

            FakeTransport checkCondition = new FakeTransport(
                new RawScsiResult(0x02, 0, 0, new byte[0]));
            BrokerCompletion checkCompletion = ReadOnlyScsiExecutor.
                ExecuteCapturedLoaderWriteBufferModeOneZeroOnce(
                    LoaderWriteBufferModeOneZeroRequest(21), checkCondition);
            Require(checkCompletion.ScsiStatus == 0x02 &&
                checkCompletion.Sense.Length == 0 &&
                checkCondition.CallCount == 1,
                "experimental WRITE BUFFER performed automatic sense");
        }

        private static void ExpectExactLoaderWriteBufferModeOneZeroBlocked(
            BrokerRequest request, FakeTransport transport, string message)
        {
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferModeOneZeroOnce(
                        request, transport);
            }, message);
        }

        private static void TestSimulatedLoaderWriteBufferProtocolError()
        {
            FakeTransport transport = new FakeTransport();
            BrokerRequest request = LoaderWriteBufferModeOneZeroRequest(24);
            BrokerCompletion completion = ReadOnlyScsiExecutor.
                SimulateCapturedLoaderWriteBufferModeOneZeroProtocolError(
                    request);
            Require(completion.TransportStatus ==
                    BrokerTransportStatus.ProtocolError &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength == 0 && completion.Residue == 0 &&
                completion.Data.Length == 0 && completion.Sense.Length == 0 &&
                transport.CallCount == 0,
                "simulated WRITE BUFFER error touched the transport or " +
                "returned device fields");

            byte[] wrong = ScsiFraming.
                BuildPrecisionTwoLoaderWriteBufferModeOneZeroCdb();
            wrong[9] = 1;
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    SimulateCapturedLoaderWriteBufferModeOneZeroProtocolError(
                        new BrokerRequest(25, 3, 0, 0, 0, 5, 0,
                            BrokerDirection.Out, 0, 60000, 32, wrong));
            }, "simulated WRITE BUFFER accepted a CDB variant");
            Require(transport.CallCount == 0,
                "simulated WRITE BUFFER variant touched the transport");
        }

        private static void TestLoaderWriteBufferModeOneZeroOneShotGate()
        {
            BrokerRequest d8 = LoaderReadBufferD8Request(22);
            BrokerRequest write = LoaderWriteBufferModeOneZeroRequest(23);

            Expect<ArgumentException>(delegate
            {
                new CaptureCommandGate(false, true);
            }, "WRITE BUFFER gate was enabled without the preceding D8 gate");

            CaptureCommandGate d8Only = new CaptureCommandGate(true, false);
            Require(d8Only.TryConsumeLoaderReadBufferD8(d8) &&
                !d8Only.TryConsumeLoaderWriteBufferModeOneZero(write),
                "D8-only mode authorized WRITE BUFFER");

            CaptureCommandGate enabled = new CaptureCommandGate(true, true);
            Require(!enabled.TryConsumeLoaderWriteBufferModeOneZero(write) &&
                !enabled.LoaderWriteBufferModeOneZeroConsumed,
                "WRITE BUFFER was authorized before D8");
            Require(enabled.TryConsumeLoaderReadBufferD8(d8),
                "D8 did not establish the WRITE BUFFER boundary");
            Require(!enabled.TryConsumeLoaderWriteBufferModeOneZero(
                    InquiryRequest(5)) &&
                !enabled.LoaderWriteBufferModeOneZeroConsumed,
                "an unrelated request consumed the WRITE BUFFER allowance");
            Require(enabled.TryConsumeLoaderWriteBufferModeOneZero(write) &&
                enabled.LoaderWriteBufferModeOneZeroConsumed,
                "exact WRITE BUFFER did not consume its allowance");
            Require(!enabled.TryConsumeLoaderWriteBufferModeOneZero(write),
                "a second WRITE BUFFER was authorized");
        }

        private static void TestExactLoaderFirstRecordOneShot()
        {
            byte[] firmware = SyntheticPrecisionTwoFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                PrecisionTwoLoaderSequenceManifest.Create(firmware,
                    Sha256Hex(firmware));
            BrokerRequest request = LoaderFirstRecordRequest(firmware, 30);

            FakeTransport blockedTransport = new FakeTransport();
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.Execute(request, blockedTransport);
            }, "normal executor accepted the full first loader record");
            Require(blockedTransport.CallCount == 0,
                "normal executor sent the first loader record");

            FakeTransport successTransport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0));
            BrokerCompletion completion = ReadOnlyScsiExecutor.
                ExecuteCapturedLoaderWriteBufferFirstRecordOnce(request,
                    successTransport, manifest);
            Require(completion.TransportStatus ==
                    BrokerTransportStatus.Success &&
                completion.AdapterStatus == 0 && completion.ScsiStatus == 0 &&
                completion.TransferLength ==
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength &&
                completion.Residue == 0 && completion.Data.Length == 0 &&
                completion.Sense.Length == 0 &&
                successTransport.LoaderWriteBufferRecordCallCount == 1,
                "first loader record completion was mapped incorrectly");

            byte[] wrongData = request.Data;
            wrongData[100] ^= 1;
            FakeTransport variantTransport = new FakeTransport();
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferFirstRecordOnce(
                        LoaderRequestFromData(wrongData, 31),
                        variantTransport, manifest);
            }, "first loader record accepted a payload variant");
            Require(variantTransport.CallCount == 0,
                "first loader record variant reached the transport");

            FakeTransport shortTransport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 1));
            Expect<ProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferFirstRecordOnce(request,
                        shortTransport, manifest);
            }, "short first-record completion passed the exact policy");
            Require(shortTransport.CallCount == 1,
                "short completion did not exercise exactly one transport call");

            Expect<ArgumentException>(delegate
            {
                new CaptureCommandGate(false, false, true, manifest);
            }, "first-record gate was enabled without D8");
            CaptureCommandGate gate = new CaptureCommandGate(
                true, false, true, manifest);
            Require(!gate.ShouldHandleLoaderWriteBufferRecord(request),
                "first loader record was authorized before D8");
            Require(gate.TryConsumeLoaderReadBufferD8(
                    LoaderReadBufferD8Request(29)),
                "D8 did not establish the first-record boundary");
            Require(gate.ShouldHandleLoaderWriteBufferRecord(request) &&
                gate.AllowedLoaderRecordCount == 1,
                "exact first loader record was not routed to its validator");
        }

        private static void TestExactLoaderFirstTwoRecordPrefix()
        {
            byte[] firmware = SyntheticPrecisionTwoFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                PrecisionTwoLoaderSequenceManifest.Create(firmware,
                    Sha256Hex(firmware));
            PrecisionTwoLoaderSequenceValidator validator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport transport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0));

            BrokerCompletion first = ReadOnlyScsiExecutor.
                ExecuteCapturedLoaderWriteBufferRecordOnce(
                    LoaderRecordRequest(firmware, 0, 40), transport,
                    validator, 2);
            Require(first.TransportStatus == BrokerTransportStatus.Success &&
                validator.AcceptedRecordCount == 1 &&
                transport.LoaderWriteBufferRecordCallCount == 1,
                "record 1 did not establish the two-record prefix boundary");

            BrokerCompletion second = ReadOnlyScsiExecutor.
                ExecuteCapturedLoaderWriteBufferRecordOnce(
                    LoaderRecordRequest(firmware, 1, 41), transport,
                    validator, 2);
            Require(second.TransportStatus == BrokerTransportStatus.Success &&
                validator.AcceptedRecordCount == 2 &&
                transport.LoaderWriteBufferRecordCallCount == 2,
                "record 2 did not complete the exact two-record prefix");

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 2, 42), transport,
                        validator, 2);
            }, "a third loader record passed the two-record boundary");
            Require(transport.LoaderWriteBufferRecordCallCount == 2,
                "a third loader record reached the transport");

            PrecisionTwoLoaderSequenceValidator outOfOrderValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport outOfOrderTransport = new FakeTransport();
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 1, 50),
                        outOfOrderTransport, outOfOrderValidator, 2);
            }, "record 2 was accepted before record 1");
            Require(outOfOrderTransport.CallCount == 0,
                "out-of-order record 2 reached the transport");

            PrecisionTwoLoaderSequenceValidator failedValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport failedTransport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 1));
            Expect<ProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 0, 60),
                        failedTransport, failedValidator, 2);
            }, "a short record-1 completion passed the two-record policy");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 1, 61),
                        failedTransport, failedValidator, 2);
            }, "record 2 was accepted after record 1 failed");
            Require(failedTransport.CallCount == 1,
                "record 2 reached the transport after record 1 failed");

            CaptureCommandGate gate = new CaptureCommandGate(
                true, false, 2, manifest);
            Require(gate.TryConsumeLoaderReadBufferD8(
                    LoaderReadBufferD8Request(39)) &&
                gate.ShouldHandleLoaderWriteBufferRecord(
                    LoaderRecordRequest(firmware, 0, 40)) &&
                gate.AllowedLoaderRecordCount == 2,
                "two-record gate did not route the exact prefix candidate");
            Require(!gate.ShouldHandleLoaderWriteBufferRecord(
                    InquiryRequest(5)),
                "two-record gate routed an unrelated command");
        }

        private static void TestExactLoaderFirstThreeRecordPrefix()
        {
            byte[] firmware = SyntheticPrecisionTwoFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                PrecisionTwoLoaderSequenceManifest.Create(firmware,
                    Sha256Hex(firmware));
            PrecisionTwoLoaderSequenceValidator validator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport transport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0));

            for (int recordIndex = 0; recordIndex < 3; recordIndex++)
            {
                BrokerCompletion completion = ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(70 + recordIndex))),
                        transport, validator, 3);
                Require(completion.TransportStatus ==
                        BrokerTransportStatus.Success &&
                    validator.AcceptedRecordCount == recordIndex + 1 &&
                    transport.LoaderWriteBufferRecordCallCount ==
                        recordIndex + 1,
                    "three-record prefix did not advance exactly one record");
            }

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 3, 73), transport,
                        validator, 3);
            }, "a fourth loader record passed the three-record boundary");
            Require(transport.LoaderWriteBufferRecordCallCount == 3,
                "a fourth loader record reached the transport");

            PrecisionTwoLoaderSequenceValidator outOfOrderValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport outOfOrderTransport = new FakeTransport();
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 2, 80),
                        outOfOrderTransport, outOfOrderValidator, 3);
            }, "record 3 was accepted before records 1 and 2");
            Require(outOfOrderTransport.CallCount == 0,
                "out-of-order record 3 reached the transport");

            PrecisionTwoLoaderSequenceValidator failedValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport failedTransport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 1));
            ReadOnlyScsiExecutor.ExecuteCapturedLoaderWriteBufferRecordOnce(
                LoaderRecordRequest(firmware, 0, 90), failedTransport,
                failedValidator, 3);
            Expect<ProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 1, 91),
                        failedTransport, failedValidator, 3);
            }, "a short record-2 completion passed the three-record policy");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 2, 92),
                        failedTransport, failedValidator, 3);
            }, "record 3 was accepted after record 2 failed");
            Require(failedTransport.CallCount == 2,
                "record 3 reached the transport after record 2 failed");

            CaptureCommandGate gate = new CaptureCommandGate(
                true, false, 3, manifest);
            Require(gate.TryConsumeLoaderReadBufferD8(
                    LoaderReadBufferD8Request(69)) &&
                gate.ShouldHandleLoaderWriteBufferRecord(
                    LoaderRecordRequest(firmware, 0, 70)) &&
                gate.AllowedLoaderRecordCount == 3,
                "three-record gate did not route its exact prefix candidate");
        }

        private static void TestExactLoaderFirstFourRecordPrefix()
        {
            byte[] firmware = SyntheticPrecisionTwoFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                PrecisionTwoLoaderSequenceManifest.Create(firmware,
                    Sha256Hex(firmware));
            PrecisionTwoLoaderSequenceValidator validator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport transport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0));

            for (int recordIndex = 0; recordIndex < 4; recordIndex++)
            {
                BrokerCompletion completion = ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(100 + recordIndex))),
                        transport, validator, 4);
                Require(completion.TransportStatus ==
                        BrokerTransportStatus.Success &&
                    validator.AcceptedRecordCount == recordIndex + 1 &&
                    transport.LoaderWriteBufferRecordCallCount ==
                        recordIndex + 1,
                    "four-record prefix did not advance exactly one record");
            }

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 4, 104), transport,
                        validator, 4);
            }, "a fifth loader record passed the four-record boundary");
            Require(transport.LoaderWriteBufferRecordCallCount == 4,
                "a fifth loader record reached the transport");

            PrecisionTwoLoaderSequenceValidator outOfOrderValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport outOfOrderTransport = new FakeTransport();
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 3, 110),
                        outOfOrderTransport, outOfOrderValidator, 4);
            }, "record 4 was accepted before records 1 through 3");
            Require(outOfOrderTransport.CallCount == 0,
                "out-of-order record 4 reached the transport");

            PrecisionTwoLoaderSequenceValidator failedValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport failedTransport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 1));
            ReadOnlyScsiExecutor.ExecuteCapturedLoaderWriteBufferRecordOnce(
                LoaderRecordRequest(firmware, 0, 120), failedTransport,
                failedValidator, 4);
            ReadOnlyScsiExecutor.ExecuteCapturedLoaderWriteBufferRecordOnce(
                LoaderRecordRequest(firmware, 1, 121), failedTransport,
                failedValidator, 4);
            Expect<ProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 2, 122),
                        failedTransport, failedValidator, 4);
            }, "a short record-3 completion passed the four-record policy");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 3, 123),
                        failedTransport, failedValidator, 4);
            }, "record 4 was accepted after record 3 failed");
            Require(failedTransport.CallCount == 3,
                "record 4 reached the transport after record 3 failed");

            CaptureCommandGate gate = new CaptureCommandGate(
                true, false, 4, manifest);
            Require(gate.TryConsumeLoaderReadBufferD8(
                    LoaderReadBufferD8Request(99)) &&
                gate.ShouldHandleLoaderWriteBufferRecord(
                    LoaderRecordRequest(firmware, 0, 100)) &&
                gate.AllowedLoaderRecordCount == 4,
                "four-record gate did not route its exact prefix candidate");
        }

        private static void TestExactLoaderFirstFiveRecordPrefix()
        {
            byte[] firmware = SyntheticPrecisionTwoFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                PrecisionTwoLoaderSequenceManifest.Create(firmware,
                    Sha256Hex(firmware));
            PrecisionTwoLoaderSequenceValidator validator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport transport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0));

            for (int recordIndex = 0; recordIndex < 5; recordIndex++)
            {
                BrokerCompletion completion = ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(130 + recordIndex))),
                        transport, validator, 5);
                Require(completion.TransportStatus ==
                        BrokerTransportStatus.Success &&
                    validator.AcceptedRecordCount == recordIndex + 1 &&
                    transport.LoaderWriteBufferRecordCallCount ==
                        recordIndex + 1,
                    "five-record prefix did not advance exactly one record");
            }

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 5, 135), transport,
                        validator, 5);
            }, "a sixth loader record passed the five-record boundary");
            Require(transport.LoaderWriteBufferRecordCallCount == 5,
                "a sixth loader record reached the transport");

            PrecisionTwoLoaderSequenceValidator terminalValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport terminalTransport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0));
            for (int recordIndex = 0; recordIndex < 5; recordIndex++)
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(160 + recordIndex))),
                        terminalTransport, terminalValidator, 5);
            }
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderTerminalRecordRequest(165), terminalTransport,
                        terminalValidator, 5);
            }, "the terminal record passed the five-record boundary");
            Require(terminalTransport.LoaderWriteBufferRecordCallCount == 5,
                "the terminal record reached the transport");

            PrecisionTwoLoaderSequenceValidator outOfOrderValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport outOfOrderTransport = new FakeTransport();
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 4, 140),
                        outOfOrderTransport, outOfOrderValidator, 5);
            }, "record 5 was accepted before records 1 through 4");
            Require(outOfOrderTransport.CallCount == 0,
                "out-of-order record 5 reached the transport");

            PrecisionTwoLoaderSequenceValidator failedValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport failedTransport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 1));
            for (int recordIndex = 0; recordIndex < 3; recordIndex++)
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(150 + recordIndex))),
                        failedTransport, failedValidator, 5);
            }
            Expect<ProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 3, 153),
                        failedTransport, failedValidator, 5);
            }, "a short record-4 completion passed the five-record policy");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 4, 154),
                        failedTransport, failedValidator, 5);
            }, "record 5 was accepted after record 4 failed");
            Require(failedTransport.CallCount == 4,
                "record 5 reached the transport after record 4 failed");

            CaptureCommandGate gate = new CaptureCommandGate(
                true, false, 5, manifest);
            Require(gate.TryConsumeLoaderReadBufferD8(
                    LoaderReadBufferD8Request(129)) &&
                gate.ShouldHandleLoaderWriteBufferRecord(
                    LoaderRecordRequest(firmware, 0, 130)) &&
                gate.AllowedLoaderRecordCount == 5,
                "five-record gate did not route its exact prefix candidate");
        }

        private static void TestExactLoaderFirstSixRecordPrefix()
        {
            byte[] firmware = SyntheticPrecisionTwoFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                PrecisionTwoLoaderSequenceManifest.Create(firmware,
                    Sha256Hex(firmware));
            PrecisionTwoLoaderSequenceValidator validator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport transport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0));

            for (int recordIndex = 0; recordIndex < 6; recordIndex++)
            {
                BrokerCompletion completion = ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(180 + recordIndex))),
                        transport, validator, 6);
                Require(completion.TransportStatus ==
                        BrokerTransportStatus.Success &&
                    validator.AcceptedRecordCount == recordIndex + 1 &&
                    transport.LoaderWriteBufferRecordCallCount ==
                        recordIndex + 1,
                    "six-record prefix did not advance exactly one record");
            }

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 6, 186), transport,
                        validator, 6);
            }, "a seventh loader record passed the six-record boundary");
            Require(transport.LoaderWriteBufferRecordCallCount == 6,
                "a seventh loader record reached the transport");

            PrecisionTwoLoaderSequenceValidator terminalValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport terminalTransport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0));
            for (int recordIndex = 0; recordIndex < 6; recordIndex++)
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(190 + recordIndex))),
                        terminalTransport, terminalValidator, 6);
            }
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderTerminalRecordRequest(196), terminalTransport,
                        terminalValidator, 6);
            }, "the terminal record passed the six-record boundary");
            Require(terminalTransport.LoaderWriteBufferRecordCallCount == 6,
                "the terminal record reached the transport");

            PrecisionTwoLoaderSequenceValidator outOfOrderValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport outOfOrderTransport = new FakeTransport();
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 5, 200),
                        outOfOrderTransport, outOfOrderValidator, 6);
            }, "record 6 was accepted before records 1 through 5");
            Require(outOfOrderTransport.CallCount == 0,
                "out-of-order record 6 reached the transport");

            PrecisionTwoLoaderSequenceValidator failedValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport failedTransport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 1));
            for (int recordIndex = 0; recordIndex < 4; recordIndex++)
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(210 + recordIndex))),
                        failedTransport, failedValidator, 6);
            }
            Expect<ProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 4, 214),
                        failedTransport, failedValidator, 6);
            }, "a short record-5 completion passed the six-record policy");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 5, 215),
                        failedTransport, failedValidator, 6);
            }, "record 6 was accepted after record 5 failed");
            Require(failedTransport.CallCount == 5,
                "record 6 reached the transport after record 5 failed");

            CaptureCommandGate gate = new CaptureCommandGate(
                true, false, 6, manifest);
            Require(gate.TryConsumeLoaderReadBufferD8(
                    LoaderReadBufferD8Request(179)) &&
                gate.ShouldHandleLoaderWriteBufferRecord(
                    LoaderRecordRequest(firmware, 0, 180)) &&
                gate.AllowedLoaderRecordCount == 6,
                "six-record gate did not route its exact prefix candidate");
        }

        private static void TestExactLoaderFirstEightRecordPrefix()
        {
            byte[] firmware = SyntheticPrecisionTwoFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                PrecisionTwoLoaderSequenceManifest.Create(firmware,
                    Sha256Hex(firmware));
            PrecisionTwoLoaderSequenceValidator validator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport transport = new FakeTransport(
                SuccessfulLoaderRecordResults(8));

            for (int recordIndex = 0; recordIndex < 8; recordIndex++)
            {
                BrokerCompletion completion = ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(220 + recordIndex))),
                        transport, validator, 8);
                Require(completion.TransportStatus ==
                        BrokerTransportStatus.Success &&
                    validator.AcceptedRecordCount == recordIndex + 1 &&
                    transport.LoaderWriteBufferRecordCallCount ==
                        recordIndex + 1,
                    "eight-record prefix did not advance exactly one record");
            }

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 8, 228), transport,
                        validator, 8);
            }, "a ninth loader record passed the eight-record boundary");
            Require(transport.LoaderWriteBufferRecordCallCount == 8,
                "a ninth loader record reached the transport");

            PrecisionTwoLoaderSequenceValidator terminalValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport terminalTransport = new FakeTransport(
                SuccessfulLoaderRecordResults(8));
            for (int recordIndex = 0; recordIndex < 8; recordIndex++)
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(230 + recordIndex))),
                        terminalTransport, terminalValidator, 8);
            }
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderTerminalRecordRequest(238), terminalTransport,
                        terminalValidator, 8);
            }, "the terminal record passed the eight-record boundary");
            Require(terminalTransport.LoaderWriteBufferRecordCallCount == 8,
                "the terminal record reached the transport");

            PrecisionTwoLoaderSequenceValidator outOfOrderValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport outOfOrderTransport = new FakeTransport();
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 7, 240),
                        outOfOrderTransport, outOfOrderValidator, 8);
            }, "record 8 was accepted before records 1 through 7");
            Require(outOfOrderTransport.CallCount == 0,
                "out-of-order record 8 reached the transport");

            PrecisionTwoLoaderSequenceValidator failedValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport failedTransport = new FakeTransport(
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0),
                RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 1));
            for (int recordIndex = 0; recordIndex < 6; recordIndex++)
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(250 + recordIndex))),
                        failedTransport, failedValidator, 8);
            }
            Expect<ProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 6, 256),
                        failedTransport, failedValidator, 8);
            }, "a short record-7 completion passed the eight-record policy");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 7, 257),
                        failedTransport, failedValidator, 8);
            }, "record 8 was accepted after record 7 failed");
            Require(failedTransport.CallCount == 7,
                "record 8 reached the transport after record 7 failed");

            CaptureCommandGate gate = new CaptureCommandGate(
                true, false, 8, manifest);
            Require(gate.TryConsumeLoaderReadBufferD8(
                    LoaderReadBufferD8Request(219)) &&
                gate.ShouldHandleLoaderWriteBufferRecord(
                    LoaderRecordRequest(firmware, 0, 220)) &&
                gate.AllowedLoaderRecordCount == 8,
                "eight-record gate did not route its exact prefix candidate");
        }

        private static void TestExactLoaderFirstTenRecordPrefix()
        {
            byte[] firmware = SyntheticPrecisionTwoFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                PrecisionTwoLoaderSequenceManifest.Create(firmware,
                    Sha256Hex(firmware));
            PrecisionTwoLoaderSequenceValidator validator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport transport = new FakeTransport(
                SuccessfulLoaderRecordResults(10));

            for (int recordIndex = 0; recordIndex < 10; recordIndex++)
            {
                BrokerCompletion completion = ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(270 + recordIndex))),
                        transport, validator, 10);
                Require(completion.TransportStatus ==
                        BrokerTransportStatus.Success &&
                    validator.AcceptedRecordCount == recordIndex + 1 &&
                    transport.LoaderWriteBufferRecordCallCount ==
                        recordIndex + 1,
                    "ten-record prefix did not advance exactly one record");
            }

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderTerminalRecordRequest(280), transport,
                        validator, 10);
            }, "the terminal record passed the ten-record boundary");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 10, 281), transport,
                        validator, 10);
            }, "an eleventh loader record passed the ten-record boundary");
            Require(transport.LoaderWriteBufferRecordCallCount == 10,
                "a terminal or eleventh record reached the transport");

            PrecisionTwoLoaderSequenceValidator outOfOrderValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport outOfOrderTransport = new FakeTransport();
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 9, 282),
                        outOfOrderTransport, outOfOrderValidator, 10);
            }, "record 10 was accepted before records 1 through 9");
            Require(outOfOrderTransport.CallCount == 0,
                "out-of-order record 10 reached the transport");

            PrecisionTwoLoaderSequenceValidator failedValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            RawScsiResult[] failedResults =
                SuccessfulLoaderRecordResults(9);
            failedResults[8] = RawScsiResult.ForDataOut(0x00,
                ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 1);
            FakeTransport failedTransport = new FakeTransport(failedResults);
            for (int recordIndex = 0; recordIndex < 8; recordIndex++)
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(283 + recordIndex))),
                        failedTransport, failedValidator, 10);
            }
            Expect<ProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 8, 291),
                        failedTransport, failedValidator, 10);
            }, "a short record-9 completion passed the ten-record policy");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 9, 292),
                        failedTransport, failedValidator, 10);
            }, "record 10 was accepted after record 9 failed");
            Require(failedTransport.CallCount == 9,
                "record 10 reached the transport after record 9 failed");

            CaptureCommandGate gate = new CaptureCommandGate(
                true, false, 10, manifest);
            Require(gate.TryConsumeLoaderReadBufferD8(
                    LoaderReadBufferD8Request(269)) &&
                gate.ShouldHandleLoaderWriteBufferRecord(
                    LoaderRecordRequest(firmware, 0, 270)) &&
                gate.AllowedLoaderRecordCount == 10,
                "ten-record gate did not route its exact prefix candidate");
        }

        private static void TestExactLoaderFirstTwelveRecordPrefix()
        {
            byte[] firmware = SyntheticPrecisionTwoFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                PrecisionTwoLoaderSequenceManifest.Create(firmware,
                    Sha256Hex(firmware));
            PrecisionTwoLoaderSequenceValidator validator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport transport = new FakeTransport(
                SuccessfulLoaderRecordResults(12));

            for (int recordIndex = 0; recordIndex < 12; recordIndex++)
            {
                BrokerCompletion completion = ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(300 + recordIndex))),
                        transport, validator, 12);
                Require(completion.TransportStatus ==
                        BrokerTransportStatus.Success &&
                    validator.AcceptedRecordCount == recordIndex + 1 &&
                    transport.LoaderWriteBufferRecordCallCount ==
                        recordIndex + 1,
                    "twelve-record prefix did not advance exactly one record");
            }

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderTerminalRecordRequest(312), transport,
                        validator, 12);
            }, "the terminal record passed the twelve-record boundary");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 12, 313), transport,
                        validator, 12);
            }, "a thirteenth loader record passed the twelve-record boundary");
            Require(transport.LoaderWriteBufferRecordCallCount == 12,
                "a terminal or thirteenth record reached the transport");

            PrecisionTwoLoaderSequenceValidator outOfOrderValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport outOfOrderTransport = new FakeTransport();
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 11, 314),
                        outOfOrderTransport, outOfOrderValidator, 12);
            }, "record 12 was accepted before records 1 through 11");
            Require(outOfOrderTransport.CallCount == 0,
                "out-of-order record 12 reached the transport");

            PrecisionTwoLoaderSequenceValidator failedValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            RawScsiResult[] failedResults =
                SuccessfulLoaderRecordResults(11);
            failedResults[10] = RawScsiResult.ForDataOut(0x00,
                ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 1);
            FakeTransport failedTransport = new FakeTransport(failedResults);
            for (int recordIndex = 0; recordIndex < 10; recordIndex++)
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(315 + recordIndex))),
                        failedTransport, failedValidator, 12);
            }
            Expect<ProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 10, 325),
                        failedTransport, failedValidator, 12);
            }, "a short record-11 completion passed the twelve-record policy");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 11, 326),
                        failedTransport, failedValidator, 12);
            }, "record 12 was accepted after record 11 failed");
            Require(failedTransport.CallCount == 11,
                "record 12 reached the transport after record 11 failed");

            CaptureCommandGate gate = new CaptureCommandGate(
                true, false, 12, manifest);
            Require(gate.TryConsumeLoaderReadBufferD8(
                    LoaderReadBufferD8Request(299)) &&
                gate.ShouldHandleLoaderWriteBufferRecord(
                    LoaderRecordRequest(firmware, 0, 300)) &&
                gate.AllowedLoaderRecordCount == 12,
                "twelve-record gate did not route its exact prefix candidate");
        }

        private static void TestExactLoaderFirstFourteenRecordPrefix()
        {
            byte[] firmware = SyntheticPrecisionTwoFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                PrecisionTwoLoaderSequenceManifest.Create(firmware,
                    Sha256Hex(firmware));
            PrecisionTwoLoaderSequenceValidator validator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport transport = new FakeTransport(
                SuccessfulLoaderRecordResults(14));

            for (int recordIndex = 0; recordIndex < 14; recordIndex++)
            {
                BrokerCompletion completion = ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(340 + recordIndex))),
                        transport, validator, 14);
                Require(completion.TransportStatus ==
                        BrokerTransportStatus.Success &&
                    validator.AcceptedRecordCount == recordIndex + 1 &&
                    transport.LoaderWriteBufferRecordCallCount ==
                        recordIndex + 1,
                    "fourteen-record prefix did not advance exactly one record");
            }

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderTerminalRecordRequest(354), transport,
                        validator, 14);
            }, "the terminal record passed the fourteen-record boundary");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 14, 355), transport,
                        validator, 14);
            }, "a fifteenth loader record passed the fourteen-record boundary");
            Require(transport.LoaderWriteBufferRecordCallCount == 14,
                "a terminal or fifteenth record reached the transport");

            PrecisionTwoLoaderSequenceValidator outOfOrderValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport outOfOrderTransport = new FakeTransport();
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 13, 356),
                        outOfOrderTransport, outOfOrderValidator, 14);
            }, "record 14 was accepted before records 1 through 13");
            Require(outOfOrderTransport.CallCount == 0,
                "out-of-order record 14 reached the transport");

            PrecisionTwoLoaderSequenceValidator failedValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            RawScsiResult[] failedResults =
                SuccessfulLoaderRecordResults(13);
            failedResults[12] = RawScsiResult.ForDataOut(0x00,
                ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 1);
            FakeTransport failedTransport = new FakeTransport(failedResults);
            for (int recordIndex = 0; recordIndex < 12; recordIndex++)
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(357 + recordIndex))),
                        failedTransport, failedValidator, 14);
            }
            Expect<ProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 12, 369),
                        failedTransport, failedValidator, 14);
            }, "a short record-13 completion passed the fourteen-record policy");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 13, 370),
                        failedTransport, failedValidator, 14);
            }, "record 14 was accepted after record 13 failed");
            Require(failedTransport.CallCount == 13,
                "record 14 reached the transport after record 13 failed");

            CaptureCommandGate gate = new CaptureCommandGate(
                true, false, 14, manifest);
            Require(gate.TryConsumeLoaderReadBufferD8(
                    LoaderReadBufferD8Request(339)) &&
                gate.ShouldHandleLoaderWriteBufferRecord(
                    LoaderRecordRequest(firmware, 0, 340)) &&
                gate.AllowedLoaderRecordCount == 14,
                "fourteen-record gate did not route its exact prefix candidate");
        }

        private static void TestExactLoaderAllSixteenRecordPrefix()
        {
            byte[] firmware = SyntheticPrecisionTwoFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                PrecisionTwoLoaderSequenceManifest.Create(firmware,
                    Sha256Hex(firmware));
            PrecisionTwoLoaderSequenceValidator validator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport transport = new FakeTransport(
                SuccessfulLoaderRecordResults(16));

            for (int recordIndex = 0; recordIndex < 16; recordIndex++)
            {
                BrokerCompletion completion = ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(380 + recordIndex))),
                        transport, validator, 16);
                Require(completion.TransportStatus ==
                        BrokerTransportStatus.Success &&
                    validator.AcceptedRecordCount == recordIndex + 1 &&
                    transport.LoaderWriteBufferRecordCallCount ==
                        recordIndex + 1,
                    "sixteen-record prefix did not advance exactly one record");
            }

            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderTerminalRecordRequest(396), transport,
                        validator, 16);
            }, "the terminal record passed the sixteen-record boundary");
            Require(transport.LoaderWriteBufferRecordCallCount == 16,
                "the terminal record reached the sixteen-record transport");

            PrecisionTwoLoaderSequenceValidator outOfOrderValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport outOfOrderTransport = new FakeTransport();
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 15, 397),
                        outOfOrderTransport, outOfOrderValidator, 16);
            }, "record 16 was accepted before records 1 through 15");
            Require(outOfOrderTransport.CallCount == 0,
                "out-of-order record 16 reached the transport");

            PrecisionTwoLoaderSequenceValidator failedValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            RawScsiResult[] failedResults =
                SuccessfulLoaderRecordResults(15);
            failedResults[14] = RawScsiResult.ForDataOut(0x00,
                ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 1);
            FakeTransport failedTransport = new FakeTransport(failedResults);
            for (int recordIndex = 0; recordIndex < 14; recordIndex++)
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(398 + recordIndex))),
                        failedTransport, failedValidator, 16);
            }
            Expect<ProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 14, 412),
                        failedTransport, failedValidator, 16);
            }, "a short record-15 completion passed the sixteen-record policy");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 15, 413),
                        failedTransport, failedValidator, 16);
            }, "record 16 was accepted after record 15 failed");
            Require(failedTransport.CallCount == 15,
                "record 16 reached the transport after record 15 failed");

            CaptureCommandGate gate = new CaptureCommandGate(
                true, false, 16, manifest);
            Require(gate.TryConsumeLoaderReadBufferD8(
                    LoaderReadBufferD8Request(379)) &&
                gate.ShouldHandleLoaderWriteBufferRecord(
                    LoaderRecordRequest(firmware, 0, 380)) &&
                gate.AllowedLoaderRecordCount == 16,
                "sixteen-record gate did not route its exact prefix candidate");
        }

        private static void TestExactLoaderCompleteSequence()
        {
            byte[] firmware = SyntheticPrecisionTwoFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                PrecisionTwoLoaderSequenceManifest.Create(firmware,
                    Sha256Hex(firmware));
            PrecisionTwoLoaderSequenceValidator validator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport transport = new FakeTransport(
                SuccessfulCompleteLoaderResults());
            for (int recordIndex = 0; recordIndex < 16; recordIndex++)
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(430 + recordIndex))),
                        transport, validator, 16);
            }
            BrokerCompletion terminal = ReadOnlyScsiExecutor.
                ExecuteCapturedLoaderWriteBufferTerminalOnce(
                    LoaderTerminalRecordRequest(446), transport, validator);
            Require(terminal.TransportStatus == BrokerTransportStatus.Success &&
                terminal.TransferLength == 10 && terminal.Residue == 0 &&
                validator.State == PrecisionTwoLoaderSequenceState.Complete &&
                validator.AcceptedRecordCount == 16 &&
                transport.LoaderWriteBufferRecordCallCount == 16 &&
                transport.LoaderWriteBufferTerminalCallCount == 1 &&
                transport.CallCount == 17,
                "the complete loader sequence did not execute exactly once");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferTerminalOnce(
                        LoaderTerminalRecordRequest(447), transport, validator);
            }, "a second loader terminal was accepted");
            Require(transport.LoaderWriteBufferTerminalCallCount == 1,
                "a second loader terminal reached the transport");

            PrecisionTwoLoaderSequenceValidator prematureValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport prematureTransport = new FakeTransport();
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferTerminalOnce(
                        LoaderTerminalRecordRequest(448), prematureTransport,
                        prematureValidator);
            }, "the terminal was accepted before sixteen records");
            Require(prematureTransport.CallCount == 0,
                "a premature terminal reached the transport");

            PrecisionTwoLoaderSequenceValidator variantValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FakeTransport variantTransport = new FakeTransport(
                SuccessfulLoaderRecordResults(16));
            for (int recordIndex = 0; recordIndex < 16; recordIndex++)
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(449 + recordIndex))),
                        variantTransport, variantValidator, 16);
            }
            BrokerRequest exactTerminal = LoaderTerminalRecordRequest(465);
            byte[] wrongCdb = exactTerminal.Cdb;
            wrongCdb[8] = 0x09;
            BrokerRequest variantTerminal = new BrokerRequest(465, 3, 0, 0,
                0, 5, 0, BrokerDirection.Out, 10, 60000, 32, wrongCdb,
                exactTerminal.Data);
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferTerminalOnce(
                        variantTerminal, variantTransport, variantValidator);
            }, "a terminal CDB variant was accepted");
            Require(variantTransport.LoaderWriteBufferTerminalCallCount == 0,
                "a terminal CDB variant reached the transport");

            PrecisionTwoLoaderSequenceValidator failedRecordValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            RawScsiResult[] failedRecordResults =
                SuccessfulLoaderRecordResults(16);
            failedRecordResults[15] = RawScsiResult.ForDataOut(0x00,
                ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 1);
            FakeTransport failedRecordTransport =
                new FakeTransport(failedRecordResults);
            for (int recordIndex = 0; recordIndex < 15; recordIndex++)
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(466 + recordIndex))),
                        failedRecordTransport, failedRecordValidator, 16);
            }
            Expect<ProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, 15, 481),
                        failedRecordTransport, failedRecordValidator, 16);
            }, "a short record-16 completion passed terminal gating");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferTerminalOnce(
                        LoaderTerminalRecordRequest(482),
                        failedRecordTransport, failedRecordValidator);
            }, "the terminal was accepted after record 16 failed");
            Require(failedRecordTransport.
                    LoaderWriteBufferTerminalCallCount == 0,
                "the terminal reached transport after record 16 failed");

            PrecisionTwoLoaderSequenceValidator shortTerminalValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            RawScsiResult[] shortTerminalResults =
                SuccessfulCompleteLoaderResults();
            shortTerminalResults[16] = RawScsiResult.ForDataOut(0x00, 10, 1);
            FakeTransport shortTerminalTransport =
                new FakeTransport(shortTerminalResults);
            for (int recordIndex = 0; recordIndex < 16; recordIndex++)
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferRecordOnce(
                        LoaderRecordRequest(firmware, recordIndex,
                            checked((ulong)(483 + recordIndex))),
                        shortTerminalTransport, shortTerminalValidator, 16);
            }
            Expect<ProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferTerminalOnce(
                        LoaderTerminalRecordRequest(499),
                        shortTerminalTransport, shortTerminalValidator);
            }, "a short terminal completion passed the exact policy");
            Expect<BrokerProtocolException>(delegate
            {
                ReadOnlyScsiExecutor.
                    ExecuteCapturedLoaderWriteBufferTerminalOnce(
                        LoaderTerminalRecordRequest(500),
                        shortTerminalTransport, shortTerminalValidator);
            }, "the terminal was retried after a failed completion");
            Require(shortTerminalTransport.
                    LoaderWriteBufferTerminalCallCount == 1,
                "a failed terminal was submitted more than once");
        }

        private static BrokerRequest LoaderReadBufferD8Request(ulong requestId)
        {
            return new BrokerRequest(requestId, 3, 0, 0, 0, 5, 0,
                BrokerDirection.In, 66, 60000, 32,
                ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb());
        }

        private static BrokerRequest ScannerReadyRequest(ulong requestId)
        {
            return new BrokerRequest(requestId, 3, 0, 0, 0, 5, 0,
                BrokerDirection.In, 2, 60000, 32,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb());
        }

        private static BrokerRequest FaultPixelReadBufferRequest(
            ulong requestId)
        {
            return new BrokerRequest(requestId, 3, 0, 0, 0, 5, 0,
                BrokerDirection.In, 22, 60000, 32,
                ScsiFraming.BuildPrecisionTwoFaultPixelReadBufferCdb());
        }

        private static BrokerRequest FaultPixelDataReadBufferRequest(
            ulong requestId)
        {
            return new BrokerRequest(requestId, 3, 0, 0, 0, 5, 0,
                BrokerDirection.In, 58, 60000, 32,
                ScsiFraming.BuildPrecisionTwoFaultPixelDataReadBufferCdb());
        }

        private static BrokerRequest CalibrationReadBufferRequest(
            ulong requestId)
        {
            return new BrokerRequest(requestId, 3, 0, 0, 0, 5, 0,
                BrokerDirection.In, 1024, 60000, 32,
                ScsiFraming.BuildPrecisionTwoCalibrationReadBufferCdb());
        }

        private static BrokerRequest D8Offset55ReadBufferRequest(
            ulong requestId)
        {
            return new BrokerRequest(requestId, 3, 0, 0, 0, 5, 0,
                BrokerDirection.In, 10, 60000, 32,
                ScsiFraming.BuildPrecisionTwoD8Offset55ReadBufferCdb());
        }

        private static BrokerRequest PreviewSetWindowRequest(ulong requestId,
            byte[] data)
        {
            return new BrokerRequest(requestId, 3, 0, 0, 0, 5, 0,
                BrokerDirection.Out, 84, 60000, 32,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data);
        }

        private static BrokerRequest PreviewImageReadRequest(ulong requestId,
            uint scanWidth)
        {
            uint transferLength = ScsiFraming.
                GetPrecisionTwoPreviewImageReadLength(scanWidth);
            return new BrokerRequest(requestId, 3, 0, 0, 0, 5, 0,
                BrokerDirection.In, transferLength, 60000, 32,
                ScsiFraming.BuildPrecisionTwoPreviewImageReadCdb(scanWidth));
        }

        private static byte[] PreviewSetWindowData()
        {
            byte[] data = new byte[84];
            data[7] = 0x4C;
            return data;
        }

        private static void CompleteOneOperationalInitializationCycle(
            OperationalReadOnlyGate gate, ulong requestId)
        {
            gate.RecordOperationalD8Completion(new BrokerCompletion(
                requestId, 3, BrokerTransportStatus.Success, 0, 0, 66, 0,
                new byte[0], new byte[66]));
            BrokerRequest scannerReady = ScannerReadyRequest(requestId + 1);
            Require(gate.TryConsumeScannerReady(scannerReady),
                "test cycle could not consume initial ScannerReady");
            gate.RecordScannerReadyCompletion(new BrokerCompletion(
                requestId + 1, 3, BrokerTransportStatus.Success, 0, 0, 2, 0,
                new byte[0], new byte[2]));
            Require(gate.TryConsumeFaultPixelReadBuffer(
                    FaultPixelReadBufferRequest(requestId + 2)),
                "test cycle could not consume fault-pixel header");
            gate.RecordFaultPixelReadBufferCompletion(new BrokerCompletion(
                requestId + 2, 3, BrokerTransportStatus.Success, 0, 0, 22, 0,
                new byte[0], KnownFaultPixelHeader()));
            Require(gate.TryConsumePostFaultPixelScannerReady(scannerReady),
                "test cycle could not consume post-header ScannerReady");
            gate.RecordPostFaultPixelScannerReadyCompletion(
                new BrokerCompletion(requestId + 3, 3,
                    BrokerTransportStatus.Success, 0, 0, 2, 0,
                    new byte[0], new byte[2]));
            Require(gate.TryConsumeFaultPixelDataReadBuffer(
                    FaultPixelDataReadBufferRequest(requestId + 4)),
                "test cycle could not consume fault-pixel data");
            gate.RecordFaultPixelDataReadBufferCompletion(
                new BrokerCompletion(requestId + 4, 3,
                    BrokerTransportStatus.Success, 0, 0, 58, 0,
                    new byte[0], new byte[58]));
            Require(gate.TryConsumeCalibrationReadBuffer(
                    CalibrationReadBufferRequest(requestId + 5)),
                "test cycle could not consume calibration data");
            gate.RecordCalibrationReadBufferCompletion(new BrokerCompletion(
                requestId + 5, 3, BrokerTransportStatus.Success, 0, 0,
                1024, 0, new byte[0], new byte[1024]));
            Require(gate.TryConsumePostCalibrationScannerReady(scannerReady),
                "test cycle could not consume continuation ScannerReady");
            gate.RecordPostCalibrationScannerReadyCompletion(
                new BrokerCompletion(requestId + 6, 3,
                    BrokerTransportStatus.Success, 0, 0, 2, 0,
                    new byte[0], new byte[2]));
        }

        private static byte[] KnownFaultPixelHeader()
        {
            return new byte[]
            {
                0x46, 0x61, 0x75, 0x6C, 0x20, 0x50, 0x69, 0x78,
                0x73, 0x00, 0x20, 0x20, 0x30, 0x0A, 0x20, 0x20,
                0x31, 0x0A, 0x20, 0x20, 0x30, 0x0A
            };
        }

        private static BrokerRequest LoaderWriteBufferModeOneZeroRequest(
            ulong requestId)
        {
            return new BrokerRequest(requestId, 3, 0, 0, 0, 5, 0,
                BrokerDirection.Out, 0, 60000, 32,
                ScsiFraming.
                    BuildPrecisionTwoLoaderWriteBufferModeOneZeroCdb());
        }

        private static BrokerRequest LoaderFirstRecordRequest(byte[] firmware,
            ulong requestId)
        {
            return LoaderRecordRequest(firmware, 0, requestId);
        }

        private static BrokerRequest LoaderRecordRequest(byte[] firmware,
            int recordIndex, ulong requestId)
        {
            byte[] data = new byte[
                ScsiFraming.PrecisionTwoLoaderFirstRecordLength];
            ushort address = checked((ushort)(0x3000 +
                (recordIndex * 0x100)));
            data[0] = 0x01;
            data[1] = (byte)(address >> 8);
            data[2] = (byte)(address & 0xFF);
            Buffer.BlockCopy(firmware, recordIndex * 4096, data, 10, 4096);
            return LoaderRequestFromData(data, requestId);
        }

        private static BrokerRequest LoaderTerminalRecordRequest(
            ulong requestId)
        {
            byte[] data = new byte[]
            {
                0x00, 0x30, 0x01, 0x00, 0x0E,
                0x00, 0x00, 0x00, 0x00, 0x00
            };
            return new BrokerRequest(requestId, 3, 0, 0, 0, 5, 0,
                BrokerDirection.Out, checked((uint)data.Length), 60000, 32,
                ScsiFraming.BuildPrecisionTwoLoaderTerminalRecordCdb(), data);
        }

        private static BrokerRequest LoaderRequestFromData(byte[] data,
            ulong requestId)
        {
            return new BrokerRequest(requestId, 3, 0, 0, 0, 5, 0,
                BrokerDirection.Out, checked((uint)data.Length), 60000, 32,
                ScsiFraming.BuildPrecisionTwoLoaderFirstRecordCdb(), data);
        }

        private static byte[] SyntheticPrecisionTwoFirmware()
        {
            byte[] firmware = new byte[65536];
            for (int index = 0; index < firmware.Length; index++)
            {
                firmware[index] = (byte)(((index * 17) + (index / 31)) & 0xFF);
            }
            return firmware;
        }

        private static RawScsiResult[] SuccessfulLoaderRecordResults(
            int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException("count");
            }
            RawScsiResult[] results = new RawScsiResult[count];
            for (int index = 0; index < results.Length; index++)
            {
                results[index] = RawScsiResult.ForDataOut(0x00,
                    ScsiFraming.PrecisionTwoLoaderFirstRecordLength, 0);
            }
            return results;
        }

        private static RawScsiResult[] SuccessfulCompleteLoaderResults()
        {
            RawScsiResult[] results = new RawScsiResult[17];
            RawScsiResult[] records = SuccessfulLoaderRecordResults(16);
            Array.Copy(records, results, records.Length);
            results[16] = RawScsiResult.ForDataOut(0x00, 10, 0);
            return results;
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(bytes)).
                    Replace("-", string.Empty);
            }
        }

        private static BrokerRequest InquiryRequest(byte physicalTarget)
        {
            return new BrokerRequest(1, 3, 0, 0, 0, physicalTarget, 0,
                BrokerDirection.In, 36, 5000, 18,
                ScsiFraming.BuildInquiryCdb(36));
        }

        private static byte[] KnownInquiry()
        {
            byte[] bytes = new byte[36];
            for (int i = 8; i < bytes.Length; i++)
            {
                bytes[i] = 0x20;
            }
            bytes[0] = 0x06;
            bytes[4] = 31;
            PutAscii(bytes, 8, 8, "Imacon");
            PutAscii(bytes, 16, 16, "SCSI Loader");
            PutAscii(bytes, 32, 4, "L302");
            return bytes;
        }

        private static void PutAscii(byte[] bytes, int offset, int length,
            string value)
        {
            for (int i = 0; i < length; i++)
            {
                bytes[offset + i] = 0x20;
            }
            byte[] encoded = Encoding.ASCII.GetBytes(value);
            Buffer.BlockCopy(encoded, 0, bytes, offset,
                Math.Min(encoded.Length, length));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void Expect<T>(Action action, string message)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            throw new InvalidOperationException(message);
        }

        private sealed class FakeTransport : IReadOnlyScsiTransport
        {
            private readonly Queue<RawScsiResult> results;

            internal FakeTransport(params RawScsiResult[] results)
            {
                this.results = new Queue<RawScsiResult>(results);
            }

            internal int CallCount { get; private set; }
            internal int LoaderReadBufferD8CallCount { get; private set; }
            internal int OperationalReadBufferD8CallCount { get; private set; }
            internal int OperationalScannerReadyCallCount { get; private set; }
            internal int OperationalFaultPixelReadBufferCallCount
            {
                get;
                private set;
            }
            internal int OperationalFaultPixelDataReadBufferCallCount
            {
                get;
                private set;
            }
            internal int OperationalCalibrationReadBufferCallCount
            {
                get;
                private set;
            }
            internal int OperationalD8Offset55ReadBufferCallCount
            {
                get;
                private set;
            }
            internal int OperationalPreviewSetWindowCallCount
            {
                get;
                private set;
            }
            internal int LoaderWriteBufferModeOneZeroCallCount
            {
                get;
                private set;
            }
            internal int LoaderWriteBufferRecordCallCount
            {
                get;
                private set;
            }
            internal int LoaderWriteBufferTerminalCallCount
            {
                get;
                private set;
            }

            public RawScsiResult Execute(byte target, byte lun, byte[] cdb,
                uint requestedLength, int timeoutMilliseconds)
            {
                CallCount++;
                Require(target == 5 && lun == 0,
                    "executor changed the physical address");
                Require(timeoutMilliseconds == 5000,
                    "executor changed the request timeout");
                if (results.Count == 0)
                {
                    throw new InvalidOperationException(
                        "fake transport received an unexpected call");
                }
                RawScsiResult result = results.Dequeue();
                Require(result.RequestedLength == requestedLength,
                    "executor changed the transfer length");
                return result;
            }

            public RawScsiResult ExecutePrecisionTwoLoaderReadBufferD8Once()
            {
                CallCount++;
                LoaderReadBufferD8CallCount++;
                if (results.Count == 0)
                {
                    throw new InvalidOperationException(
                        "fake transport received an unexpected D8 call");
                }
                RawScsiResult result = results.Dequeue();
                Require(result.RequestedLength == 66,
                    "D8 transport changed the transfer length");
                return result;
            }

            public RawScsiResult
                ExecutePrecisionTwoOperationalReadBufferD8Once()
            {
                CallCount++;
                OperationalReadBufferD8CallCount++;
                if (results.Count == 0)
                {
                    throw new InvalidOperationException(
                        "fake transport received an unexpected operational " +
                        "D8 call");
                }
                RawScsiResult result = results.Dequeue();
                Require(result.RequestedLength == 66,
                    "operational D8 transport changed the transfer length");
                return result;
            }

            public RawScsiResult
                ExecutePrecisionTwoOperationalScannerReadyOnce()
            {
                CallCount++;
                OperationalScannerReadyCallCount++;
                if (results.Count == 0)
                {
                    throw new InvalidOperationException(
                        "fake transport received an unexpected ScannerReady " +
                        "call");
                }
                RawScsiResult result = results.Dequeue();
                Require(result.RequestedLength == 2,
                    "ScannerReady transport changed the transfer length");
                return result;
            }

            public RawScsiResult
                ExecutePrecisionTwoOperationalFaultPixelReadBufferOnce()
            {
                CallCount++;
                OperationalFaultPixelReadBufferCallCount++;
                if (results.Count == 0)
                {
                    throw new InvalidOperationException(
                        "fake transport received an unexpected fault-pixel " +
                        "READ BUFFER call");
                }
                RawScsiResult result = results.Dequeue();
                Require(result.RequestedLength == 22,
                    "fault-pixel transport changed the transfer length");
                return result;
            }

            public RawScsiResult
                ExecutePrecisionTwoOperationalFaultPixelDataReadBufferOnce()
            {
                CallCount++;
                OperationalFaultPixelDataReadBufferCallCount++;
                if (results.Count == 0)
                {
                    throw new InvalidOperationException(
                        "fake transport received an unexpected fault-pixel " +
                        "data READ BUFFER call");
                }
                RawScsiResult result = results.Dequeue();
                Require(result.RequestedLength == 58,
                    "fault-pixel data transport changed the transfer length");
                return result;
            }

            public RawScsiResult
                ExecutePrecisionTwoOperationalCalibrationReadBufferOnce()
            {
                CallCount++;
                OperationalCalibrationReadBufferCallCount++;
                if (results.Count == 0)
                {
                    throw new InvalidOperationException(
                        "fake transport received an unexpected calibration " +
                        "READ BUFFER call");
                }
                RawScsiResult result = results.Dequeue();
                Require(result.RequestedLength == 1024,
                    "calibration transport changed the transfer length");
                return result;
            }

            public RawScsiResult
                ExecutePrecisionTwoOperationalD8Offset55ReadBufferOnce()
            {
                CallCount++;
                OperationalD8Offset55ReadBufferCallCount++;
                if (results.Count == 0)
                {
                    throw new InvalidOperationException(
                        "fake transport received an unexpected D8 offset-55 " +
                        "READ BUFFER call");
                }
                RawScsiResult result = results.Dequeue();
                Require(result.RequestedLength == 10,
                    "D8 offset-55 transport changed the transfer length");
                return result;
            }

            public RawScsiResult
                ExecutePrecisionTwoOperationalPreviewSetWindowOnce(byte[] data)
            {
                CallCount++;
                OperationalPreviewSetWindowCallCount++;
                Require(data != null && data.Length == 84 &&
                    data[7] == 0x4C,
                    "Preview SET WINDOW transport received the wrong payload");
                if (results.Count == 0)
                {
                    throw new InvalidOperationException(
                        "fake transport received an unexpected Preview SET " +
                        "WINDOW call");
                }
                RawScsiResult result = results.Dequeue();
                Require(result.RequestedLength == 84 &&
                    result.Data.Length == 0,
                    "Preview SET WINDOW transport returned an invalid " +
                    "data-out result");
                return result;
            }

            public RawScsiResult
                ExecutePrecisionTwoLoaderWriteBufferModeOneZeroOnce()
            {
                CallCount++;
                LoaderWriteBufferModeOneZeroCallCount++;
                if (results.Count == 0)
                {
                    throw new InvalidOperationException(
                        "fake transport received an unexpected WRITE BUFFER call");
                }
                RawScsiResult result = results.Dequeue();
                Require(result.RequestedLength == 0 &&
                    result.TransferLength == 0 && result.Data.Length == 0,
                    "WRITE BUFFER transport returned a data phase");
                return result;
            }

            public RawScsiResult
                ExecutePrecisionTwoLoaderWriteBufferRecordOnce(
                    byte[] record, int recordIndex)
            {
                CallCount++;
                LoaderWriteBufferRecordCallCount++;
                ushort address = checked((ushort)(0x3000 +
                    (recordIndex * 0x100)));
                Require(record != null && record.Length ==
                        ScsiFraming.PrecisionTwoLoaderFirstRecordLength &&
                    recordIndex >= 0 && recordIndex <= 15 &&
                    record[0] == 0x01 &&
                    record[1] == (byte)(address >> 8) &&
                    record[2] == (byte)(address & 0xFF),
                    "loader-record transport received the wrong payload");
                if (results.Count == 0)
                {
                    throw new InvalidOperationException(
                        "fake transport received an unexpected loader record");
                }
                RawScsiResult result = results.Dequeue();
                Require(result.RequestedLength ==
                        ScsiFraming.PrecisionTwoLoaderFirstRecordLength &&
                    result.Data.Length == 0,
                    "loader-record transport returned an invalid data-out result");
                return result;
            }

            public RawScsiResult
                ExecutePrecisionTwoLoaderWriteBufferTerminalOnce(byte[] record)
            {
                CallCount++;
                LoaderWriteBufferTerminalCallCount++;
                byte[] expected = new byte[]
                {
                    0x00, 0x30, 0x01, 0x00, 0x0E,
                    0x00, 0x00, 0x00, 0x00, 0x00
                };
                Require(record != null && record.Length == expected.Length &&
                    Sha256Hex(record) == Sha256Hex(expected),
                    "loader-terminal transport received the wrong payload");
                if (results.Count == 0)
                {
                    throw new InvalidOperationException(
                        "fake transport received an unexpected loader terminal");
                }
                RawScsiResult result = results.Dequeue();
                Require(result.RequestedLength == 10 &&
                    result.Data.Length == 0,
                    "loader-terminal transport returned an invalid data-out result");
                return result;
            }
        }
    }
}
