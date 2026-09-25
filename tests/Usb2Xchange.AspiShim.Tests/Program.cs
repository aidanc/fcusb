// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Usb2Xchange.AspiShim;
using Usb2Xchange.FlexColorAspiLauncher;
using Usb2Xchange.Protocol;
using Usb2Xchange.WinUsb;

namespace Usb2Xchange.AspiShim.Tests
{
    internal static class Program
    {
        private static int passed;
        private static int failed;

        private static int Main()
        {
            Environment.SetEnvironmentVariable(
                "USB2XCHANGE_ASPI_TRANSPORT", "offline-replay",
                EnvironmentVariableTarget.Process);
            Run("managed support result advertises one adapter", TestSupportInfo);
            Run("native PE32 exports are callable", TestNativeExports);
            Run("offline replay exposes only synthetic target 5",
                TestOfflineReplay);
            Run("offline replay streams observed Preview image rows",
                TestOfflineOperationalReplay);
            Run("offline replay permits one bounded repeat scan",
                TestOfflineRepeatScan);
            Run("offline full-scan log and TIFF output are exact",
                TestOfflineFullScanOutput);
            Run("offline operator log requires two exact transactions",
                TestOfflineOperatorRepeatLog);
            Run("live operator log requires cold then exact warm completion",
                TestLiveOperatorRepeatLog);
            Run("FlexColor progress observations validate range and percent",
                TestFlexColorProgressObservation);
            Run("full-scan startup fingerprint log is exclusive and exact",
                TestFullScanStartupFingerprintLog);
            Run("offline Preview replay fails closed on state and row drift",
                TestOfflinePreviewSequenceFailures);
            Run("offline replay identifier is exact and validated",
                TestOfflineReplayIdentifierValidation);
            Run("host-adapter inquiry is populated", TestHostAdapterInquiry);
            Run("device type is obtained through INQUIRY", TestGetDeviceType);
            Run("absent target maps to no device", TestAbsentTarget);
            Run("INQUIRY copies returned bytes", TestInquiry);
            Run("TEST UNIT READY permits zero data", TestTestUnitReady);
            Run("exact operational discovery reads are accepted",
                TestOperationalDiscoveryReads);
            Run("extended operational read SRBs are exact and mode gated",
                TestExtendedOperationalReadSrbRouting);
            Run("offline Preview rows have a separate bounded SRB envelope",
                TestOfflinePreviewReadSrbRouting);
            Run("live discovery gate is identity and sequence bounded",
                TestReadOnlyDiscoveryGate);
            Run("operational sequence gate accepts repeated exact cycles",
                TestOperationalSequenceExact);
            Run("live Preview gate bounds both post-window readiness phases",
                TestLivePreviewSuccessorGate);
            Run("live Preview readiness permits carrier-motion timing",
                TestLivePreviewReadinessTiming);
            Run("live Preview burst permits exactly eight ordered rows",
                TestLivePreviewBurstGate);
            Run("live Preview stream bounds rows and direct DF polls",
                TestLivePreviewStreamGate);
            Run("live Preview short retry advances only full logical rows",
                TestLivePreviewShortRetryGate);
            Run("live Preview short retry fails closed at every bound",
                TestLivePreviewShortRetryFailures);
            Run("natural Preview boundary permits exactly 996 full rows",
                TestLivePreviewNaturalBoundaryGate);
            Run("natural per-row retry policy exceeds the old total safely",
                TestLivePreviewNaturalPerRowBoundaryGate);
            Run("powered natural policy permits exactly 911 full rows",
                TestLivePreviewPoweredNaturalBoundaryGate);
            Run("full-scan observer re-arms only after one complete Preview",
                TestLiveFullScanSuccessorGate);
            Run("full-scan terminal probe is one-shot and quarantined",
                TestLiveFullScanTerminalProbeGate);
            Run("full-scan INQUIRY revalidation is exact and bounded",
                TestLiveFullScanInquiryGate);
            Run("WinUSB cleanup bounds completion and cancellation separately",
                TestWinUsbPoweredPreviewCleanupProgress);
            Run("Preview terminal log reflects cleanup authority",
                TestPreviewStreamCompletedMessage);
            Run("WinUSB readiness permits consumed short-retry submissions",
                TestWinUsbShortRetryScannerReadyProgress);
            Run("operational sequence gate fails closed on anomalies",
                TestOperationalSequenceFailures);
            Run("loader data-out requires an explicit processor mode",
                TestLoaderDataOutDisabled);
            Run("loader SRBs route only exact data-out envelopes",
                TestLoaderDataOutSrbRouting);
            Run("Preview SET WINDOW SRB is mode-bounded before transport",
                TestPreviewSetWindowSrbRouting);
            Run("startup WRITE fingerprint never reaches data-out transport",
                TestStartupWriteFingerprintSrbRouting);
            Run("WinUSB Preview manifest is bound before device discovery",
                TestWinUsbPreviewManifestBinding);
            Run("loader sequence gate accepts one complete ordered fixture",
                TestLoaderSequenceExact);
            Run("loader sequence gate fails closed on every anomaly",
                TestLoaderSequenceFailures);
            Run("live state-changing runtimes require distinct exact approvals",
                TestLiveStateChangingRuntimeApprovals);
            Run("live operator session requires its distinct exact approval",
                TestLiveOperatorSessionRuntimeApproval);
            Run("full-scan observer requires its distinct exact approval",
                TestLiveFullScanRuntimeApproval);
            Run("write and vendor commands are blocked", TestBlockedCommands);
            Run("trusted FlexColor identity gate is exact",
                TestTrustedFlexColorIdentityGate);
            Run("trusted pass-through runtime needs token and process trust",
                TestTrustedPassThroughRuntimeApproval);
            Run("trusted pass-through forwards arbitrary CDB directions",
                TestTrustedPassThroughSrbRouting);
            Run("trusted pass-through rejects malformed SRBs",
                TestTrustedPassThroughMalformedSrbs);
            Run("trusted pass-through maps short completion safely",
                TestTrustedPassThroughShortCompletion);
            Run("trusted pass-through serializes complete SRBs",
                TestTrustedPassThroughSerialization);
            Run("trusted pass-through preserves CHECK CONDITION sense",
                TestTrustedPassThroughCheckCondition);
            Run("selection timeout maps to ASPI status", TestSelectionTimeout);
            Run("check condition obtains sense data", TestCheckCondition);
            Run("target BUSY never copies short transport data",
                TestBusyDoesNotCopyData);
            Run("event-notify completion is signaled", TestEventNotification);
            Run("failed execution logs exact request metadata",
                TestFailureLogMetadata);
            Run("Preview observer separates initial success from cleanup rejection",
                TestPreviewObserveLogClassification);
            Run("Preview first-read observer requires an exact completion",
                TestPreviewFirstReadLogClassification);
            Run("Preview burst observer requires eight exact ordered rows",
                TestPreviewBurstLogClassification);
            Run("Preview stream observer requires rows and exact DF polls",
                TestPreviewStreamLogClassification);
            Run("Preview short-retry observer validates rows and retries",
                TestPreviewShortRetryLogClassification);
            Run("natural Preview observer validates its distinct limits",
                TestPreviewNaturalLogClassification);
            Run("powered natural observer validates the 911-row boundary",
                TestPreviewPoweredNaturalLogClassification);
            Run("powered cleanup observer requires two exact completions",
                TestPreviewPoweredCleanupLogClassification);
            Run("operator session rotates only after exact cycle completion",
                TestOperatorSessionCycleRotation);
            Run("operator session failures remain terminal",
                TestOperatorSessionFailureIsTerminal);
            Run("operator session factory failures remain terminal",
                TestOperatorSessionFactoryFailureIsTerminal);
            Run("operator cycle combines only proven completion policies",
                TestOperatorCycleConfiguration);
            Run("warm operator INQUIRY authority is exact and counter bounded",
                TestWarmOperatorInquiryGate);
            Run("live warm operator gate reserves exactly three cycles",
                TestLiveWarmOperatorGate);
            Run("offline operator runtime requires its distinct approval",
                TestOfflineOperatorRuntimeApproval);
            Run("offline warm operator prefix fails closed on cold commands",
                TestOfflineWarmOperatorPrefixGuards);
            Run("operator session repeats complete offline transactions",
                TestOfflineOperatorSessionRepeat);

            Console.WriteLine("Passed: {0}; Failed: {1}", passed, failed);
            return failed == 0 ? 0 : 1;
        }

        private static void TestSupportInfo()
        {
            Equal((uint)0x0101, AspiExports.GetASPI32SupportInfo());
        }

        private static void TestFlexColorProgressObservation()
        {
            var progress = new FlexColorProgressObservation(true, true,
                10, 210, 110, "test");
            Equal(50, progress.Percent);
            True(!progress.AtMaximum);
            var complete = new FlexColorProgressObservation(true, true,
                0, 100, 100, "test");
            Equal(100, complete.Percent);
            True(complete.AtMaximum);
            var absent = new FlexColorProgressObservation(false, false,
                0, 0, 0, string.Empty);
            Equal(-1, absent.Percent);
            bool rejected = false;
            try
            {
                new FlexColorProgressObservation(true, true, 0, 100, 101,
                    "test");
            }
            catch (ArgumentOutOfRangeException)
            {
                rejected = true;
            }
            True(rejected);
        }

        private static void TestLiveOperatorRepeatLog()
        {
            const string previewOne =
                "LIVE ASPI exact post-image cleanup SET WINDOW completed: " +
                "ordinal=1/2, status=0x00, actual=84, residue=0, " +
                "payload-sha256=" +
                "3092FA15361A422D558EF968164EE6CB00B854413630CC0C07A4F62877515A9D.";
            const string previewTwo =
                "LIVE ASPI exact post-image cleanup SET WINDOW completed: " +
                "ordinal=2/2, status=0x00, actual=84, residue=0, " +
                "payload-sha256=" +
                "3092FA15361A422D558EF968164EE6CB00B854413630CC0C07A4F62877515A9D.";
            const string fullOne =
                "LIVE ASPI exact full-scan cleanup SET WINDOW completed: " +
                "ordinal=1/2, status=0x00, actual=84, residue=0, " +
                "payload-sha256=" +
                "23B3C62B62096A50A58E6BC035D20C6C04E38F5D10A30BF27C6348D0C37EFB15.";
            const string fullTwo =
                "LIVE ASPI exact full-scan cleanup SET WINDOW completed: " +
                "ordinal=2/2, status=0x00, actual=84, residue=0, " +
                "payload-sha256=" +
                "23B3C62B62096A50A58E6BC035D20C6C04E38F5D10A30BF27C6348D0C37EFB15.";
            const string stream =
                "LIVE ASPI exact bounded full-scan image stream completed: " +
                "rows=998/998, submissions=1123/8982, " +
                "short-retries=125/7984.";
            const string complete =
                "LIVE ASPI exact full-scan transport completed after 998 " +
                "rows and two cleanup SET WINDOW completions.";
            string cold = previewOne + "\n" + previewTwo + "\n" + stream +
                "\n" + fullOne + "\n" + fullTwo + "\n" + complete;
            string warm =
                "LIVE ASPI warm operator cycle seeded; one exact prefix.\n" +
                "LIVE ASPI warm operator exact operational INQUIRY prefix " +
                "completed; DF follows.\n" +
                "LIVE ASPI warm operator Preview initialization armed " +
                "after exact M333 INQUIRY; DF active.\n" +
                "LIVE ASPI warm operator stabilization M333 INQUIRY " +
                "completed: ordinal=1/2, counter=3, status=0x00, " +
                "actual=96, residue=0.\n" +
                "LIVE ASPI warm operator stabilization M333 INQUIRY " +
                "completed: ordinal=2/2, counter=4, status=0x00, " +
                "actual=96, residue=0.\n" + cold;
            LiveOperatorRepeatLogMetadata exact =
                LiveOperatorRepeatLogInspector.Inspect(cold + "\n" + warm,
                    2);
            Equal(2, exact.RowCounts.Length);
            Equal(998, exact.RowCounts[0]);
            Equal(998, exact.RowCounts[1]);
            ExpectInvalid(delegate
            {
                LiveOperatorRepeatLogInspector.Inspect(cold + "\n" + cold,
                    2);
            });
            ExpectInvalid(delegate
            {
                LiveOperatorRepeatLogInspector.Inspect(cold + "\n" + warm +
                    "\nUnhandled ASPI command failure: synthetic", 2);
            });
            ExpectInvalid(delegate
            {
                LiveOperatorRepeatLogInspector.Inspect(cold + "\n" + warm +
                    "\nEXEC_SCSI_CMD failed [synthetic]", 2);
            });
            ExpectInvalid(delegate
            {
                string invalidAccounting = warm.Replace(
                    "submissions=1123/8982, short-retries=125/7984",
                    "submissions=1124/8982, short-retries=125/7984");
                LiveOperatorRepeatLogInspector.Inspect(cold + "\n" +
                    invalidAccounting, 2);
            });
            ExpectInvalid(delegate
            {
                string wrongOrder = warm.Replace(fullOne + "\n" + fullTwo,
                    fullTwo + "\n" + fullOne);
                LiveOperatorRepeatLogInspector.Inspect(cold + "\n" +
                    wrongOrder, 2);
            });
        }

        private static void TestNativeExports()
        {
            Equal((uint)0x0101, NativeGetSupportInfo());
        }

        private static void TestOfflineReplay()
        {
            OfflineReplayAspiTransport replay =
                new OfflineReplayAspiTransport();
            AspiTransportResult absent = replay.Execute(4, 0,
                new byte[] { 0x12, 0, 0, 0, 36, 0 }, 36);
            Equal((byte)AdapterStatus.SelectionTimeout,
                absent.AdapterStatus);

            AspiTransportResult present = replay.Execute(5, 0,
                new byte[] { 0x12, 0, 0, 0, 96, 0 }, 96);
            Equal((byte)AdapterStatus.Success, present.AdapterStatus);
            Equal(96, present.Data.Length);
            Equal((byte)0x06, present.Data[0]);
            Equal("Imacon", ReadAscii(present.Data, 8, 8));
            Equal("FlexTight II", ReadAscii(present.Data, 16, 16));
            Equal("M333", ReadAscii(present.Data, 32, 4));
        }

        private static void TestOfflineOperationalReplay()
        {
            byte[] setWindow = PreviewSetWindowData();
            string scannerIdentifier = "FP12345678";
            OfflineReplayAspiTransport replay =
                new OfflineReplayAspiTransport(null,
                    Sha256Hex(setWindow), scannerIdentifier);
            AspiTransportResult d8 = replay.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb(), 66);
            Equal(66, d8.Data.Length);
            Equal((byte)AdapterStatus.Success, d8.AdapterStatus);
            Equal((byte)'F', d8.Data[55]);
            Equal((byte)'P', d8.Data[56]);
            Equal(scannerIdentifier, ReadAscii(d8.Data, 55, 10));
            replay.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            byte[] startupWrite = new byte[
                ScsiFraming.PrecisionTwoStartupWriteBuffer1082Length];
            for (int index = 10; index < startupWrite.Length; ++index)
            {
                startupWrite[index] = checked((byte)(index & 0xFF));
            }
            for (int cycle = 0; cycle < 6; ++cycle)
            {
                ReplayInitializationCycle(replay, 2);
                replay.Execute(5, 0,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
                replay.ExecuteDataOut(5, 0,
                    ScsiFraming.BuildPrecisionTwoStartupWriteBuffer1082Cdb(),
                    startupWrite);
                AspiTransportResult verification = replay.Execute(5, 0,
                    ScsiFraming.BuildPrecisionTwoStartupReadBufferE01072Cdb(),
                    ScsiFraming.PrecisionTwoStartupReadBufferE01072Length);
                Equal(1072, verification.Data.Length);
                for (int index = 0; index < verification.Data.Length; ++index)
                {
                    Equal(startupWrite[index + 10], verification.Data[index]);
                }
                AspiTransportResult identifier = replay.Execute(5, 0,
                    ScsiFraming.BuildPrecisionTwoD8Offset55ReadBufferCdb(),
                    ScsiFraming.PrecisionTwoD8Offset55Length);
                Equal(10, identifier.Data.Length);
                for (int index = 0; index < identifier.Data.Length; ++index)
                {
                    Equal(d8.Data[55 + index], identifier.Data[index]);
                }
                AspiTransportResult cachedIdentifier = replay.Execute(5, 0,
                    ScsiFraming.BuildPrecisionTwoD8Offset55ReadBufferCdb(),
                    ScsiFraming.PrecisionTwoD8Offset55Length);
                for (int index = 0;
                    index < cachedIdentifier.Data.Length; ++index)
                {
                    Equal(identifier.Data[index],
                        cachedIdentifier.Data[index]);
                }
                replay.Execute(5, 0,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            }
            AspiTransportResult setWindowResult = replay.ExecuteDataOut(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow);
            Equal((byte)AdapterStatus.Success,
                setWindowResult.AdapterStatus);
            replay.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            replay.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            byte[] imageRead =
                ScsiFraming.BuildPrecisionTwoPreviewImageReadCdb(640);
            imageRead[4] = 0x28;
            AspiTransportResult firstRow = replay.Execute(5, 0, imageRead,
                3840);
            Equal((byte)AdapterStatus.Success, firstRow.AdapterStatus);
            Equal(3840, firstRow.Data.Length);
            AspiTransportResult secondRow = replay.Execute(5, 0, imageRead,
                3840);
            Equal(3840, secondRow.Data.Length);
            True(firstRow.Data[2] != secondRow.Data[2] ||
                firstRow.Data[3] != secondRow.Data[3]);
            replay.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            Equal(3840, replay.Execute(5, 0, imageRead, 3840).Data.Length);
            replay.ExecuteDataOut(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow);
            replay.ExecuteDataOut(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow);
            ExpectInvalid(delegate
            {
                replay.ExecuteDataOut(5, 0,
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                    setWindow);
            });
        }

        private static void TestOfflineReplayIdentifierValidation()
        {
            string setWindowHash = Sha256Hex(PreviewSetWindowData());
            ExpectArgument(delegate
            {
                new OfflineReplayAspiTransport(null, setWindowHash,
                    "FP1234567");
            });
            ExpectArgument(delegate
            {
                new OfflineReplayAspiTransport(null, setWindowHash,
                    "XX12345678");
            });
            ExpectArgument(delegate
            {
                new OfflineReplayAspiTransport(null, setWindowHash,
                    "FP1234567a");
            });
        }

        private static void TestOfflineRepeatScan()
        {
            byte[] setWindow = PreviewSetWindowData();
            OfflineReplayAspiTransport replay =
                new OfflineReplayAspiTransport(null, Sha256Hex(setWindow),
                    "FP12345678");
            replay.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb(), 66);
            replay.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);

            for (int cycle = 0; cycle < 5; ++cycle)
            {
                ReplayInitializationCycle(replay, 2);
            }

            CompleteOfflineImageStream(replay, setWindow, 640, 2);
            replay.Execute(5, 0, new byte[] { 0x12, 0, 0, 0, 96, 0 }, 96);
            replay.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            ReplayInitializationCycle(replay, 2);
            CompleteOfflineImageStream(replay, setWindow, 641, 1);

            replay.Execute(5, 0, new byte[] { 0x12, 0, 0, 0, 96, 0 }, 96);
            ExpectInvalid(delegate
            {
                replay.Execute(5, 0,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            });
        }

        private static void CompleteOfflineImageStream(
            IAspiDataOutTransport replay, byte[] setWindow,
            uint width, int rowCount)
        {
            replay.ExecuteDataOut(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), setWindow);
            replay.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            replay.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            byte[] read = ScsiFraming.
                BuildPrecisionTwoPreviewImageReadCdb(width);
            for (int row = 0; row < rowCount; ++row)
            {
                Equal(checked((int)(width * 6)), replay.Execute(5, 0, read,
                    width * 6).Data.Length);
            }
            replay.ExecuteDataOut(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), setWindow);
            replay.ExecuteDataOut(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), setWindow);
        }

        private static void TestOfflineFullScanOutput()
        {
            const string hash =
                "E8F5D0217E816D7F17E43A17573A466AA863873B18D6BC0E4660D2FECF0B0485";
            var log = new StringBuilder();
            log.AppendLine("completed two post-image SET WINDOW cleanup " +
                "requests without a WinUSB call; streams=1.");
            log.AppendLine("OFFLINE REPLAY armed exactly one repeat scan " +
                "after the completed Preview and exact INQUIRY prefix.");
            log.AppendLine("cycles=6, next=PreviewSetWindowReady");
            log.AppendLine("Preview SET WINDOW candidate validated for " +
                "transport; payload-sha256=" + hash);
            for (int row = 0; row < 996; ++row)
            {
                log.AppendLine("length=4488, CDB=28 00 00 00 28 00 00 11 " +
                    "88 00");
            }
            log.AppendLine("completed two post-image SET WINDOW cleanup " +
                "requests without a WinUSB call; streams=2.");
            OfflineFullScanLogMetadata metadata =
                OfflineFullScanLogInspector.Inspect(log.ToString());
            Equal(996, metadata.RowCount);
            Equal(hash, metadata.SetWindowSha256);

            string exactLog = log.ToString();
            const string rowLine =
                "length=4488, CDB=28 00 00 00 28 00 00 11 88 00";
            int firstRow = exactLog.IndexOf(rowLine,
                StringComparison.Ordinal);
            string wrongRows = exactLog.Remove(firstRow,
                rowLine.Length + Environment.NewLine.Length);
            ExpectInvalid(delegate
            {
                OfflineFullScanLogInspector.Inspect(wrongRows);
            });

            byte[] tiff = BuildExactTestTiff(4, 3, 300);
            TiffOutputMetadata output = TiffOutputInspector.Inspect(tiff,
                4, 3, 300);
            Equal((uint)4, output.Width);
            Equal((uint)3, output.Height);
            Equal((uint)300, output.Ppi);
            Equal((uint)36, output.PixelBytes);
            tiff[67] = 5;
            ExpectInvalid(delegate
            {
                TiffOutputInspector.Inspect(tiff, 4, 3, 300);
            });
        }

        private static void TestOfflineOperatorRepeatLog()
        {
            const string hash =
                "E8F5D0217E816D7F17E43A17573A466AA863873B18D6BC0E4660D2FECF0B0485";
            var log = new StringBuilder();
            for (int transaction = 0; transaction < 2; ++transaction)
            {
                if (transaction != 0)
                {
                    log.AppendLine("OFFLINE REPLAY seeded one bounded warm " +
                        "operator cycle with exactly three remaining " +
                        "bounded initialization and image-stream phases; " +
                        "no WinUSB call.");
                    log.AppendLine("OFFLINE REPLAY armed exactly one repeat " +
                        "scan after the completed Preview and exact " +
                        "INQUIRY prefix.");
                    log.AppendLine(
                        "cycles=4, next=PreviewSetWindowReady");
                    log.AppendLine(
                        "cycles=5, next=PreviewSetWindowReady");
                }
                log.AppendLine("completed two post-image SET WINDOW cleanup " +
                    "requests without a WinUSB call; streams=1.");
                log.AppendLine("OFFLINE REPLAY armed exactly one repeat " +
                    "scan after the completed Preview and exact INQUIRY " +
                    "prefix.");
                log.AppendLine("cycles=6, next=PreviewSetWindowReady");
                log.AppendLine("Preview SET WINDOW candidate validated for " +
                    "transport; payload-sha256=" + hash);
                for (int row = 0; row < 996; ++row)
                {
                    log.AppendLine("length=4488, CDB=28 00 00 00 28 00 00 " +
                        "11 88 00");
                }
                log.AppendLine("completed two post-image SET WINDOW cleanup " +
                    "requests without a WinUSB call; streams=2.");
            }

            OfflineOperatorRepeatLogMetadata metadata =
                OfflineOperatorRepeatLogInspector.Inspect(log.ToString(), 2);
            Equal(2, metadata.RowCounts.Length);
            Equal(996, metadata.RowCounts[0]);
            Equal(996, metadata.RowCounts[1]);
            Equal(hash, metadata.SetWindowSha256[0]);
            Equal(hash, metadata.SetWindowSha256[1]);

            const string rowLine =
                "length=4488, CDB=28 00 00 00 28 00 00 11 88 00";
            string wrongRows = log.ToString();
            int secondTransaction = wrongRows.IndexOf(
                "completed two post-image SET WINDOW cleanup requests " +
                "without a WinUSB call; streams=1.",
                wrongRows.IndexOf("streams=2.",
                    StringComparison.Ordinal) + 10,
                StringComparison.Ordinal);
            int removedRow = wrongRows.IndexOf(rowLine, secondTransaction,
                StringComparison.Ordinal);
            wrongRows = wrongRows.Remove(removedRow,
                rowLine.Length + Environment.NewLine.Length);
            ExpectInvalid(delegate
            {
                OfflineOperatorRepeatLogInspector.Inspect(wrongRows, 2);
            });

            ExpectInvalid(delegate
            {
                OfflineOperatorRepeatLogInspector.Inspect(
                    log.ToString() + log.ToString(), 2);
            });
            ExpectInvalid(delegate
            {
                OfflineOperatorRepeatLogInspector.Inspect(
                    "OFFLINE REPLAY armed exactly one repeat scan after " +
                    "the completed Preview and exact INQUIRY prefix.\r\n" +
                    log.ToString(), 2);
            });
        }

        private static byte[] BuildExactTestTiff(ushort width,
            ushort height, uint ppi)
        {
            const int entryCount = 13;
            const int bitsOffset = 170;
            const int xResolutionOffset = 176;
            const int yResolutionOffset = 184;
            const int pixelOffset = 192;
            int pixelBytes = checked(width * height * 3);
            byte[] data = new byte[pixelOffset + pixelBytes];
            data[0] = (byte)'M';
            data[1] = (byte)'M';
            WriteUInt16(data, 2, 42);
            WriteUInt32(data, 4, 8);
            WriteUInt16(data, 8, entryCount);
            int entry = 10;
            WriteTiffLongEntry(data, ref entry, 0x00FE, 0);
            WriteTiffShortEntry(data, ref entry, 0x0100, width);
            WriteTiffShortEntry(data, ref entry, 0x0101, height);
            WriteTiffOffsetEntry(data, ref entry, 0x0102, 3, bitsOffset);
            WriteTiffShortEntry(data, ref entry, 0x0103, 1);
            WriteTiffShortEntry(data, ref entry, 0x0106, 2);
            WriteTiffLongEntry(data, ref entry, 0x0111, pixelOffset);
            WriteTiffShortEntry(data, ref entry, 0x0115, 3);
            WriteTiffLongEntry(data, ref entry, 0x0116, height);
            WriteTiffLongEntry(data, ref entry, 0x0117,
                checked((uint)pixelBytes));
            WriteTiffOffsetEntry(data, ref entry, 0x011A, 1,
                xResolutionOffset, 5);
            WriteTiffOffsetEntry(data, ref entry, 0x011B, 1,
                yResolutionOffset, 5);
            WriteTiffShortEntry(data, ref entry, 0x0128, 2);
            WriteUInt16(data, bitsOffset, 8);
            WriteUInt16(data, bitsOffset + 2, 8);
            WriteUInt16(data, bitsOffset + 4, 8);
            WriteUInt32(data, xResolutionOffset, ppi);
            WriteUInt32(data, xResolutionOffset + 4, 1);
            WriteUInt32(data, yResolutionOffset, ppi);
            WriteUInt32(data, yResolutionOffset + 4, 1);
            return data;
        }

        private static void WriteTiffShortEntry(byte[] data, ref int offset,
            ushort tag, ushort value)
        {
            WriteUInt16(data, offset, tag);
            WriteUInt16(data, offset + 2, 3);
            WriteUInt32(data, offset + 4, 1);
            WriteUInt16(data, offset + 8, value);
            offset += 12;
        }

        private static void WriteTiffLongEntry(byte[] data, ref int offset,
            ushort tag, uint value)
        {
            WriteUInt16(data, offset, tag);
            WriteUInt16(data, offset + 2, 4);
            WriteUInt32(data, offset + 4, 1);
            WriteUInt32(data, offset + 8, value);
            offset += 12;
        }

        private static void WriteTiffOffsetEntry(byte[] data, ref int offset,
            ushort tag, uint count, uint valueOffset, ushort type = 3)
        {
            WriteUInt16(data, offset, tag);
            WriteUInt16(data, offset + 2, type);
            WriteUInt32(data, offset + 4, count);
            WriteUInt32(data, offset + 8, valueOffset);
            offset += 12;
        }

        private static void WriteUInt16(byte[] data, int offset,
            int value)
        {
            data[offset] = checked((byte)((value >> 8) & 0xFF));
            data[offset + 1] = checked((byte)(value & 0xFF));
        }

        private static void WriteUInt32(byte[] data, int offset,
            uint value)
        {
            data[offset] = checked((byte)((value >> 24) & 0xFF));
            data[offset + 1] = checked((byte)((value >> 16) & 0xFF));
            data[offset + 2] = checked((byte)((value >> 8) & 0xFF));
            data[offset + 3] = checked((byte)(value & 0xFF));
        }

        private static void TestFullScanStartupFingerprintLog()
        {
            const string hash =
                "0123456789ABCDEF0123456789ABCDEF" +
                "0123456789ABCDEF0123456789ABCDEF";
            var text = new StringBuilder();
            text.AppendLine("LIVE ASPI full-scan successor observer armed " +
                "after two exact Preview cleanup completions; retained.");
            text.AppendLine("LIVE ASPI full-scan successor observed its " +
                "exact operational INQUIRY prefix.");
            text.AppendLine("LIVE ASPI full-scan initial ScannerReady " +
                "response: attempt=1/256, phase-elapsed-ms=0/60000, " +
                "status=0x00, actual=2, residue=0, first=0x08, " +
                "second=0x00, ready=False.");
            text.AppendLine("LIVE ASPI full-scan initial ScannerReady " +
                "response: attempt=2/256, phase-elapsed-ms=255/60000, " +
                "status=0x00, actual=2, residue=0, first=0x00, " +
                "second=0x00, ready=True.");
            text.AppendLine("LIVE ASPI full-scan extra ScannerReady " +
                "response: attempt=1/256, elapsed-ms=0/60000, " +
                "first=0x08, second=0x00, ready=False.");
            text.AppendLine("LIVE ASPI full-scan extra ScannerReady " +
                "response: attempt=2/256, elapsed-ms=255/60000, " +
                "first=0x00, second=0x00, ready=True.");
            text.AppendLine("Blocked ASPI SRB: startup WRITE BUFFER " +
                "fingerprint captured and blocked before USB; " +
                "payload-sha256=" + hash + ".");
            FullScanStartupFingerprintLogMetadata observed =
                FullScanStartupFingerprintLogInspector.Inspect(
                    text.ToString());
            True(observed.Armed);
            True(observed.InquiryObserved);
            Equal(2, observed.InitialScannerReadyAttempts);
            Equal(1, observed.InitialScannerReadyNotReadyCount);
            True(observed.InitialScannerReadyExact);
            True(observed.InitialScannerReadyCompleted);
            Equal(2, observed.ExtraScannerReadyAttempts);
            Equal(1, observed.ExtraScannerReadyNotReadyCount);
            True(observed.ExtraScannerReadyExact);
            True(observed.ExtraScannerReadyCompleted);
            Equal(hash, observed.StartupWriteSha256);
            True(!observed.SetWindowObserved);
            True(observed.HasExclusiveTerminalFingerprint);
            True(!observed.FailureBeforeTerminalFingerprint);

            var executionText = new StringBuilder();
            executionText.AppendLine(
                "LIVE ASPI full-scan successor observer armed after two " +
                "exact Preview cleanup completions; retained.");
            executionText.AppendLine(
                "LIVE ASPI full-scan successor observed its exact " +
                "operational INQUIRY prefix.");
            executionText.AppendLine(
                "LIVE ASPI full-scan initial ScannerReady response: " +
                "attempt=1/256, phase-elapsed-ms=0/60000, status=0x00, " +
                "actual=2, residue=0, first=0x00, second=0x00, " +
                "ready=True.");
            executionText.AppendLine(
                "LIVE ASPI full-scan SET WINDOW completed once: " +
                "status=0x00, actual=84, residue=0, payload-sha256=" +
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanSetWindowSha256 + ".");
            executionText.AppendLine(
                "LIVE ASPI full-scan first image READ fingerprint captured " +
                "and blocked before USB: width=777, length=4662, " +
                "selector=0x28, CDB=28 00 00 00 28 00 00 12 36 00.");
            executionText.AppendLine(
                "EXEC_SCSI_CMD failed after first image fingerprint");
            FullScanStartupFingerprintLogMetadata execution =
                FullScanStartupFingerprintLogInspector.Inspect(
                    executionText.ToString());
            True(execution.FullScanSetWindowCompleted);
            Equal(PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanSetWindowSha256,
                execution.CompletedSetWindowSha256);
            True(execution.FirstImageReadObserved);
            Equal(777, execution.FirstImageWidth);
            Equal(4662, execution.FirstImageLength);
            Equal(0x28, execution.FirstImageSelector);
            Equal("28 00 00 00 28 00 00 12 36 00",
                execution.FirstImageCdb);
            True(!execution.FailureBeforeTerminalFingerprint);

            string completionText =
                "LIVE ASPI full-scan in-stream operational INQUIRY " +
                "revalidation completed: ordinal=1/26, " +
                "after-rows=237/762, status=0x00, actual=96, residue=0, " +
                "identity=M333.\r\n" +
                "LIVE ASPI full-scan in-stream operational INQUIRY " +
                "revalidation completed: ordinal=2/26, " +
                "after-rows=267/762, status=0x00, actual=96, residue=0, " +
                "identity=M333.\r\n" +
                "LIVE ASPI exact bounded full-scan image stream completed: " +
                "rows=762/762, submissions=2290/6858, " +
                "short-retries=1528/6096.\r\n" +
                "LIVE ASPI exact full-scan cleanup SET WINDOW completed: " +
                "ordinal=1/2, status=0x00, actual=84, residue=0, " +
                "payload-sha256=" + PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanCleanupSetWindowSha256 + ".\r\n" +
                "LIVE ASPI exact full-scan cleanup SET WINDOW completed: " +
                "ordinal=2/2, status=0x00, actual=84, residue=0, " +
                "payload-sha256=" + PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanCleanupSetWindowSha256 + ".\r\n" +
                "LIVE ASPI exact full-scan transport completed after 762 " +
                "rows and two cleanup SET WINDOW completions.\r\n";
            FullScanCompletionLogMetadata completion =
                FullScanCompletionLogInspector.Inspect(completionText);
            Equal(762, completion.Rows);
            Equal(762, completion.RowLimit);
            Equal(2290, completion.Submissions);
            Equal(1528, completion.ShortRetries);
            True(completion.FirstCleanup && completion.SecondCleanup);
            True(completion.CleanupExact);
            Equal(2, completion.InStreamInquiryRevalidations);
            True(completion.InStreamInquiryRevalidationsExact);
            True(completion.TransportCompleted);
            True(!completion.FailureBeforeCompletion);
            FullScanCompletionLogMetadata wrongRevalidation =
                FullScanCompletionLogInspector.Inspect(
                    completionText.Replace("ordinal=2/26",
                        "ordinal=3/26"));
            True(!wrongRevalidation.InStreamInquiryRevalidationsExact);

            string naturalCompletionText = completionText.
                Replace("ordinal=1/26", "ordinal=1/34").
                Replace("ordinal=2/26", "ordinal=2/34").
                Replace("after-rows=237/762", "after-rows=237/996").
                Replace("after-rows=267/762", "after-rows=267/996").
                Replace("rows=762/762, submissions=2290/6858, " +
                    "short-retries=1528/6096",
                    "rows=996/996, submissions=2390/8964, " +
                    "short-retries=1394/7968").
                Replace("completed after 762 rows",
                    "completed after 996 rows");
            FullScanCompletionLogMetadata naturalCompletion =
                FullScanCompletionLogInspector.Inspect(
                    naturalCompletionText, true);
            Equal(996, naturalCompletion.Rows);
            Equal(8964, naturalCompletion.MaximumSubmissions);
            Equal(7968, naturalCompletion.MaximumShortRetries);
            True(naturalCompletion.InStreamInquiryRevalidationsExact);
            True(naturalCompletion.TransportCompleted);
            True(!FullScanCompletionLogInspector.Inspect(
                naturalCompletionText).TransportCompleted);
            string row997CompletionText = naturalCompletionText.
                Replace("after-rows=237/996", "after-rows=237/997").
                Replace("after-rows=267/996", "after-rows=267/997").
                Replace("rows=996/996, submissions=2390/8964, " +
                    "short-retries=1394/7968",
                    "rows=997/997, submissions=2391/8973, " +
                    "short-retries=1394/7976").
                Replace("completed after 996 rows",
                    "completed after 997 rows");
            FullScanCompletionLogMetadata row997Completion =
                FullScanCompletionLogInspector.Inspect(
                    row997CompletionText, 997);
            Equal(997, row997Completion.Rows);
            Equal(8973, row997Completion.MaximumSubmissions);
            Equal(7976, row997Completion.MaximumShortRetries);
            True(row997Completion.InStreamInquiryRevalidationsExact);
            True(row997Completion.TransportCompleted);
            True(!FullScanCompletionLogInspector.Inspect(
                row997CompletionText, true).TransportCompleted);
            string progressCompletionText = row997CompletionText.
                Replace("after-rows=237/997", "after-rows=237/998").
                Replace("after-rows=267/997", "after-rows=267/998").
                Replace("rows=997/997, submissions=2391/8973, " +
                    "short-retries=1394/7976",
                    "rows=998/998, submissions=2392/8982, " +
                    "short-retries=1394/7984").
                Replace("completed after 997 rows",
                    "completed after 998 rows");
            FullScanCompletionLogMetadata progressCompletion =
                FullScanCompletionLogInspector.InspectProgressBounded(
                    progressCompletionText, 998, 998);
            Equal(998, progressCompletion.Rows);
            Equal(998, progressCompletion.RowLimit);
            Equal(8982, progressCompletion.MaximumSubmissions);
            Equal(7984, progressCompletion.MaximumShortRetries);
            True(progressCompletion.InStreamInquiryRevalidationsExact);
            True(progressCompletion.TransportCompleted);
            True(!FullScanCompletionLogInspector.Inspect(
                progressCompletionText, 997).TransportCompleted);
            bool rejectedUnsupportedRowLimit = false;
            try
            {
                FullScanCompletionLogInspector.Inspect(
                    row997CompletionText, 998);
            }
            catch (ArgumentOutOfRangeException)
            {
                rejectedUnsupportedRowLimit = true;
            }
            True(rejectedUnsupportedRowLimit);

            string terminalText =
                "LIVE ASPI full-scan successor observer armed after two " +
                "exact Preview cleanup completions\r\n" +
                "LIVE ASPI exact full-scan terminal probe observed and " +
                "quarantined: row=763, status=0x00, requested=4494, " +
                "actual=10, residue=4484, width=749, selector=0x28, " +
                "submission=887, CDB=28 00 00 00 28 00 00 11 8E 00, " +
                "payload-sha256=" + new string('A', 64) + ". No probe " +
                "payload bytes were copied to FlexColor; every successor " +
                "is blocked before USB.\r\n" +
                "EXEC_SCSI_CMD failed after quarantined probe\r\n";
            FullScanTerminalProbeLogMetadata terminal =
                FullScanTerminalProbeLogInspector.Inspect(terminalText);
            True(terminal != null);
            Equal(763, terminal.Row);
            Equal(0, terminal.Status);
            Equal(4494, terminal.Requested);
            Equal(10, terminal.Actual);
            Equal(4484, terminal.Residue);
            Equal(749, terminal.Width);
            Equal(0x28, terminal.Selector);
            Equal(887, terminal.Submission);
            True(terminal.LengthConsistent);
            True(!FullScanStartupFingerprintLogInspector.Inspect(
                terminalText).FailureBeforeTerminalFingerprint);
            True(FullScanTerminalProbeLogInspector.Inspect(
                terminalText + terminalText) == null);

            text.AppendLine("LIVE ASPI full-scan SET WINDOW fingerprint " +
                "captured and blocked before USB; payload-sha256=" + hash +
                ".");
            FullScanStartupFingerprintLogMetadata ambiguous =
                FullScanStartupFingerprintLogInspector.Inspect(
                    text.ToString());
            True(!ambiguous.HasExclusiveTerminalFingerprint);

            string failed =
                "LIVE ASPI full-scan successor observer armed after two " +
                "exact Preview cleanup completions\r\n" +
                "EXEC_SCSI_CMD failed before terminal fingerprint";
            FullScanStartupFingerprintLogMetadata failedMetadata =
                FullScanStartupFingerprintLogInspector.Inspect(failed);
            True(failedMetadata.FailureBeforeTerminalFingerprint);

            text.AppendLine("EXEC_SCSI_CMD failed after terminal " +
                "fingerprint");
            FullScanStartupFingerprintLogMetadata expectedTerminalFailure =
                FullScanStartupFingerprintLogInspector.Inspect(
                    text.ToString());
            True(!expectedTerminalFailure.FailureBeforeTerminalFingerprint);

            FullScanStartupFingerprintLogMetadata windowOnly =
                FullScanStartupFingerprintLogInspector.Inspect(
                    "LIVE ASPI full-scan SET WINDOW fingerprint captured " +
                    "and blocked before USB; payload-sha256=" + hash + ".");
            True(windowOnly.SetWindowObserved);
            True(windowOnly.HasExclusiveTerminalFingerprint);
        }

        private static void TestOfflinePreviewSequenceFailures()
        {
            byte[] setWindow = PreviewSetWindowData();
            byte[] imageRead =
                ScsiFraming.BuildPrecisionTwoPreviewImageReadCdb(640);
            imageRead[4] = 0x28;

            long now = 0;
            AspiOperationalSequenceGate earlyRow =
                ReadyOfflinePreviewGate(delegate { return now; }, ref now);
            ArmOfflinePreviewWindow(earlyRow, setWindow);
            ExpectInvalid(delegate
            {
                earlyRow.BeginOfflinePreviewImageRead(5, 0, imageRead, 3840);
            });

            now = 0;
            AspiOperationalSequenceGate extraReady =
                ReadyOfflinePreviewGate(delegate { return now; }, ref now);
            ArmOfflinePreviewWindow(extraReady, setWindow);
            CompleteOperationalRead(extraReady,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(extraReady,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            ExpectInvalid(delegate
            {
                extraReady.BeginRead(5, 0,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            });

            now = 0;
            AspiOperationalSequenceGate changedRow =
                ReadyOfflinePreviewGate(delegate { return now; }, ref now);
            ArmOfflinePreviewWindow(changedRow, setWindow);
            CompleteOperationalRead(changedRow,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(changedRow,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            Equal((uint)640, changedRow.BeginOfflinePreviewImageRead(5, 0,
                imageRead, 3840));
            byte[] widerRead =
                ScsiFraming.BuildPrecisionTwoPreviewImageReadCdb(641);
            widerRead[4] = 0x28;
            ExpectProtocol(delegate
            {
                changedRow.BeginOfflinePreviewImageRead(5, 0, widerRead,
                    3846);
            });

            now = 0;
            AspiOperationalSequenceGate earlyCleanup =
                ReadyOfflinePreviewGate(delegate { return now; }, ref now);
            ArmOfflinePreviewWindow(earlyCleanup, setWindow);
            CompleteOperationalRead(earlyCleanup,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(earlyCleanup,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            ExpectInvalid(delegate
            {
                earlyCleanup.ObservePreviewSetWindow(5, 0,
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                    setWindow);
            });

            now = 0;
            AspiOperationalSequenceGate shortCompletion =
                ReadyOfflinePreviewGate(delegate { return now; }, ref now);
            ArmOfflinePreviewWindow(shortCompletion, setWindow);
            shortCompletion.BeginRead(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            ++now;
            ExpectInvalid(delegate
            {
                shortCompletion.CompleteRead(
                    (byte)AdapterStatus.Success, 1, 1, new byte[1]);
            });

            now = 0;
            AspiOperationalSequenceGate lateCompletion =
                ReadyOfflinePreviewGate(delegate { return now; }, ref now);
            ArmOfflinePreviewWindow(lateCompletion, setWindow);
            lateCompletion.BeginRead(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            now += AspiOperationalSequenceGate.
                MaximumCompletionMilliseconds + 1;
            ExpectInvalid(delegate
            {
                lateCompletion.CompleteRead(
                    (byte)AdapterStatus.Success, 0, 2, new byte[2]);
            });
        }

        private static AspiOperationalSequenceGate ReadyOfflinePreviewGate(
            Func<long> clock, ref long now)
        {
            byte[] setWindow = PreviewSetWindowData();
            AspiOperationalSequenceGate gate =
                new AspiOperationalSequenceGate(clock,
                    Sha256Hex(setWindow), true);
            gate.RecordOperationalD8Completion(
                (byte)AdapterStatus.Success, 0, 66, new byte[66]);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteInitializationCycle(gate, 0, ref now);
            Equal(AspiOperationalSequenceState.PreviewSetWindowReady,
                gate.State);
            return gate;
        }

        private static void ArmOfflinePreviewWindow(
            AspiOperationalSequenceGate gate, byte[] setWindow)
        {
            gate.ObservePreviewSetWindow(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow);
            gate.RecordPreviewSetWindowCompletion(
                (byte)AdapterStatus.Success, 0, 84);
        }

        private static void ReplayInitializationCycle(
            IAspiDataOutTransport replay, int continuation)
        {
            replay.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoFaultPixelReadBufferCdb(), 22);
            replay.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            replay.Execute(5, 0, ScsiFraming.
                BuildPrecisionTwoFaultPixelDataReadBufferCdb(), 58);
            replay.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoCalibrationReadBufferCdb(),
                1024);
            if (continuation == 1)
            {
                replay.Execute(5, 0, ScsiFraming.
                    BuildPrecisionTwoD8Offset55ReadBufferCdb(), 10);
            }
            else if (continuation == 2)
            {
                replay.Execute(5, 0, ScsiFraming.
                    BuildPrecisionTwoDynamicConfigurationD8ReadBufferCdb(),
                    1024);
            }
            replay.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
        }

        private static void TestHostAdapterInquiry()
        {
            FakeTransport transport = new FakeTransport();
            IntPtr srb = AllocateSrb();
            try
            {
                Marshal.WriteByte(srb, AspiConstants.CommandOffset,
                    AspiConstants.HostAdapterInquiry);
                uint result = new AspiSrbProcessor(transport).Process(srb);
                Equal((uint)AspiConstants.StatusComplete, result);
                Equal(AspiConstants.StatusComplete,
                    Marshal.ReadByte(srb, AspiConstants.StatusOffset));
                Equal((byte)1, Marshal.ReadByte(srb, 0x08));
                Equal((byte)7, Marshal.ReadByte(srb, 0x09));
                Equal("USB2Xchange", ReadAscii(srb, 0x0A, 16));
                Equal(0, transport.CallCount);
            }
            finally
            {
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestGetDeviceType()
        {
            FakeTransport transport = new FakeTransport();
            byte[] data = new byte[36];
            data[0] = 0x86;
            transport.Enqueue(data, (byte)AdapterStatus.Success);
            IntPtr srb = AllocateSrb();
            try
            {
                Marshal.WriteByte(srb, AspiConstants.CommandOffset,
                    AspiConstants.GetDeviceType);
                Marshal.WriteByte(srb, AspiConstants.TargetOffset, 5);
                uint result = new AspiSrbProcessor(transport).Process(srb);
                Equal((uint)AspiConstants.StatusComplete, result);
                Equal((byte)0x06, Marshal.ReadByte(srb, 0x0A));
                Equal((byte)0x12, transport.LastCdb[0]);
                Equal((byte)5, transport.LastTarget);
            }
            finally
            {
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestAbsentTarget()
        {
            FakeTransport transport = new FakeTransport();
            transport.Enqueue(new byte[0],
                (byte)AdapterStatus.SelectionTimeout);
            IntPtr srb = Marshal.AllocHGlobal(24);
            try
            {
                for (int i = 0; i < 12; i++)
                {
                    Marshal.WriteByte(srb, i, 0);
                }
                for (int i = 12; i < 24; i++)
                {
                    Marshal.WriteByte(srb, i, 0xCC);
                }
                Marshal.WriteByte(srb, AspiConstants.CommandOffset,
                    AspiConstants.GetDeviceType);
                Marshal.WriteByte(srb, AspiConstants.TargetOffset, 4);
                uint result = new AspiSrbProcessor(transport).Process(srb);
                Equal((uint)AspiConstants.StatusNoDevice, result);
                for (int i = 12; i < 24; i++)
                {
                    Equal((byte)0xCC, Marshal.ReadByte(srb, i));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestInquiry()
        {
            FakeTransport transport = new FakeTransport();
            byte[] data = new byte[36];
            data[8] = (byte)'I';
            transport.Enqueue(data, (byte)AdapterStatus.Success);
            IntPtr buffer = Marshal.AllocHGlobal(36);
            IntPtr srb = AllocateSrb();
            try
            {
                ConfigureExecuteSrb(srb, 5, AspiConstants.FlagDirectionIn,
                    36, buffer, new byte[] { 0x12, 0, 0, 0, 36, 0 });
                uint result = new AspiSrbProcessor(transport).Process(srb);
                Equal((uint)AspiConstants.StatusComplete, result);
                Equal((byte)'I', Marshal.ReadByte(buffer, 8));
                Equal(AspiConstants.HostStatusOk,
                    Marshal.ReadByte(srb, AspiConstants.HostStatusOffset));
                Equal(AspiConstants.TargetStatusGood,
                    Marshal.ReadByte(srb, AspiConstants.TargetStatusOffset));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestTestUnitReady()
        {
            FakeTransport transport = new FakeTransport();
            transport.Enqueue(new byte[0], (byte)AdapterStatus.Success);
            IntPtr srb = AllocateSrb();
            try
            {
                ConfigureExecuteSrb(srb, 5, 0, 0, IntPtr.Zero,
                    new byte[] { 0, 0, 0, 0, 0, 0 });
                uint result = new AspiSrbProcessor(transport).Process(srb);
                Equal((uint)AspiConstants.StatusComplete, result);
                Equal((byte)0x00, transport.LastCdb[0]);
            }
            finally
            {
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestBlockedCommands()
        {
            FakeTransport transport = new FakeTransport();
            AspiSrbProcessor processor = new AspiSrbProcessor(transport);
            IntPtr srb = AllocateSrb();
            try
            {
                ConfigureExecuteSrb(srb, 5, 0, 0, IntPtr.Zero,
                    new byte[] { 0xE1, 0, 0, 0, 0, 0 });
                Equal((uint)AspiConstants.StatusInvalidCommand,
                    processor.Process(srb));

                IntPtr buffer = Marshal.AllocHGlobal(1);
                try
                {
                    ConfigureExecuteSrb(srb, 5,
                        AspiConstants.FlagDirectionOut, 1, buffer,
                        new byte[] { 0x12, 0, 0, 0, 1, 0 });
                    Equal((uint)AspiConstants.StatusInvalidSrb,
                        processor.Process(srb));
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
                Equal(0, transport.CallCount);
            }
            finally
            {
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestTrustedFlexColorIdentityGate()
        {
            True(TrustedFlexColorProcessGate.IsTrustedIdentity(
                "FlexColor.exe",
                TrustedFlexColorProcessGate.ExecutableSha256,
                TrustedFlexColorProcessGate.PatchedDllSha256,
                TrustedFlexColorProcessGate.OriginalDllSha256));
            True(TrustedFlexColorProcessGate.IsTrustedIdentity(
                "FLEXCOLOR.EXE",
                TrustedFlexColorProcessGate.ExecutableSha256,
                TrustedFlexColorProcessGate.PatchedDllSha256,
                TrustedFlexColorProcessGate.OriginalDllSha256));
            True(!TrustedFlexColorProcessGate.IsTrustedIdentity(
                "Other.exe",
                TrustedFlexColorProcessGate.ExecutableSha256,
                TrustedFlexColorProcessGate.PatchedDllSha256,
                TrustedFlexColorProcessGate.OriginalDllSha256));
            True(!TrustedFlexColorProcessGate.IsTrustedIdentity(
                "FlexColor.exe", new string('0', 64),
                TrustedFlexColorProcessGate.PatchedDllSha256,
                TrustedFlexColorProcessGate.OriginalDllSha256));
            True(!TrustedFlexColorProcessGate.IsTrustedIdentity(
                "FlexColor.exe",
                TrustedFlexColorProcessGate.ExecutableSha256,
                TrustedFlexColorProcessGate.OriginalDllSha256,
                TrustedFlexColorProcessGate.OriginalDllSha256));
            True(!TrustedFlexColorProcessGate.ValidateCurrentProcess(
                new TestUsbLog()));
        }

        private static void TestTrustedPassThroughRuntimeApproval()
        {
            string originalMode = Environment.GetEnvironmentVariable(
                AspiRuntime.TransportEnvironmentVariable);
            string originalApproval = Environment.GetEnvironmentVariable(
                AspiRuntime.TrustedFlexColorApprovalEnvironmentVariable);
            TestUsbLog log = new TestUsbLog();
            try
            {
                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.TrustedFlexColorPassThroughMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(
                    AspiRuntime.TrustedFlexColorApprovalEnvironmentVariable,
                    null, EnvironmentVariableTarget.Process);
                AspiRuntimeContext missing = AspiRuntime.Create(log,
                    delegate { return true; });
                Equal((byte)0, missing.AdapterCount);
                True(missing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TrustedFlexColorApprovalEnvironmentVariable,
                    "wrong", EnvironmentVariableTarget.Process);
                AspiRuntimeContext wrong = AspiRuntime.Create(log,
                    delegate { return true; });
                Equal((byte)0, wrong.AdapterCount);
                True(wrong.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TrustedFlexColorApprovalEnvironmentVariable,
                    AspiRuntime.TrustedFlexColorApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext untrusted = AspiRuntime.Create(log,
                    delegate { return false; });
                Equal((byte)0, untrusted.AdapterCount);
                True(untrusted.Transport is DisabledAspiTransport);

                AspiRuntimeContext actualUntrustedProcess =
                    AspiRuntime.Create(log);
                Equal((byte)0, actualUntrustedProcess.AdapterCount);
                True(actualUntrustedProcess.Transport is
                    DisabledAspiTransport);

                AspiRuntimeContext exact = AspiRuntime.Create(log,
                    delegate { return true; });
                Equal((byte)1, exact.AdapterCount);
                True(exact.Transport is
                    IAspiTransparentPassThroughTransport);
                True(!exact.LoaderDataOutEnabled);
                True(!exact.PreviewSetWindowEnabled);
                AspiTransportResult absent = exact.Transport.Execute(4, 0,
                    new byte[] { 0x12, 0, 0, 0, 36, 0 }, 36);
                Equal((byte)AdapterStatus.SelectionTimeout,
                    absent.AdapterStatus);
                Equal((uint)36, absent.Residue);
                ((IDisposable)exact.Transport).Dispose();
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable, originalMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(
                    AspiRuntime.TrustedFlexColorApprovalEnvironmentVariable,
                    originalApproval, EnvironmentVariableTarget.Process);
            }
        }

        private static void TestTrustedPassThroughSrbRouting()
        {
            var transport = new FakeTransparentTransport();
            byte[] input = new byte[] { 0x10, 0x20, 0x30, 0x40 };
            transport.Enqueue(new AspiTransportResult(input,
                (byte)AdapterStatus.Success, 6, 2));
            transport.Enqueue(new AspiTransportResult(new byte[0],
                (byte)AdapterStatus.Success, 3, 0));
            transport.Enqueue(new AspiTransportResult(new byte[0],
                (byte)AdapterStatus.Success, 0, 0));
            AspiSrbProcessor processor = new AspiSrbProcessor(transport);
            IntPtr buffer = Marshal.AllocHGlobal(6);
            IntPtr srb = AllocateSrb();
            try
            {
                for (int index = 0; index < 6; ++index)
                {
                    Marshal.WriteByte(buffer, index, 0xCC);
                }
                byte[] readCdb = new byte[]
                {
                    0xE1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
                };
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn |
                    AspiConstants.FlagResidualCount,
                    6, buffer, readCdb);
                Equal((uint)AspiConstants.StatusComplete,
                    processor.Process(srb));
                Equal((byte)0x10, Marshal.ReadByte(buffer, 0));
                Equal((byte)0x40, Marshal.ReadByte(buffer, 3));
                Equal((byte)0xCC, Marshal.ReadByte(buffer, 4));
                Equal((uint)2, unchecked((uint)Marshal.ReadInt32(srb,
                    AspiConstants.BufferLengthOffset)));
                Equal(BitConverter.ToString(readCdb),
                    BitConverter.ToString(transport.LastCdb));

                byte[] output = new byte[] { 9, 8, 7 };
                Marshal.Copy(output, 0, buffer, output.Length);
                byte[] writeCdb = new byte[]
                {
                    0xE2, 1, 2, 3, 4, 5, 6, 7,
                    8, 9, 10, 11, 12, 13, 14, 15
                };
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionOut, 3, buffer, writeCdb);
                Equal((uint)AspiConstants.StatusComplete,
                    processor.Process(srb));
                Equal("09-08-07",
                    BitConverter.ToString(transport.LastDataOut));
                Equal((byte)9, Marshal.ReadByte(buffer, 0));
                Equal(BitConverter.ToString(writeCdb),
                    BitConverter.ToString(transport.LastCdb));
                True(transport.LastDataOutReferenceCleared);

                byte[] noDataCdb = new byte[] { 0xE3, 0, 0, 0, 0, 0 };
                ConfigureExecuteSrb(srb, 5, 0, 0, IntPtr.Zero, noDataCdb);
                Equal((uint)AspiConstants.StatusComplete,
                    processor.Process(srb));
                Equal(BitConverter.ToString(noDataCdb),
                    BitConverter.ToString(transport.LastCdb));
                Equal(3, transport.CallCount);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestTrustedPassThroughMalformedSrbs()
        {
            var transport = new FakeTransparentTransport();
            AspiSrbProcessor processor = new AspiSrbProcessor(transport);
            IntPtr srb = AllocateSrb();
            try
            {
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn |
                    AspiConstants.FlagDirectionOut,
                    1, new IntPtr(1), new byte[] { 0xE1, 0 });
                Equal((uint)AspiConstants.StatusInvalidSrb,
                    processor.Process(srb));

                ConfigureExecuteSrb(srb, 5, 0x80, 0, IntPtr.Zero,
                    new byte[] { 0xE1, 0 });
                Equal((uint)AspiConstants.StatusInvalidSrb,
                    processor.Process(srb));

                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn,
                    AspiConstants.MaximumTrustedTransferLength + 1U,
                    new IntPtr(1), new byte[] { 0xE1, 0 });
                Equal((uint)AspiConstants.StatusInvalidSrb,
                    processor.Process(srb));

                ConfigureExecuteSrb(srb, 5, 0, 0, IntPtr.Zero,
                    new byte[] { 0xE1 });
                Equal((uint)AspiConstants.StatusInvalidSrb,
                    processor.Process(srb));

                ConfigureExecuteSrb(srb, 5, 0, 0, IntPtr.Zero,
                    new byte[] { 0xE1, 0 });
                Equal((uint)AspiConstants.StatusInvalidSrb,
                    processor.Process(srb));
                Equal(0, transport.CallCount);
            }
            finally
            {
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestTrustedPassThroughShortCompletion()
        {
            var transport = new FakeTransparentTransport();
            transport.Enqueue(new AspiTransportResult(
                new byte[] { 1, 2 }, (byte)AdapterStatus.Success, 8, 6));
            transport.Enqueue(new AspiTransportResult(
                new byte[] { 3, 4 }, (byte)AdapterStatus.Success, 8, 6));
            AspiSrbProcessor processor = new AspiSrbProcessor(transport);
            IntPtr buffer = Marshal.AllocHGlobal(8);
            IntPtr srb = AllocateSrb();
            try
            {
                for (int index = 0; index < 8; ++index)
                {
                    Marshal.WriteByte(buffer, index, 0xCC);
                }
                byte[] cdb = new byte[] { 0xE1, 0, 0, 0, 0, 0 };
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn, 8, buffer, cdb);
                Equal((uint)AspiConstants.StatusError,
                    processor.Process(srb));
                Equal(AspiConstants.TargetStatusBusy,
                    Marshal.ReadByte(srb,
                        AspiConstants.TargetStatusOffset));
                Equal((byte)0xCC, Marshal.ReadByte(buffer, 0));

                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn |
                    AspiConstants.FlagResidualCount,
                    8, buffer, cdb);
                Equal((uint)AspiConstants.StatusComplete,
                    processor.Process(srb));
                Equal((byte)3, Marshal.ReadByte(buffer, 0));
                Equal((uint)6, unchecked((uint)Marshal.ReadInt32(srb,
                    AspiConstants.BufferLengthOffset)));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestTrustedPassThroughSerialization()
        {
            var transport = new BlockingTransparentTransport();
            AspiSrbProcessor processor = new AspiSrbProcessor(transport);
            IntPtr firstSrb = AllocateSrb();
            IntPtr secondSrb = AllocateSrb();
            Exception firstFailure = null;
            Exception secondFailure = null;
            Thread first = null;
            Thread second = null;
            try
            {
                ConfigureExecuteSrb(firstSrb, 5, 0, 0, IntPtr.Zero,
                    new byte[] { 0xE1, 0, 0, 0, 0, 0 });
                ConfigureExecuteSrb(secondSrb, 5, 0, 0, IntPtr.Zero,
                    new byte[] { 0xE2, 0, 0, 0, 0, 0 });
                first = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        Equal((uint)AspiConstants.StatusComplete,
                            processor.Process(firstSrb));
                    }
                    catch (Exception ex)
                    {
                        firstFailure = ex;
                    }
                }));
                second = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        Equal((uint)AspiConstants.StatusComplete,
                            processor.Process(secondSrb));
                    }
                    catch (Exception ex)
                    {
                        secondFailure = ex;
                    }
                }));
                first.Start();
                True(transport.FirstEntered.WaitOne(5000));
                second.Start();
                True(!transport.SecondEntered.WaitOne(100));
                transport.ReleaseFirst.Set();
                True(first.Join(5000));
                True(second.Join(5000));
                True(transport.SecondEntered.WaitOne(0));
                if (firstFailure != null)
                {
                    throw firstFailure;
                }
                if (secondFailure != null)
                {
                    throw secondFailure;
                }
                Equal(2, transport.CallCount);
            }
            finally
            {
                transport.ReleaseFirst.Set();
                if (first != null && first.IsAlive)
                {
                    first.Join(5000);
                }
                if (second != null && second.IsAlive)
                {
                    second.Join(5000);
                }
                Marshal.FreeHGlobal(firstSrb);
                Marshal.FreeHGlobal(secondSrb);
                transport.Dispose();
            }
        }

        private static void TestTrustedPassThroughCheckCondition()
        {
            var transport = new FakeTransparentTransport();
            transport.Enqueue(new AspiTransportResult(new byte[0],
                (byte)AdapterStatus.CheckCondition, 4, 4));
            byte[] sense = new byte[18];
            sense[0] = 0x70;
            sense[2] = 0x05;
            transport.Enqueue(new AspiTransportResult(sense,
                (byte)AdapterStatus.Success, 18, 0));
            AspiSrbProcessor processor = new AspiSrbProcessor(transport);
            IntPtr buffer = Marshal.AllocHGlobal(4);
            IntPtr srb = AllocateSrb();
            try
            {
                for (int index = 0; index < 4; ++index)
                {
                    Marshal.WriteByte(buffer, index, 0xCC);
                }
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn, 4, buffer,
                    new byte[] { 0xE1, 0, 0, 0, 0, 0 });
                Marshal.WriteByte(srb, AspiConstants.SenseLengthOffset, 18);
                Equal((uint)AspiConstants.StatusError,
                    processor.Process(srb));
                Equal(AspiConstants.HostStatusOk,
                    Marshal.ReadByte(srb, AspiConstants.HostStatusOffset));
                Equal(AspiConstants.TargetStatusCheckCondition,
                    Marshal.ReadByte(srb, AspiConstants.TargetStatusOffset));
                Equal((byte)0x70, Marshal.ReadByte(srb,
                    AspiConstants.SenseAreaOffset));
                Equal((byte)0x05, Marshal.ReadByte(srb,
                    AspiConstants.SenseAreaOffset + 2));
                Equal((byte)0xCC, Marshal.ReadByte(buffer, 0));
                Equal(2, transport.CallCount);
                Equal((byte)0x03, transport.LastCdb[0]);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestOperationalDiscoveryReads()
        {
            FakeTransport transport = new FakeTransport();
            transport.Enqueue(new byte[66], (byte)AdapterStatus.Success);
            transport.Enqueue(new byte[2], (byte)AdapterStatus.Success);
            AspiSrbProcessor processor = new AspiSrbProcessor(transport);
            IntPtr buffer = Marshal.AllocHGlobal(66);
            IntPtr srb = AllocateSrb();
            try
            {
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn, 66, buffer,
                    ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb());
                Equal((uint)AspiConstants.StatusComplete,
                    processor.Process(srb));
                Equal(10, transport.LastCdb.Length);

                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn, 2, buffer,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb());
                Equal((uint)AspiConstants.StatusComplete,
                    processor.Process(srb));

                byte[] variant =
                    ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb();
                variant[8] = 0x41;
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn, 66, buffer, variant);
                Equal((uint)AspiConstants.StatusInvalidCommand,
                    processor.Process(srb));
                Equal(2, transport.CallCount);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestOfflinePreviewReadSrbRouting()
        {
            const uint length = 4488;
            byte[] cdb =
                ScsiFraming.BuildPrecisionTwoPreviewImageReadCdb(748);
            cdb[4] = 0x28;
            FakeTransport replayTransport = new FakeTransport();
            replayTransport.Enqueue(new byte[checked((int)length)],
                (byte)AdapterStatus.Success);
            IntPtr buffer = Marshal.AllocHGlobal(checked((int)length));
            IntPtr srb = AllocateSrb();
            try
            {
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn, length, buffer, cdb);
                AspiSrbProcessor replayProcessor = new AspiSrbProcessor(
                    replayTransport, null, false, true, true);
                Equal((uint)AspiConstants.StatusComplete,
                    replayProcessor.Process(srb));
                Equal(1, replayTransport.CallCount);

                FakeTransport livePreviewTransport = new FakeTransport();
                livePreviewTransport.Enqueue(
                    new byte[checked((int)length)],
                    (byte)AdapterStatus.Success);
                AspiSrbProcessor livePreviewProcessor =
                    new AspiSrbProcessor(livePreviewTransport, null, false,
                        false, true, true);
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn, length, buffer, cdb);
                Equal((uint)AspiConstants.StatusComplete,
                    livePreviewProcessor.Process(srb));
                Equal(1, livePreviewTransport.CallCount);

                byte[] oldSelector = (byte[])cdb.Clone();
                oldSelector[4] = 0x51;
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn, length, buffer,
                    oldSelector);
                Equal((uint)AspiConstants.StatusInvalidCommand,
                    livePreviewProcessor.Process(srb));
                Equal(1, livePreviewTransport.CallCount);

                FakeTransport ordinaryTransport = new FakeTransport();
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn, length, buffer, cdb);
                Equal((uint)AspiConstants.StatusInvalidSrb,
                    new AspiSrbProcessor(ordinaryTransport).Process(srb));
                Equal(0, ordinaryTransport.CallCount);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestExtendedOperationalReadSrbRouting()
        {
            FakeTransport transport = new FakeTransport();
            transport.Enqueue(KnownFaultPixelHeader(),
                (byte)AdapterStatus.Success);
            transport.Enqueue(new byte[
                ScsiFraming.PrecisionTwoFaultPixelDataLength],
                (byte)AdapterStatus.Success);
            transport.Enqueue(new byte[
                ScsiFraming.PrecisionTwoCalibrationBufferLength],
                (byte)AdapterStatus.Success);
            transport.Enqueue(new byte[
                ScsiFraming.PrecisionTwoD8Offset55Length],
                (byte)AdapterStatus.Success);
            transport.Enqueue(new byte[
                ScsiFraming.PrecisionTwoDynamicConfigurationLength],
                (byte)AdapterStatus.Success);
            AspiSrbProcessor processor = new AspiSrbProcessor(transport, true);
            IntPtr buffer = Marshal.AllocHGlobal(1082);
            IntPtr srb = AllocateSrb();
            try
            {
                byte[][] cdbs = new byte[][]
                {
                    ScsiFraming.BuildPrecisionTwoFaultPixelReadBufferCdb(),
                    ScsiFraming.BuildPrecisionTwoFaultPixelDataReadBufferCdb(),
                    ScsiFraming.BuildPrecisionTwoCalibrationReadBufferCdb(),
                    ScsiFraming.BuildPrecisionTwoD8Offset55ReadBufferCdb(),
                    ScsiFraming.
                        BuildPrecisionTwoDynamicConfigurationD8ReadBufferCdb()
                };
                uint[] lengths = new uint[] { 22, 58, 1024, 10, 1024 };
                for (int index = 0; index < cdbs.Length; ++index)
                {
                    ConfigureExecuteSrb(srb, 5,
                        AspiConstants.FlagDirectionIn, lengths[index], buffer,
                        cdbs[index]);
                    Equal((uint)AspiConstants.StatusComplete,
                        processor.Process(srb));
                }
                Equal(5, transport.CallCount);

                byte[] variant = ScsiFraming.
                    BuildPrecisionTwoCalibrationReadBufferCdb();
                variant[7] = 3;
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn, 1024, buffer, variant);
                Equal((uint)AspiConstants.StatusInvalidCommand,
                    processor.Process(srb));
                Equal(5, transport.CallCount);

                FakeTransport replayTransport = new FakeTransport();
                replayTransport.Enqueue(new byte[1072],
                    (byte)AdapterStatus.Success);
                AspiSrbProcessor replayProcessor = new AspiSrbProcessor(
                    replayTransport, null, false, true, true);
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn, 1072, buffer,
                    ScsiFraming.
                        BuildPrecisionTwoStartupReadBufferE01072Cdb());
                Equal((uint)AspiConstants.StatusComplete,
                    replayProcessor.Process(srb));
                Equal(1, replayTransport.CallCount);

                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn, 1072, buffer,
                    ScsiFraming.
                        BuildPrecisionTwoStartupReadBufferE01072Cdb());
                Equal((uint)AspiConstants.StatusInvalidCommand,
                    processor.Process(srb));

                FakeTransport disabledTransport = new FakeTransport();
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn, 22, buffer,
                    ScsiFraming.BuildPrecisionTwoFaultPixelReadBufferCdb());
                Equal((uint)AspiConstants.StatusInvalidCommand,
                    new AspiSrbProcessor(disabledTransport).Process(srb));
                Equal(0, disabledTransport.CallCount);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestReadOnlyDiscoveryGate()
        {
            AspiReadOnlyDiscoveryGate gate =
                new AspiReadOnlyDiscoveryGate();
            ExpectInvalid(delegate
            {
                gate.BeginD8(5, 0, 66);
            });
            gate.RecordIdentity(InquiryData.Parse(BuildInquiry(
                "Imacon", "FlexTight II", "M333")));
            ExpectInvalid(delegate
            {
                gate.BeginD8(4, 0, 66);
            });
            gate.BeginD8(5, 0, 66);
            ExpectInvalid(delegate
            {
                gate.BeginD8(5, 0, 66);
            });
            gate.RecordD8((byte)AdapterStatus.Success, 0, 66);
            gate.BeginScannerReady(5, 0, 2);
            ExpectInvalid(delegate
            {
                gate.BeginScannerReady(5, 0, 2);
            });

            AspiReadOnlyDiscoveryGate failed =
                new AspiReadOnlyDiscoveryGate();
            failed.RecordIdentity(InquiryData.Parse(BuildInquiry(
                "Imacon", "FlexTight II", "M333")));
            failed.BeginD8(5, 0, 66);
            failed.RecordD8((byte)AdapterStatus.SelectionTimeout, 66, 0);
            ExpectInvalid(delegate
            {
                failed.BeginScannerReady(5, 0, 2);
            });

            AspiReadOnlyDiscoveryGate loader =
                new AspiReadOnlyDiscoveryGate();
            loader.RecordIdentity(InquiryData.Parse(BuildInquiry(
                "Imacon", "SCSI Loader", "L302")));
            loader.BeginD8(5, 0, 66);
            loader.RecordD8((byte)AdapterStatus.Success, 0, 66);
            ExpectInvalid(delegate
            {
                loader.BeginScannerReady(5, 0, 2);
            });

            True(loader.LoaderSequenceReady);
            loader.RecordLoaderTransitionCompleted();
            True(!loader.LoaderSequenceReady);
            Equal(PrecisionTwoIdentity.Unknown, loader.Identity);
            True(loader.PostLoaderScannerReadyRequired);
            loader.BeginPostLoaderScannerReady(5, 0, 2);
            True(!loader.PostLoaderScannerReadyRequired);
            ExpectInvalid(delegate
            {
                loader.BeginPostLoaderScannerReady(5, 0, 2);
            });
            loader.RecordPostLoaderScannerReady(
                (byte)AdapterStatus.Success, 0, 2, new byte[2]);
            True(loader.PostLoaderScannerReadySucceeded);
            loader.RecordIdentity(InquiryData.Parse(BuildInquiry(
                "Imacon", "FlexTight II", "M333")));
            True(loader.PostLoaderOperationalInitializationRequired);
            loader.ArmPostLoaderOperationalInitialization();
            True(!loader.PostLoaderOperationalInitializationRequired);
            ExpectInvalid(delegate
            {
                loader.BeginD8(5, 0, 66);
            });
            ExpectInvalid(delegate
            {
                loader.ArmPostLoaderOperationalInitialization();
            });

            AspiReadOnlyDiscoveryGate failedTransition =
                new AspiReadOnlyDiscoveryGate();
            failedTransition.RecordIdentity(InquiryData.Parse(BuildInquiry(
                "Imacon", "SCSI Loader", "L302")));
            failedTransition.BeginD8(5, 0, 66);
            failedTransition.RecordD8(
                (byte)AdapterStatus.Success, 0, 66);
            failedTransition.RecordLoaderTransitionCompleted();
            failedTransition.BeginPostLoaderScannerReady(5, 0, 2);
            failedTransition.RecordPostLoaderScannerReady(
                (byte)AdapterStatus.SelectionTimeout, 2, 0, new byte[0]);
            failedTransition.RecordIdentity(InquiryData.Parse(BuildInquiry(
                "Imacon", "FlexTight II", "M333")));
            True(!failedTransition.
                PostLoaderOperationalInitializationRequired);
            ExpectInvalid(delegate
            {
                failedTransition.BeginD8(5, 0, 66);
            });
            ExpectInvalid(delegate
            {
                failedTransition.ArmPostLoaderOperationalInitialization();
            });

            AspiReadOnlyDiscoveryGate nonReadyTransition =
                new AspiReadOnlyDiscoveryGate();
            nonReadyTransition.RecordIdentity(InquiryData.Parse(BuildInquiry(
                "Imacon", "SCSI Loader", "L302")));
            nonReadyTransition.BeginD8(5, 0, 66);
            nonReadyTransition.RecordD8(
                (byte)AdapterStatus.Success, 0, 66);
            nonReadyTransition.RecordLoaderTransitionCompleted();
            nonReadyTransition.BeginPostLoaderScannerReady(5, 0, 2);
            nonReadyTransition.RecordPostLoaderScannerReady(
                (byte)AdapterStatus.Success, 0, 2,
                new byte[] { 0x08, 0x00 });
            True(!nonReadyTransition.PostLoaderScannerReadySucceeded);
        }

        private static void TestOperationalSequenceExact()
        {
            long now = 0;
            byte[] setWindow = PreviewSetWindowData();
            AspiOperationalSequenceGate gate =
                new AspiOperationalSequenceGate(delegate { return now; },
                    Sha256Hex(setWindow));
            gate.RecordOperationalD8Completion(
                (byte)AdapterStatus.Success, 0, 66, new byte[66]);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteInitializationCycle(gate, 1, ref now);
            Equal(1, gate.CompletedInitializationCycles);
            Equal(AspiOperationalSequenceState.PreviewSetWindowReady,
                gate.State);

            CompleteInitializationCycle(gate, 0, ref now);
            Equal(2, gate.CompletedInitializationCycles);
            Equal(AspiOperationalSequenceState.PreviewSetWindowReady,
                gate.State);

            CompleteInitializationCycle(gate, 2, ref now);
            Equal(3, gate.CompletedInitializationCycles);
            Equal(AspiOperationalSequenceState.PreviewSetWindowReady,
                gate.State);

            gate.ObservePreviewSetWindow(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow);
            Equal(AspiOperationalSequenceState.PreviewSetWindowObserved,
                gate.State);
            gate.RecordPreviewSetWindowCompletion(
                (byte)AdapterStatus.Success, 0, 84);
            Equal((uint)640, gate.ObservePredictedPreviewImageRead(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewImageReadCdb(640),
                3840));
            Equal(AspiOperationalSequenceState.
                PredictedPreviewImageReadObserved, gate.State);

            now = 0;
            AspiOperationalSequenceGate postLoaderGate =
                new AspiOperationalSequenceGate(delegate { return now; });
            postLoaderGate.BeginPostLoaderInitialization();
            Equal(AspiOperationalSequenceState.AwaitInitialScannerReady,
                postLoaderGate.State);
            CompleteOperationalRead(postLoaderGate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            Equal(AspiOperationalSequenceState.AwaitFaultPixelHeader,
                postLoaderGate.State);
        }

        private static void TestOperationalSequenceFailures()
        {
            AspiOperationalSequenceGate outOfOrder =
                ReadyOperationalGate();
            ExpectInvalid(delegate
            {
                outOfOrder.BeginRead(5, 0,
                    ScsiFraming.BuildPrecisionTwoFaultPixelReadBufferCdb(),
                    22);
            });
            Equal(AspiOperationalSequenceState.Failed, outOfOrder.State);

            AspiOperationalSequenceGate wrongTarget =
                ReadyOperationalGate();
            ExpectInvalid(delegate
            {
                wrongTarget.BeginRead(4, 0,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            });
            Equal(AspiOperationalSequenceState.Failed, wrongTarget.State);

            AspiOperationalSequenceGate wrongHeader =
                ReadyOperationalGate();
            long now = 0;
            CompleteOperationalRead(wrongHeader,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            wrongHeader.BeginRead(5, 0,
                ScsiFraming.BuildPrecisionTwoFaultPixelReadBufferCdb(), 22);
            ExpectProtocol(delegate
            {
                wrongHeader.CompleteRead((byte)AdapterStatus.Success, 0, 22,
                    new byte[22]);
            });
            Equal(AspiOperationalSequenceState.Failed, wrongHeader.State);

            now = 0;
            AspiOperationalSequenceGate expired =
                new AspiOperationalSequenceGate(delegate { return now; });
            expired.RecordOperationalD8Completion(
                (byte)AdapterStatus.Success, 0, 66, new byte[66]);
            expired.BeginRead(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            now = AspiOperationalSequenceGate.
                MaximumCompletionMilliseconds + 1;
            ExpectInvalid(delegate
            {
                expired.CompleteRead((byte)AdapterStatus.Success, 0, 2,
                    new byte[2]);
            });
            Equal(AspiOperationalSequenceState.Failed, expired.State);

            now = 5;
            AspiOperationalSequenceGate backwards =
                new AspiOperationalSequenceGate(delegate { return now; });
            backwards.RecordOperationalD8Completion(
                (byte)AdapterStatus.Success, 0, 66, new byte[66]);
            backwards.BeginRead(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            now = 4;
            ExpectInvalid(delegate
            {
                backwards.CompleteRead((byte)AdapterStatus.Success, 0, 2,
                    new byte[2]);
            });
            Equal(AspiOperationalSequenceState.Failed, backwards.State);

            AspiOperationalSequenceGate nonReadyCommon =
                ReadyOperationalGate();
            nonReadyCommon.BeginRead(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            ExpectProtocol(delegate
            {
                nonReadyCommon.CompleteRead(
                    (byte)AdapterStatus.Success, 0, 2,
                    new byte[] { 0x08, 0x00 });
            });
            Equal(AspiOperationalSequenceState.Failed,
                nonReadyCommon.State);

            AspiOperationalSequenceGate repeatedPostLoader =
                new AspiOperationalSequenceGate(delegate { return 0; });
            repeatedPostLoader.BeginPostLoaderInitialization();
            ExpectInvalid(delegate
            {
                repeatedPostLoader.BeginPostLoaderInitialization();
            });
            Equal(AspiOperationalSequenceState.Failed,
                repeatedPostLoader.State);
        }

        private static void TestLivePreviewSuccessorGate()
        {
            long now = 0;
            byte[] setWindow = PreviewSetWindowData();
            AspiOperationalSequenceGate gate =
                new AspiOperationalSequenceGate(delegate { return now; },
                    Sha256Hex(setWindow), false, true);
            gate.RecordOperationalD8Completion(
                (byte)AdapterStatus.Success, 0, 66, new byte[66]);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteInitializationCycle(gate, 0, ref now);
            gate.ObservePreviewSetWindow(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow);
            gate.RecordPreviewSetWindowCompletion(
                (byte)AdapterStatus.Success, 0, 84);
            Equal(AspiOperationalSequenceState.
                AwaitFirstPostWindowScannerReady, gate.State);
            gate.BeginRead(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            Equal(1, gate.PendingPostWindowScannerReadyAttempt);
            ++now;
            True(!gate.CompleteRead((byte)AdapterStatus.Success, 0, 2,
                new byte[] { 0x08, 0x7F }));
            Equal(AspiOperationalSequenceState.
                AwaitFirstPostWindowScannerReady, gate.State);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            Equal(AspiOperationalSequenceState.
                AwaitSecondPostWindowScannerReady, gate.State);
            gate.BeginRead(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            Equal(1, gate.PendingPostWindowScannerReadyAttempt);
            ++now;
            True(!gate.CompleteRead((byte)AdapterStatus.Success, 0, 2,
                new byte[] { 0x01, 0x55 }));
            Equal(AspiOperationalSequenceState.
                AwaitSecondPostWindowScannerReady, gate.State);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            Equal(AspiOperationalSequenceState.
                AwaitPredictedPreviewImageRead, gate.State);
            Equal((uint)666, gate.ObservePredictedPreviewImageRead(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewImageReadCdb(666),
                3996));

            now = 0;
            AspiOperationalSequenceGate boundedRetry =
                new AspiOperationalSequenceGate(delegate { return now; },
                    Sha256Hex(setWindow), false, true);
            boundedRetry.RecordOperationalD8Completion(
                (byte)AdapterStatus.Success, 0, 66, new byte[66]);
            CompleteOperationalRead(boundedRetry,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteInitializationCycle(boundedRetry, 0, ref now);
            boundedRetry.ObservePreviewSetWindow(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow);
            boundedRetry.RecordPreviewSetWindowCompletion(
                (byte)AdapterStatus.Success, 0, 84);
            for (int attempt = 1; attempt <=
                AspiOperationalSequenceGate.
                    MaximumPostWindowScannerReadyAttemptsPerPhase; ++attempt)
            {
                boundedRetry.BeginRead(5, 0,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
                Equal(attempt,
                    boundedRetry.PendingPostWindowScannerReadyAttempt);
                ++now;
                True(!boundedRetry.CompleteRead(
                    (byte)AdapterStatus.Success, 0, 2,
                    new byte[] { 0x01, 0x02 }));
                Equal(AspiOperationalSequenceState.
                    AwaitFirstPostWindowScannerReady, boundedRetry.State);
            }
            ExpectProtocol(delegate
            {
                boundedRetry.BeginRead(5, 0,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            });
            Equal(AspiOperationalSequenceState.Failed,
                boundedRetry.State);

            now = 0;
            AspiOperationalSequenceGate early =
                new AspiOperationalSequenceGate(delegate { return now; },
                    Sha256Hex(setWindow), false, true);
            early.RecordOperationalD8Completion(
                (byte)AdapterStatus.Success, 0, 66, new byte[66]);
            CompleteOperationalRead(early,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteInitializationCycle(early, 0, ref now);
            early.ObservePreviewSetWindow(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow);
            early.RecordPreviewSetWindowCompletion(
                (byte)AdapterStatus.Success, 0, 84);
            ExpectInvalid(delegate
            {
                early.ObservePredictedPreviewImageRead(5, 0,
                    ScsiFraming.BuildPrecisionTwoPreviewImageReadCdb(748),
                    4488);
            });
            Equal(AspiOperationalSequenceState.Failed, early.State);
        }

        private static void TestLivePreviewReadinessTiming()
        {
            Equal(255, AspiOperationalSequenceGate.
                RecoveredScannerReadyRetryDelayMilliseconds);
            Equal(7, AspiOperationalSequenceGate.
                RecoveredScannerReadyEscalationCadence);

            long now = 0;
            byte[] setWindow = PreviewSetWindowData();
            AspiOperationalSequenceGate delayed =
                ArmLivePreviewReadinessGate(delegate { return now; },
                    setWindow, ref now);
            for (int attempt = 1; attempt <= 104; ++attempt)
            {
                delayed.BeginRead(5, 0,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
                Equal(attempt,
                    delayed.PendingPostWindowScannerReadyAttempt);
                ++now;
                True(!delayed.CompleteRead(
                    (byte)AdapterStatus.Success, 0, 2,
                    new byte[] { 0x08, 0x00 }));
                now += AspiOperationalSequenceGate.
                    RecoveredScannerReadyRetryDelayMilliseconds;
            }
            delayed.BeginRead(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            Equal(105, delayed.PendingPostWindowScannerReadyAttempt);
            ++now;
            True(delayed.CompleteRead((byte)AdapterStatus.Success, 0, 2,
                new byte[2]));
            True(delayed.
                LastPostWindowScannerReadyCompletionElapsedMilliseconds >=
                    26000);
            True(delayed.
                LastPostWindowScannerReadyCompletionElapsedMilliseconds <
                    AspiOperationalSequenceGate.
                        MaximumPostWindowScannerReadyPhaseMilliseconds);
            delayed.BeginRead(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            Equal(1, delayed.PendingPostWindowScannerReadyAttempt);
            ++now;
            True(delayed.CompleteRead((byte)AdapterStatus.Success, 0, 2,
                new byte[2]));
            Equal(AspiOperationalSequenceState.
                AwaitPredictedPreviewImageRead, delayed.State);

            now = 0;
            AspiOperationalSequenceGate timedOut =
                ArmLivePreviewReadinessGate(delegate { return now; },
                    setWindow, ref now);
            timedOut.BeginRead(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            ++now;
            True(!timedOut.CompleteRead((byte)AdapterStatus.Success, 0, 2,
                new byte[] { 0x01, 0x00 }));
            now += AspiOperationalSequenceGate.
                MaximumPostWindowScannerReadyPhaseMilliseconds;
            ExpectProtocol(delegate
            {
                timedOut.BeginRead(5, 0,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            });
            Equal(AspiOperationalSequenceState.Failed, timedOut.State);
        }

        private static void TestLivePreviewBurstGate()
        {
            long now = 0;
            byte[] setWindow = PreviewSetWindowData();
            AspiOperationalSequenceGate gate = ArmLivePreviewReadinessGate(
                delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            byte[] cdb = ScsiFraming.
                BuildPrecisionTwoPreviewImageReadCdb(666);
            for (int row = 0; row < PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewBurstRows; ++row)
            {
                Equal((uint)666, gate.BeginLivePreviewImageBurstRead(5, 0,
                    cdb, 3996, PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewBurstRows));
                Equal(row + 1 == PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewBurstRows
                    ? AspiOperationalSequenceState.
                        PredictedPreviewImageReadObserved
                    : AspiOperationalSequenceState.PreviewImageStreaming,
                    gate.State);
            }
            ExpectInvalid(delegate
            {
                gate.BeginLivePreviewImageBurstRead(5, 0, cdb, 3996,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewBurstRows);
            });
            Equal(AspiOperationalSequenceState.Failed, gate.State);

            now = 0;
            AspiOperationalSequenceGate wrongLimit =
                ArmLivePreviewReadinessGate(delegate { return now; },
                    setWindow, ref now);
            CompleteOperationalRead(wrongLimit,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(wrongLimit,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            ExpectInvalid(delegate
            {
                wrongLimit.BeginLivePreviewImageBurstRead(5, 0, cdb, 3996, 7);
            });
            Equal(AspiOperationalSequenceState.Failed, wrongLimit.State);

            now = 0;
            AspiOperationalSequenceGate noStreamWidening =
                ArmLivePreviewReadinessGate(delegate { return now; },
                    setWindow, ref now);
            CompleteOperationalRead(noStreamWidening,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(noStreamWidening,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            ExpectInvalid(delegate
            {
                noStreamWidening.BeginLivePreviewImageBurstRead(5, 0, cdb,
                    3996, PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamRows);
            });
            Equal(AspiOperationalSequenceState.Failed,
                noStreamWidening.State);
        }

        private static void TestLivePreviewStreamGate()
        {
            long now = 0;
            byte[] setWindow = PreviewSetWindowData();
            AspiOperationalSequenceGate gate =
                ArmLivePreviewStreamReadinessGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            byte[] cdb = ScsiFraming.
                BuildPrecisionTwoPreviewImageReadCdb(666);
            AspiOperationalSequenceGate wrongMode =
                ArmLivePreviewStreamReadinessGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(wrongMode,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(wrongMode,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            ExpectInvalid(delegate
            {
                wrongMode.BeginLivePreviewImageBurstRead(5, 0, cdb, 3996,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewBurstRows);
            });
            Equal(AspiOperationalSequenceState.Failed, wrongMode.State);
            for (int row = 0; row < PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamRows; ++row)
            {
                Equal((uint)666, gate.BeginLivePreviewImageBurstRead(5, 0,
                    cdb, 3996, PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamRows));
                if (row == 0 || row == 179)
                {
                    gate.BeginRead(5, 0,
                        ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
                    Equal(row + 1, gate.LivePreviewImageRows);
                    Equal(row == 0 ? 1 : 2,
                        gate.PendingLivePreviewScannerReadyPoll);
                    ++now;
                    True(gate.CompleteRead((byte)AdapterStatus.Success, 0, 2,
                        row == 0
                            ? new byte[] { 0x08, 0x00 }
                            : new byte[] { 0x00, 0x00 }));
                    Equal(AspiOperationalSequenceState.PreviewImageStreaming,
                        gate.State);
                }
            }
            Equal(AspiOperationalSequenceState.
                PredictedPreviewImageReadObserved, gate.State);
            ExpectInvalid(delegate
            {
                gate.BeginRead(5, 0,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            });
            Equal(AspiOperationalSequenceState.Failed, gate.State);

            now = 0;
            AspiOperationalSequenceGate wrongResponse =
                ArmLivePreviewStreamReadinessGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(wrongResponse,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(wrongResponse,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            wrongResponse.BeginLivePreviewImageBurstRead(5, 0, cdb, 3996,
                PrecisionTwoPreviewCommandManifest.AspiLivePreviewStreamRows);
            wrongResponse.BeginRead(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            ++now;
            ExpectProtocol(delegate
            {
                wrongResponse.CompleteRead((byte)AdapterStatus.Success, 0, 2,
                    new byte[] { 0x08, 0x01 });
            });
            Equal(AspiOperationalSequenceState.Failed, wrongResponse.State);

            now = 0;
            AspiOperationalSequenceGate pollBound =
                ArmLivePreviewStreamReadinessGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(pollBound,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(pollBound,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            pollBound.BeginLivePreviewImageBurstRead(5, 0, cdb, 3996,
                PrecisionTwoPreviewCommandManifest.AspiLivePreviewStreamRows);
            for (int poll = 0; poll < PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamMaximumScannerReadyPolls; ++poll)
            {
                pollBound.BeginRead(5, 0,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
                ++now;
                True(pollBound.CompleteRead(
                    (byte)AdapterStatus.Success, 0, 2,
                    new byte[] { 0x08, 0x00 }));
            }
            ExpectProtocol(delegate
            {
                pollBound.BeginRead(5, 0,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            });
            Equal(AspiOperationalSequenceState.Failed, pollBound.State);

            now = 0;
            AspiOperationalSequenceGate timeBound =
                ArmLivePreviewStreamReadinessGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(timeBound,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(timeBound,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            timeBound.BeginLivePreviewImageBurstRead(5, 0, cdb, 3996,
                PrecisionTwoPreviewCommandManifest.AspiLivePreviewStreamRows);
            now += AspiOperationalSequenceGate.
                MaximumLivePreviewStreamMilliseconds;
            ExpectProtocol(delegate
            {
                timeBound.BeginLivePreviewImageBurstRead(5, 0, cdb, 3996,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamRows);
            });
            Equal(AspiOperationalSequenceState.Failed, timeBound.State);
        }

        private static void TestLivePreviewShortRetryGate()
        {
            long now = 0;
            byte[] setWindow = PreviewSetWindowData();
            AspiOperationalSequenceGate gate =
                ArmLivePreviewShortRetryReadinessGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            byte[] cdb = ScsiFraming.
                BuildPrecisionTwoPreviewImageReadCdb(666);

            Equal((uint)666, gate.BeginLivePreviewImageShortRetryRead(
                5, 0, cdb, 3996));
            Equal(1, gate.LivePreviewImageReadSubmissions);
            Equal(AspiLivePreviewImageCompletion.RetryShortRow,
                gate.CompleteLivePreviewImageShortRetryRead(
                    (byte)AdapterStatus.Success, 3996, 3986, 10,
                    new byte[10]));
            Equal(0, gate.LivePreviewImageRows);
            Equal(1, gate.LivePreviewShortRetries);
            Equal(1, gate.LivePreviewConsecutiveShortRetries);

            Equal((uint)666, gate.BeginLivePreviewImageShortRetryRead(
                5, 0, cdb, 3996));
            Equal(2, gate.LivePreviewImageReadSubmissions);
            Equal(AspiLivePreviewImageCompletion.FullRow,
                gate.CompleteLivePreviewImageShortRetryRead(
                    (byte)AdapterStatus.Success, 3996, 0, 3996,
                    new byte[3996]));
            Equal(1, gate.LivePreviewImageRows);
            Equal(0, gate.LivePreviewConsecutiveShortRetries);

            gate.BeginRead(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            Equal(1, gate.PendingLivePreviewScannerReadyPoll);
            ++now;
            True(gate.CompleteRead((byte)AdapterStatus.Success, 0, 2,
                new byte[] { 0x08, 0x00 }));

            while (gate.LivePreviewImageRows <
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamRows)
            {
                gate.BeginLivePreviewImageShortRetryRead(5, 0, cdb, 3996);
                Equal(AspiLivePreviewImageCompletion.FullRow,
                    gate.CompleteLivePreviewImageShortRetryRead(
                        (byte)AdapterStatus.Success, 3996, 0, 3996,
                        new byte[3996]));
            }
            Equal(256, gate.LivePreviewImageRows);
            Equal(257, gate.LivePreviewImageReadSubmissions);
            Equal(AspiOperationalSequenceState.
                PredictedPreviewImageReadObserved, gate.State);
            ExpectInvalid(delegate
            {
                gate.BeginLivePreviewImageShortRetryRead(5, 0, cdb, 3996);
            });
            Equal(AspiOperationalSequenceState.Failed, gate.State);
        }

        private static void TestWinUsbShortRetryScannerReadyProgress()
        {
            Usb2XchangeDevice.
                ValidatePrecisionTwoPreviewStreamScannerReadyProgress(
                    true, 192, 0, 180, 192, 0,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamRows,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamMaximumReadSubmissions,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamMaximumShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamMaximumScannerReadyPolls);
            Usb2XchangeDevice.
                ValidatePrecisionTwoPreviewStreamScannerReadyProgress(
                    true, 1000, 500, 900, 1000, 500,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumReadSubmissions,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumScannerReadyPolls);
            Usb2XchangeDevice.
                ValidatePrecisionTwoPreviewStreamScannerReadyProgress(
                    true, 1800, 500, 900, 1800, 500,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalPerRowMaximumReadSubmissions,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalPerRowMaximumShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumScannerReadyPolls);
            Usb2XchangeDevice.
                ValidatePrecisionTwoPreviewStreamScannerReadyProgress(
                    true, 1500, 500, 900, 1500, 500,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalRows,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalMaximumShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumScannerReadyPolls);
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoPreviewStreamScannerReadyProgress(
                        true, 1000, 0, 900, 1000, 0,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewNaturalRows,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewNaturalMaximumReadSubmissions,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewNaturalMaximumShortRetries,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewStreamMaximumScannerReadyPolls);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoPreviewStreamScannerReadyProgress(
                        true, 1000, 0, 900, 1000, 0,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewNaturalRows,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewNaturalPerRowMaximumReadSubmissions,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewNaturalMaximumShortRetries,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewNaturalMaximumScannerReadyPolls);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoPreviewStreamScannerReadyProgress(
                        true, 192, 0, 180, 180, 0,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewStreamRows,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewStreamMaximumReadSubmissions,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewStreamMaximumShortRetries,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewStreamMaximumScannerReadyPolls);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoPreviewStreamScannerReadyProgress(
                        true, 245, 0, 180, 245, 0,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewStreamRows,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewStreamMaximumReadSubmissions,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewStreamMaximumShortRetries,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewStreamMaximumScannerReadyPolls);
            });
        }

        private static void TestLivePreviewShortRetryFailures()
        {
            byte[] setWindow = PreviewSetWindowData();
            byte[] cdb = ScsiFraming.
                BuildPrecisionTwoPreviewImageReadCdb(666);

            long now = 0;
            AspiOperationalSequenceGate consecutive =
                ArmLivePreviewShortRetryReadinessGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(consecutive,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(consecutive,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            for (int retry = 0; retry < PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewStreamMaximumConsecutiveShortRetries;
                    ++retry)
            {
                consecutive.BeginLivePreviewImageShortRetryRead(5, 0, cdb,
                    3996);
                Equal(AspiLivePreviewImageCompletion.RetryShortRow,
                    consecutive.CompleteLivePreviewImageShortRetryRead(
                        (byte)AdapterStatus.Success, 3996, 3986, 10,
                        new byte[10]));
            }
            Equal(8, consecutive.LivePreviewImageReadSubmissions);
            ExpectInvalid(delegate
            {
                consecutive.BeginLivePreviewImageShortRetryRead(5, 0, cdb,
                    3996);
            });
            Equal(8, consecutive.LivePreviewImageReadSubmissions);
            Equal(AspiOperationalSequenceState.Failed, consecutive.State);

            now = 0;
            AspiOperationalSequenceGate total =
                ArmLivePreviewShortRetryReadinessGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(total,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(total,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            for (int retry = 0; retry <
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamMaximumShortRetries; ++retry)
            {
                total.BeginLivePreviewImageShortRetryRead(5, 0, cdb, 3996);
                Equal(AspiLivePreviewImageCompletion.RetryShortRow,
                    total.CompleteLivePreviewImageShortRetryRead(
                        (byte)AdapterStatus.Success, 3996, 3986, 10,
                        new byte[10]));
                if (retry + 1 < PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamMaximumShortRetries)
                {
                    total.BeginLivePreviewImageShortRetryRead(5, 0, cdb,
                        3996);
                    Equal(AspiLivePreviewImageCompletion.FullRow,
                        total.CompleteLivePreviewImageShortRetryRead(
                            (byte)AdapterStatus.Success, 3996, 0, 3996,
                            new byte[3996]));
                }
            }
            Equal(64, total.LivePreviewShortRetries);
            Equal(127, total.LivePreviewImageReadSubmissions);
            ExpectInvalid(delegate
            {
                total.BeginLivePreviewImageShortRetryRead(5, 0, cdb, 3996);
            });
            Equal(127, total.LivePreviewImageReadSubmissions);
            Equal(AspiOperationalSequenceState.Failed, total.State);

            AssertShortRetryCompletionRejected(
                (byte)AdapterStatus.Success, 3985, new byte[11]);
            AssertShortRetryCompletionRejected(
                (byte)AdapterStatus.Success, 3985, new byte[10]);
            AssertShortRetryCompletionRejected(
                (byte)AdapterStatus.Busy, 3986, new byte[10]);

            now = 0;
            AspiOperationalSequenceGate timed =
                ArmLivePreviewShortRetryReadinessGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(timed,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(timed,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            timed.BeginLivePreviewImageShortRetryRead(5, 0, cdb, 3996);
            timed.CompleteLivePreviewImageShortRetryRead(
                (byte)AdapterStatus.Success, 3996, 0, 3996,
                new byte[3996]);
            now += AspiOperationalSequenceGate.
                MaximumLivePreviewStreamMilliseconds;
            ExpectProtocol(delegate
            {
                timed.BeginLivePreviewImageShortRetryRead(5, 0, cdb, 3996);
            });
            Equal(AspiOperationalSequenceState.Failed, timed.State);
        }

        private static void AssertShortRetryCompletionRejected(byte status,
            uint residue, byte[] data)
        {
            long now = 0;
            byte[] setWindow = PreviewSetWindowData();
            AspiOperationalSequenceGate gate =
                ArmLivePreviewShortRetryReadinessGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            gate.BeginLivePreviewImageShortRetryRead(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewImageReadCdb(666), 3996);
            Equal(1, gate.LivePreviewImageReadSubmissions);
            ExpectInvalid(delegate
            {
                gate.CompleteLivePreviewImageShortRetryRead(status, 3996,
                    residue, data.Length, data);
            });
            Equal(1, gate.LivePreviewImageReadSubmissions);
            Equal(AspiOperationalSequenceState.Failed, gate.State);
        }

        private static void TestLivePreviewNaturalBoundaryGate()
        {
            long now = 0;
            byte[] setWindow = PreviewSetWindowData();
            AspiOperationalSequenceGate gate =
                ArmLivePreviewNaturalReadinessGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            byte[] cdb = ScsiFraming.
                BuildPrecisionTwoPreviewImageReadCdb(666);
            byte[] fullRow = new byte[3996];
            byte[] shortRow = new byte[10];

            for (int row = 1; row <= PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalRows; ++row)
            {
                int shorts = row == 257 ? 2 : row == 900 ? 1 : 0;
                for (int retry = 0; retry < shorts; ++retry)
                {
                    gate.BeginLivePreviewImageShortRetryRead(
                        5, 0, cdb, 3996);
                    Equal(AspiLivePreviewImageCompletion.RetryShortRow,
                        gate.CompleteLivePreviewImageShortRetryRead(
                            (byte)AdapterStatus.Success, 3996, 3986, 10,
                            shortRow));
                }
                gate.BeginLivePreviewImageShortRetryRead(5, 0, cdb, 3996);
                Equal(AspiLivePreviewImageCompletion.FullRow,
                    gate.CompleteLivePreviewImageShortRetryRead(
                        (byte)AdapterStatus.Success, 3996, 0, 3996,
                        fullRow));
                if (row == 180)
                {
                    gate.BeginRead(5, 0,
                        ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
                    ++now;
                    True(gate.CompleteRead((byte)AdapterStatus.Success, 0,
                        2, new byte[] { 0x08, 0x00 }));
                    gate.BeginRead(5, 0,
                        ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
                    ++now;
                    True(gate.CompleteRead((byte)AdapterStatus.Success, 0,
                        2, new byte[2]));
                }
            }

            Equal(996, gate.LivePreviewImageRows);
            Equal(999, gate.LivePreviewImageReadSubmissions);
            Equal(3, gate.LivePreviewShortRetries);
            Equal(AspiOperationalSequenceState.
                PredictedPreviewImageReadObserved, gate.State);
            ExpectInvalid(delegate
            {
                gate.BeginLivePreviewImageShortRetryRead(5, 0, cdb, 3996);
            });
            Equal(AspiOperationalSequenceState.Failed, gate.State);

            ExpectArgument(delegate
            {
                new WinUsbAspiTransport(null, false, true, true,
                    Sha256Hex(setWindow),
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows, false);
            });
        }

        private static void TestLivePreviewNaturalPerRowBoundaryGate()
        {
            long now = 0;
            byte[] setWindow = PreviewSetWindowData();
            AspiOperationalSequenceGate gate =
                ArmLivePreviewNaturalPerRowReadinessGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            byte[] cdb = ScsiFraming.
                BuildPrecisionTwoPreviewImageReadCdb(666);
            byte[] fullRow = new byte[3996];
            byte[] shortRow = new byte[10];

            for (int row = 1; row <= PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalRows; ++row)
            {
                if (row <= 600)
                {
                    gate.BeginLivePreviewImageShortRetryRead(
                        5, 0, cdb, 3996);
                    Equal(AspiLivePreviewImageCompletion.RetryShortRow,
                        gate.CompleteLivePreviewImageShortRetryRead(
                            (byte)AdapterStatus.Success, 3996, 3986, 10,
                            shortRow));
                }
                gate.BeginLivePreviewImageShortRetryRead(5, 0, cdb, 3996);
                Equal(AspiLivePreviewImageCompletion.FullRow,
                    gate.CompleteLivePreviewImageShortRetryRead(
                        (byte)AdapterStatus.Success, 3996, 0, 3996,
                        fullRow));
            }

            Equal(996, gate.LivePreviewImageRows);
            Equal(1596, gate.LivePreviewImageReadSubmissions);
            Equal(600, gate.LivePreviewShortRetries);
            Equal(AspiOperationalSequenceState.
                PredictedPreviewImageReadObserved, gate.State);
            ExpectInvalid(delegate
            {
                gate.BeginLivePreviewImageShortRetryRead(5, 0, cdb, 3996);
            });

            now = 0;
            AspiOperationalSequenceGate consecutive =
                ArmLivePreviewNaturalPerRowReadinessGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(consecutive,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(consecutive,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            for (int retry = 0; retry < 8; ++retry)
            {
                consecutive.BeginLivePreviewImageShortRetryRead(
                    5, 0, cdb, 3996);
                Equal(AspiLivePreviewImageCompletion.RetryShortRow,
                    consecutive.CompleteLivePreviewImageShortRetryRead(
                        (byte)AdapterStatus.Success, 3996, 3986, 10,
                        shortRow));
            }
            ExpectInvalid(delegate
            {
                consecutive.BeginLivePreviewImageShortRetryRead(
                    5, 0, cdb, 3996);
            });

            var transport = new WinUsbAspiTransport(null, false, true, true,
                Sha256Hex(setWindow),
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalRows, true,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalPerRowMaximumReadSubmissions,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalPerRowMaximumShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalMaximumConsecutiveShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalMaximumScannerReadyPolls,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalMaximumMilliseconds);
            Equal(8964, transport.PreviewMaximumReadSubmissions);
            Equal(7968, transport.PreviewMaximumShortRetries);
            transport.Dispose();

            ExpectArgument(delegate
            {
                new WinUsbAspiTransport(null, false, true, true,
                    Sha256Hex(setWindow),
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows, true,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalPerRowMaximumReadSubmissions,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumConsecutiveShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumScannerReadyPolls,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumMilliseconds);
            });
        }

        private static void TestLivePreviewPoweredNaturalBoundaryGate()
        {
            long now = 0;
            byte[] setWindow = PreviewSetWindowData();
            AspiOperationalSequenceGate gate =
                ArmLivePreviewPoweredNaturalReadinessGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            byte[] cdb = ScsiFraming.
                BuildPrecisionTwoPreviewImageReadCdb(666);
            byte[] fullRow = new byte[3996];
            byte[] shortRow = new byte[10];

            for (int row = 1; row <= PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows; ++row)
            {
                if (row <= 700)
                {
                    gate.BeginLivePreviewImageShortRetryRead(
                        5, 0, cdb, 3996);
                    Equal(AspiLivePreviewImageCompletion.RetryShortRow,
                        gate.CompleteLivePreviewImageShortRetryRead(
                            (byte)AdapterStatus.Success, 3996, 3986, 10,
                            shortRow));
                }
                gate.BeginLivePreviewImageShortRetryRead(5, 0, cdb, 3996);
                Equal(AspiLivePreviewImageCompletion.FullRow,
                    gate.CompleteLivePreviewImageShortRetryRead(
                        (byte)AdapterStatus.Success, 3996, 0, 3996,
                        fullRow));
            }

            Equal(911, gate.LivePreviewImageRows);
            Equal(1611, gate.LivePreviewImageReadSubmissions);
            Equal(700, gate.LivePreviewShortRetries);
            Equal(AspiOperationalSequenceState.
                PredictedPreviewImageReadObserved, gate.State);
            ExpectInvalid(delegate
            {
                gate.BeginLivePreviewImageShortRetryRead(5, 0, cdb, 3996);
            });

            var transport = new WinUsbAspiTransport(null, false, true, true,
                Sha256Hex(setWindow),
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
                    AspiLivePreviewNaturalMaximumMilliseconds);
            Equal(911, transport.PreviewImageReadLimit);
            Equal(8199, transport.PreviewMaximumReadSubmissions);
            Equal(7288, transport.PreviewMaximumShortRetries);
            transport.Dispose();

            long consecutiveNow = 0;
            AspiOperationalSequenceGate consecutiveGate =
                ArmLivePreviewPoweredNaturalReadinessGate(
                    delegate { return consecutiveNow; }, setWindow,
                    ref consecutiveNow);
            CompleteOperationalRead(consecutiveGate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref consecutiveNow);
            CompleteOperationalRead(consecutiveGate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref consecutiveNow);
            for (int retry = 0; retry <
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredMaximumConsecutiveShortRetries;
                    ++retry)
            {
                consecutiveGate.BeginLivePreviewImageShortRetryRead(
                    5, 0, cdb, 3996);
                Equal(AspiLivePreviewImageCompletion.RetryShortRow,
                    consecutiveGate.CompleteLivePreviewImageShortRetryRead(
                        (byte)AdapterStatus.Success, 3996, 3986, 10,
                        shortRow));
            }
            Equal(32, consecutiveGate.LivePreviewConsecutiveShortRetries);
            ExpectInvalid(delegate
            {
                consecutiveGate.BeginLivePreviewImageShortRetryRead(
                    5, 0, cdb, 3996);
            });

            ExpectArgument(delegate
            {
                new WinUsbAspiTransport(null, false, true, true,
                    Sha256Hex(setWindow),
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalRows, true,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalPerRowMaximumShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumConsecutiveShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumScannerReadyPolls,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumMilliseconds);
            });
        }

        private static void TestLiveFullScanSuccessorGate()
        {
            long now = 0;
            byte[] setWindow = PreviewSetWindowData();
            AspiOperationalSequenceGate startupGate =
                ArmLiveFullScanInitialReadyGate(
                    delegate { return now; }, setWindow, ref now);
            Equal(AspiOperationalSequenceState.AwaitInitialScannerReady,
                startupGate.State);
            byte[] scannerReady =
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb();
            long initialReadyStarted = now;
            startupGate.BeginRead(5, 0, scannerReady, 2);
            Equal(1, startupGate.
                PendingLiveFullScanInitialScannerReadyAttempt);
            Equal((long)0, startupGate.
                PendingLiveFullScanInitialScannerReadyElapsedMilliseconds);
            ++now;
            True(!startupGate.CompleteRead(
                (byte)AdapterStatus.Success, 0, 2,
                new byte[] { 0x08, 0x00 }));
            Equal(AspiOperationalSequenceState.AwaitInitialScannerReady,
                startupGate.State);
            now = initialReadyStarted + 255;
            startupGate.BeginRead(5, 0, scannerReady, 2);
            Equal(2, startupGate.
                PendingLiveFullScanInitialScannerReadyAttempt);
            Equal((long)255, startupGate.
                PendingLiveFullScanInitialScannerReadyElapsedMilliseconds);
            ++now;
            True(startupGate.CompleteRead(
                (byte)AdapterStatus.Success, 0, 2, new byte[2]));
            Equal(AspiOperationalSequenceState.AwaitFaultPixelHeader,
                startupGate.State);
            CompleteInitializationCycle(startupGate, 2, ref now);
            Equal(2, startupGate.CompletedInitializationCycles);
            startupGate.ObserveLiveFullScanStartupWrite(5, 0,
                ScsiFraming.BuildPrecisionTwoStartupWriteBuffer1082Cdb(),
                new byte[ScsiFraming.
                    PrecisionTwoStartupWriteBuffer1082Length]);
            Equal(AspiOperationalSequenceState.Failed, startupGate.State);

            now = 0;
            AspiOperationalSequenceGate windowGate =
                ArmLiveFullScanInitialReadyGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(windowGate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteInitializationCycle(windowGate, 0, ref now);
            byte[] changedWindow = (byte[])setWindow.Clone();
            changedWindow[83] ^= 1;
            windowGate.ObserveLiveFullScanSetWindowSuccessor(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                changedWindow);
            Equal(AspiOperationalSequenceState.Failed, windowGate.State);

            now = 0;
            AspiOperationalSequenceGate early =
                ArmLivePreviewPoweredNaturalReadinessGate(
                    delegate { return now; }, setWindow, ref now);
            ExpectInvalid(delegate
            {
                early.BeginLiveFullScanSuccessorObservation();
            });

            now = 0;
            AspiOperationalSequenceGate executionGate =
                ArmLiveFullScanInitialReadyGate(
                    delegate { return now; }, setWindow, ref now);
            CompleteOperationalRead(executionGate, scannerReady,
                new byte[2], ref now);
            CompleteInitializationCycle(executionGate, 0, ref now);
            executionGate.BeginLiveFullScanSetWindowExecution(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow, Sha256Hex(setWindow));
            executionGate.RecordPreviewSetWindowCompletion(
                (byte)AdapterStatus.Success, 0, 84);
            Equal(AspiOperationalSequenceState.
                AwaitFirstPostWindowScannerReady, executionGate.State);
            CompleteOperationalRead(executionGate, scannerReady,
                new byte[2], ref now);
            CompleteOperationalRead(executionGate, scannerReady,
                new byte[2], ref now);
            byte[] fullScanRead = ScsiFraming.
                BuildPrecisionTwoPreviewImageReadCdb(777);
            Equal((uint)777,
                executionGate.ObserveLiveFullScanFirstImageRead(
                    5, 0, fullScanRead, 4662));
            Equal(AspiOperationalSequenceState.Failed,
                executionGate.State);

            now = 0;
            AspiOperationalSequenceGate streamGate =
                ArmLiveFullScanImageStreamGate(
                    delegate { return now; }, setWindow, ref now);
            byte[] authenticFullScanRead = ScsiFraming.
                BuildPrecisionTwoPreviewImageReadCdb(
                    PrecisionTwoPreviewCommandManifest.AspiLiveFullScanWidth);
            byte[] authenticFullScanRow = new byte[
                PrecisionTwoPreviewCommandManifest.AspiLiveFullScanLength];
            for (int rowIndex = 0; rowIndex <
                    PrecisionTwoPreviewCommandManifest.AspiLiveFullScanRows;
                    ++rowIndex)
            {
                Equal(PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanWidth,
                    streamGate.BeginLivePreviewImageShortRetryRead(5, 0,
                        authenticFullScanRead,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanLength));
                Equal(AspiLivePreviewImageCompletion.FullRow,
                    streamGate.CompleteLivePreviewImageShortRetryRead(
                        (byte)AdapterStatus.Success,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanLength, 0,
                        checked((int)PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanLength), authenticFullScanRow));
            }
            Equal(PrecisionTwoPreviewCommandManifest.AspiLiveFullScanRows,
                streamGate.LivePreviewImageRows);
            Equal(AspiOperationalSequenceState.PreviewImageStreaming,
                streamGate.State);
            CompleteOperationalRead(streamGate, scannerReady,
                new byte[2], ref now);
            Equal(AspiOperationalSequenceState.PreviewImageStreaming,
                streamGate.State);

            now = 0;
            AspiOperationalSequenceGate naturalStreamGate =
                ArmLiveFullScanImageStreamGate(
                    delegate { return now; }, setWindow, ref now, true);
            for (int rowIndex = 0; rowIndex <
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanNaturalRows; ++rowIndex)
            {
                naturalStreamGate.BeginLivePreviewImageShortRetryRead(5, 0,
                    authenticFullScanRead,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanLength);
                naturalStreamGate.CompleteLivePreviewImageShortRetryRead(
                    (byte)AdapterStatus.Success,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanLength, 0,
                    authenticFullScanRow.Length, authenticFullScanRow);
            }
            Equal(PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanNaturalRows,
                naturalStreamGate.LivePreviewImageRows);
            Equal(PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanNaturalMaximumReadSubmissions,
                naturalStreamGate.LiveFullScanMaximumReadSubmissions);
            ExpectInvalid(delegate
            {
                naturalStreamGate.BeginLivePreviewImageShortRetryRead(5, 0,
                    authenticFullScanRead,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanLength);
            });

            now = 0;
            AspiOperationalSequenceGate row997StreamGate =
                ArmLiveFullScanImageStreamGate(
                    delegate { return now; }, setWindow, ref now, false, true);
            for (int rowIndex = 0; rowIndex <
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanRow997CompletionRows; ++rowIndex)
            {
                row997StreamGate.BeginLivePreviewImageShortRetryRead(5, 0,
                    authenticFullScanRead,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanLength);
                row997StreamGate.CompleteLivePreviewImageShortRetryRead(
                    (byte)AdapterStatus.Success,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanLength, 0,
                    authenticFullScanRow.Length, authenticFullScanRow);
            }
            Equal(PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanRow997CompletionRows,
                row997StreamGate.LivePreviewImageRows);
            Equal(PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanRow997CompletionMaximumReadSubmissions,
                row997StreamGate.LiveFullScanMaximumReadSubmissions);
            ExpectInvalid(delegate
            {
                row997StreamGate.BeginLivePreviewImageShortRetryRead(5, 0,
                    authenticFullScanRead,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanLength);
            });

            now = 0;
            AspiOperationalSequenceGate progressStreamGate =
                ArmLiveFullScanImageStreamGate(
                    delegate { return now; }, setWindow, ref now, false,
                    false, true);
            Equal(PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanProgressMaximumRows,
                progressStreamGate.LiveFullScanImageRowLimit);
            Equal(PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanProgressMaximumReadSubmissions,
                progressStreamGate.LiveFullScanMaximumReadSubmissions);
            Equal(PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanProgressMaximumShortRetries,
                progressStreamGate.LiveFullScanMaximumShortRetries);
            for (int rowIndex = 0; rowIndex <
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanProgressMaximumRows; ++rowIndex)
            {
                progressStreamGate.BeginLivePreviewImageShortRetryRead(5, 0,
                    authenticFullScanRead,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanLength);
                progressStreamGate.CompleteLivePreviewImageShortRetryRead(
                    (byte)AdapterStatus.Success,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanLength, 0,
                    authenticFullScanRow.Length, authenticFullScanRow);
            }
            Equal(998, progressStreamGate.LivePreviewImageRows);
            ExpectInvalid(delegate
            {
                progressStreamGate.BeginLivePreviewImageShortRetryRead(5, 0,
                    authenticFullScanRead,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanLength);
            });

            now = 0;
            AspiOperationalSequenceGate streamTimeBound =
                ArmLiveFullScanImageStreamGate(
                    delegate { return now; }, setWindow, ref now);
            long fullScanStreamStarted = now;
            streamTimeBound.BeginLivePreviewImageShortRetryRead(5, 0,
                authenticFullScanRead,
                PrecisionTwoPreviewCommandManifest.AspiLiveFullScanLength);
            streamTimeBound.CompleteLivePreviewImageShortRetryRead(
                (byte)AdapterStatus.Success,
                PrecisionTwoPreviewCommandManifest.AspiLiveFullScanLength,
                0, authenticFullScanRow.Length, authenticFullScanRow);
            now = fullScanStreamStarted +
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumMilliseconds - 1;
            streamTimeBound.BeginLivePreviewImageShortRetryRead(5, 0,
                authenticFullScanRead,
                PrecisionTwoPreviewCommandManifest.AspiLiveFullScanLength);
            streamTimeBound.CompleteLivePreviewImageShortRetryRead(
                (byte)AdapterStatus.Success,
                PrecisionTwoPreviewCommandManifest.AspiLiveFullScanLength,
                0, authenticFullScanRow.Length, authenticFullScanRow);
            now = fullScanStreamStarted +
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumMilliseconds;
            ExpectProtocol(delegate
            {
                streamTimeBound.BeginLivePreviewImageShortRetryRead(5, 0,
                    authenticFullScanRead,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanLength);
            });

            now = 0;
            AspiOperationalSequenceGate wrongActive =
                ArmLiveFullScanInitialReadyGate(
                    delegate { return now; }, setWindow, ref now);
            wrongActive.BeginRead(5, 0, scannerReady, 2);
            ExpectProtocol(delegate
            {
                wrongActive.CompleteRead((byte)AdapterStatus.Success, 0, 2,
                    new byte[] { 0x08, 0x01 });
            });

            now = 0;
            AspiOperationalSequenceGate attemptBound =
                ArmLiveFullScanInitialReadyGate(
                    delegate { return now; }, setWindow, ref now);
            for (int attempt = 1; attempt <=
                    AspiOperationalSequenceGate.
                        MaximumPostWindowScannerReadyAttemptsPerPhase;
                    ++attempt)
            {
                attemptBound.BeginRead(5, 0, scannerReady, 2);
                Equal(attempt, attemptBound.
                    PendingLiveFullScanInitialScannerReadyAttempt);
                True(!attemptBound.CompleteRead(
                    (byte)AdapterStatus.Success, 0, 2,
                    new byte[] { 0x08, 0x00 }));
            }
            ExpectProtocol(delegate
            {
                attemptBound.BeginRead(5, 0, scannerReady, 2);
            });

            now = 0;
            AspiOperationalSequenceGate timeBound =
                ArmLiveFullScanInitialReadyGate(
                    delegate { return now; }, setWindow, ref now);
            long timeBoundStarted = now;
            timeBound.BeginRead(5, 0, scannerReady, 2);
            True(!timeBound.CompleteRead(
                (byte)AdapterStatus.Success, 0, 2,
                new byte[] { 0x08, 0x00 }));
            now = timeBoundStarted + AspiOperationalSequenceGate.
                MaximumPostWindowScannerReadyPhaseMilliseconds;
            ExpectProtocol(delegate
            {
                timeBound.BeginRead(5, 0, scannerReady, 2);
            });
        }

        private static void TestLiveFullScanTerminalProbeGate()
        {
            long now = 0;
            byte[] setWindow = PreviewSetWindowData();
            AspiOperationalSequenceGate gate =
                ArmLiveFullScanImageStreamGate(
                    delegate { return now; }, setWindow, ref now);
            byte[] cdb = ScsiFraming.BuildPrecisionTwoPreviewImageReadCdb(
                PrecisionTwoPreviewCommandManifest.AspiLiveFullScanWidth);
            byte[] row = new byte[
                PrecisionTwoPreviewCommandManifest.AspiLiveFullScanLength];

            ExpectInvalid(delegate
            {
                gate.BeginLiveFullScanTerminalProbeRead(5, 0, cdb,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanLength);
            });
            Equal(AspiOperationalSequenceState.Failed, gate.State);

            now = 0;
            gate = ArmLiveFullScanImageStreamGate(
                delegate { return now; }, setWindow, ref now);
            for (int rowIndex = 0; rowIndex <
                    PrecisionTwoPreviewCommandManifest.AspiLiveFullScanRows;
                    ++rowIndex)
            {
                gate.BeginLivePreviewImageShortRetryRead(5, 0, cdb,
                    PrecisionTwoPreviewCommandManifest.AspiLiveFullScanLength);
                gate.CompleteLivePreviewImageShortRetryRead(
                    (byte)AdapterStatus.Success,
                    PrecisionTwoPreviewCommandManifest.AspiLiveFullScanLength,
                    0, row.Length, row);
            }
            Equal(PrecisionTwoPreviewCommandManifest.AspiLiveFullScanRows,
                gate.LivePreviewImageRows);
            Equal(PrecisionTwoPreviewCommandManifest.AspiLiveFullScanRows,
                gate.LivePreviewImageReadSubmissions);
            Equal(PrecisionTwoPreviewCommandManifest.AspiLiveFullScanWidth,
                gate.BeginLiveFullScanTerminalProbeRead(5, 0, cdb,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanLength));
            Equal(PrecisionTwoPreviewCommandManifest.AspiLiveFullScanRows + 1,
                gate.LivePreviewImageReadSubmissions);
            byte[] shortResult = new byte[
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewObservedShortLength];
            gate.RecordLiveFullScanTerminalProbeCompletion(
                (byte)AdapterStatus.Success,
                PrecisionTwoPreviewCommandManifest.AspiLiveFullScanLength,
                PrecisionTwoPreviewCommandManifest.AspiLiveFullScanLength -
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewObservedShortLength,
                shortResult.Length, shortResult);
            True(gate.LiveFullScanTerminalProbeObserved);
            Equal(PrecisionTwoPreviewCommandManifest.AspiLiveFullScanRows,
                gate.LivePreviewImageRows);
            Equal(AspiOperationalSequenceState.Failed, gate.State);
            ExpectInvalid(delegate
            {
                gate.BeginLiveFullScanTerminalProbeRead(5, 0, cdb,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanLength);
            });

            Usb2XchangeDevice.
                ValidatePrecisionTwoFullScanTerminalProbeProgress(true, 886,
                    false,
                    PrecisionTwoPreviewCommandManifest.AspiLiveFullScanRows,
                    886);
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoFullScanTerminalProbeProgress(true,
                        886, true,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanRows, 886);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoFullScanTerminalProbeProgress(true,
                        761, false, 761, 761);
            });
        }

        private static void TestLiveFullScanInquiryGate()
        {
            byte[] inquiry = ScsiFraming.BuildInquiryCdb(96);
            var gate = new AspiFullScanInquiryGate();
            gate.Arm(PrecisionTwoPreviewCommandManifest.
                AspiLivePreviewPoweredNaturalRows);
            Equal(AspiFullScanInquiryKind.Prefix,
                gate.Begin(5, 0, inquiry, 96, false,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalRows));
            gate.Complete(AspiFullScanInquiryKind.Prefix,
                (byte)AdapterStatus.Success, 96, 0, true);
            True(gate.PrefixCompleted);
            Equal(0, gate.InStreamRevalidations);

            Equal(AspiFullScanInquiryKind.InStreamRevalidation,
                gate.Begin(5, 0, inquiry, 96, true, 237));
            gate.Complete(AspiFullScanInquiryKind.InStreamRevalidation,
                (byte)AdapterStatus.Success, 96, 0, true);
            Equal(1, gate.InStreamRevalidations);
            Equal(237, gate.LastInStreamRevalidationRow);
            ExpectInvalid(delegate
            {
                gate.Begin(5, 0, inquiry, 96, true, 237);
            });

            var bound = ReadyFullScanInquiryGate(inquiry);
            for (int ordinal = 1; ordinal <=
                    AspiFullScanInquiryGate.MaximumInStreamRevalidations;
                    ++ordinal)
            {
                Equal(AspiFullScanInquiryKind.InStreamRevalidation,
                    bound.Begin(5, 0, inquiry, 96, true, ordinal));
                bound.Complete(
                    AspiFullScanInquiryKind.InStreamRevalidation,
                    (byte)AdapterStatus.Success, 96, 0, true);
            }
            ExpectInvalid(delegate
            {
                bound.Begin(5, 0, inquiry, 96, true,
                    AspiFullScanInquiryGate.
                        MaximumInStreamRevalidations + 1);
            });

            var natural = new AspiFullScanInquiryGate();
            natural.Arm(PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanNaturalRows);
            Equal(AspiFullScanInquiryGate.
                NaturalMaximumInStreamRevalidations,
                natural.MaximumAllowedInStreamRevalidations);
            natural.Begin(5, 0, inquiry, 96, false,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows);
            natural.Complete(AspiFullScanInquiryKind.Prefix,
                (byte)AdapterStatus.Success, 96, 0, true);
            Equal(AspiFullScanInquiryKind.InStreamRevalidation,
                natural.Begin(5, 0, inquiry, 96, true, 995));
            natural.Complete(AspiFullScanInquiryKind.InStreamRevalidation,
                (byte)AdapterStatus.Success, 96, 0, true);
            ExpectInvalid(delegate
            {
                natural.Begin(5, 0, inquiry, 96, true, 996);
            });

            var row997 = new AspiFullScanInquiryGate();
            row997.Arm(PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanRow997CompletionRows);
            Equal(AspiFullScanInquiryGate.
                Row997CompletionMaximumInStreamRevalidations,
                row997.MaximumAllowedInStreamRevalidations);
            row997.Begin(5, 0, inquiry, 96, false,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows);
            row997.Complete(AspiFullScanInquiryKind.Prefix,
                (byte)AdapterStatus.Success, 96, 0, true);
            Equal(AspiFullScanInquiryKind.InStreamRevalidation,
                row997.Begin(5, 0, inquiry, 96, true, 996));
            row997.Complete(AspiFullScanInquiryKind.InStreamRevalidation,
                (byte)AdapterStatus.Success, 96, 0, true);
            ExpectInvalid(delegate
            {
                row997.Begin(5, 0, inquiry, 96, true, 997);
            });

            var progress = new AspiFullScanInquiryGate();
            progress.Arm(PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanProgressMaximumRows);
            Equal(AspiFullScanInquiryGate.
                ProgressMaximumInStreamRevalidations,
                progress.MaximumAllowedInStreamRevalidations);
            Equal(34, progress.MaximumAllowedInStreamRevalidations);
            progress.Begin(5, 0, inquiry, 96, false,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows);
            progress.Complete(AspiFullScanInquiryKind.Prefix,
                (byte)AdapterStatus.Success, 96, 0, true);
            Equal(AspiFullScanInquiryKind.InStreamRevalidation,
                progress.Begin(5, 0, inquiry, 96, true, 997));
            progress.Complete(AspiFullScanInquiryKind.InStreamRevalidation,
                (byte)AdapterStatus.Success, 96, 0, true);
            ExpectInvalid(delegate
            {
                progress.Begin(5, 0, inquiry, 96, true, 998);
            });

            var wrongEnvelope = new AspiFullScanInquiryGate();
            wrongEnvelope.Arm(PrecisionTwoPreviewCommandManifest.
                AspiLivePreviewPoweredNaturalRows);
            ExpectInvalid(delegate
            {
                wrongEnvelope.Begin(5, 0,
                    ScsiFraming.BuildInquiryCdb(36), 36, false,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalRows);
            });

            var wrongIdentity = new AspiFullScanInquiryGate();
            wrongIdentity.Arm(PrecisionTwoPreviewCommandManifest.
                AspiLivePreviewPoweredNaturalRows);
            wrongIdentity.Begin(5, 0, inquiry, 96, false,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows);
            ExpectProtocol(delegate
            {
                wrongIdentity.Complete(AspiFullScanInquiryKind.Prefix,
                    (byte)AdapterStatus.Success, 96, 0, false);
            });

            var tooEarly = ReadyFullScanInquiryGate(inquiry);
            ExpectInvalid(delegate
            {
                tooEarly.Begin(5, 0, inquiry, 96, true, 0);
            });
        }

        private static AspiFullScanInquiryGate ReadyFullScanInquiryGate(
            byte[] inquiry)
        {
            var gate = new AspiFullScanInquiryGate();
            gate.Arm(PrecisionTwoPreviewCommandManifest.
                AspiLivePreviewPoweredNaturalRows);
            gate.Begin(5, 0, inquiry, 96, false,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows);
            gate.Complete(AspiFullScanInquiryKind.Prefix,
                (byte)AdapterStatus.Success, 96, 0, true);
            return gate;
        }

        private static void TestWinUsbPoweredPreviewCleanupProgress()
        {
            Usb2XchangeDevice.ValidatePrecisionTwoPreviewCleanupProgress(
                true, 2390, 0, 0, 911, 2390);
            Usb2XchangeDevice.ValidatePrecisionTwoPreviewCleanupProgress(
                true, 2390, 1, 1, 911, 2390);
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.ValidatePrecisionTwoPreviewCleanupProgress(
                    false, 2390, 0, 0, 911, 2390);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.ValidatePrecisionTwoPreviewCleanupProgress(
                    true, 2390, 0, 0, 910, 2390);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.ValidatePrecisionTwoPreviewCleanupProgress(
                    true, 2389, 0, 0, 911, 2390);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.ValidatePrecisionTwoPreviewCleanupProgress(
                    true, 8200, 0, 0, 911, 8200);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.ValidatePrecisionTwoPreviewCleanupProgress(
                    true, 2390, 0, 1, 911, 2390);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.ValidatePrecisionTwoPreviewCleanupProgress(
                    true, 2390, 2, 2, 911, 2390);
            });

            Usb2XchangeDevice.
                ValidatePrecisionTwoFullScanSetWindowProgress(
                    true, 2390, 2, false, 911, 2390);
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoFullScanSetWindowProgress(
                        true, 2390, 1, false, 911, 2390);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoFullScanSetWindowProgress(
                        true, 2390, 2, true, 911, 2390);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoFullScanSetWindowProgress(
                        true, 2389, 2, false, 911, 2390);
            });

            Usb2XchangeDevice.ValidatePrecisionTwoFullScanStreamProgress(
                true, 2290, 12, 762, 2290, 12, false);
            Usb2XchangeDevice.ValidatePrecisionTwoFullScanStreamProgress(
                true, 2290, 12, 762, 2290, 12, true);
            Usb2XchangeDevice.ValidatePrecisionTwoFullScanStreamProgress(
                true, 2390, 12, 996, 2390, 12, false,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanNaturalRows,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanNaturalMaximumReadSubmissions,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanNaturalMaximumShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumScannerReadyPolls);
            Usb2XchangeDevice.ValidatePrecisionTwoFullScanStreamProgress(
                true, 2391, 12, 997, 2391, 12, false,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanRow997CompletionRows,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanRow997CompletionMaximumReadSubmissions,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanRow997CompletionMaximumShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumScannerReadyPolls);
            Usb2XchangeDevice.ValidatePrecisionTwoFullScanStreamProgress(
                true, 2391, 12, 997, 2391, 12, false,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanProgressMaximumRows,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanProgressMaximumReadSubmissions,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanProgressMaximumShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumScannerReadyPolls);
            Usb2XchangeDevice.ValidatePrecisionTwoFullScanStreamProgress(
                true, PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanProgressMaximumRows, 12,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanProgressMaximumRows,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanProgressMaximumRows, 12, false,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanProgressMaximumRows,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanProgressMaximumReadSubmissions,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanProgressMaximumShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanMaximumScannerReadyPolls);
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoFullScanStreamProgress(
                        true, 2391, 12, 997, 2391, 12, false,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanProgressMaximumRows,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanNaturalMaximumReadSubmissions,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanProgressMaximumShortRetries,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanMaximumScannerReadyPolls);
            });
            Usb2XchangeDevice.
                ValidatePrecisionTwoFullScanCleanupProgress(
                    998, 998, 8982, 7984, 998, 0, 0);
            Usb2XchangeDevice.
                ValidatePrecisionTwoFullScanCleanupProgress(
                    998, 998, 8982, 7984, 998, 1, 1);
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoFullScanCleanupProgress(
                        997, 998, 8982, 7984, 998, 0, 0);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoFullScanCleanupProgress(
                        998, 998, 8964, 7984, 998, 0, 0);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoFullScanCleanupProgress(
                        997, 997, 8973, 7976, 996, 0, 0);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoFullScanStreamProgress(
                        true, 2390, 12, 996, 2390, 12, false,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanNaturalRows,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanMaximumReadSubmissions,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanNaturalMaximumShortRetries,
                        PrecisionTwoPreviewCommandManifest.
                            AspiLiveFullScanMaximumScannerReadyPolls);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoFullScanStreamProgress(
                        true, 2290, 12, 763, 2290, 12, false);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoFullScanStreamProgress(
                        true, 2289, 12, 762, 2290, 12, false);
            });

            Usb2XchangeDevice.
                ValidatePrecisionTwoPreviewCancellationCleanupProgress(
                    true, 32, 0, 0, 32, 32);
            Usb2XchangeDevice.
                ValidatePrecisionTwoPreviewCancellationCleanupProgress(
                    true, 864, 1, 1, 96, 864);
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoPreviewCancellationCleanupProgress(
                        true, 31, 0, 0, 31, 31);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoPreviewCancellationCleanupProgress(
                        true, 97, 0, 0, 97, 97);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoPreviewCancellationCleanupProgress(
                        true, 865, 0, 0, 96, 865);
            });
            ExpectInvalid(delegate
            {
                Usb2XchangeDevice.
                    ValidatePrecisionTwoPreviewCancellationCleanupProgress(
                        true, 32, 2, 2, 32, 32);
            });

            byte[] setWindow = PreviewSetWindowData();
            var cleanup = new WinUsbAspiTransport(null, false, true, true,
                Sha256Hex(setWindow),
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
                    AspiLivePreviewNaturalMaximumMilliseconds, true);
            True(cleanup.PreviewCleanupExecutionEnabled);
            True(((IAspiPreviewSetWindowValidationPolicy)cleanup).
                StatefulPreviewSetWindowValidationEnabled);
            cleanup.Dispose();

            var cancellation = new WinUsbAspiTransport(null, false, true,
                true, Sha256Hex(setWindow),
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
                    AspiLivePreviewNaturalMaximumMilliseconds, false, true);
            True(!cancellation.PreviewCleanupExecutionEnabled);
            True(cancellation.PreviewCancellationExecutionEnabled);
            True(((IAspiPreviewSetWindowValidationPolicy)cancellation).
                StatefulPreviewSetWindowValidationEnabled);
            cancellation.Dispose();

            ExpectArgument(delegate
            {
                new WinUsbAspiTransport(null, false, true, true,
                    Sha256Hex(setWindow),
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
                        AspiLivePreviewNaturalMaximumMilliseconds, true, true);
            });

            ExpectArgument(delegate
            {
                new WinUsbAspiTransport(null, false, true, true,
                    Sha256Hex(setWindow),
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows, true,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalPerRowMaximumReadSubmissions,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalPerRowMaximumShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumConsecutiveShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumScannerReadyPolls,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumMilliseconds, true);
            });
        }

        private static void TestPreviewStreamCompletedMessage()
        {
            string observer = WinUsbAspiTransport.
                BuildPreviewStreamCompletedMessage(false, 911, 2397, 8199,
                    1486, 7288);
            True(observer.IndexOf(
                "Row 912 and cleanup remain blocked before USB.",
                StringComparison.Ordinal) >= 0);

            string cleanup = WinUsbAspiTransport.
                BuildPreviewStreamCompletedMessage(true, 911, 2397, 8199,
                    1486, 7288);
            True(cleanup.IndexOf(
                "Row 912 remains blocked before USB; exactly two " +
                "hash-pinned cleanup SET WINDOW successors are now eligible.",
                StringComparison.Ordinal) >= 0);
            True(cleanup.IndexOf("and cleanup remain blocked",
                StringComparison.Ordinal) < 0);
        }

        private static AspiOperationalSequenceGate ArmLivePreviewReadinessGate(
            Func<long> clock, byte[] setWindow, ref long now)
        {
            AspiOperationalSequenceGate gate =
                new AspiOperationalSequenceGate(clock,
                    Sha256Hex(setWindow), false, true);
            gate.RecordOperationalD8Completion(
                (byte)AdapterStatus.Success, 0, 66, new byte[66]);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteInitializationCycle(gate, 0, ref now);
            gate.ObservePreviewSetWindow(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow);
            gate.RecordPreviewSetWindowCompletion(
                (byte)AdapterStatus.Success, 0, 84);
            return gate;
        }

        private static AspiOperationalSequenceGate
            ArmLivePreviewStreamReadinessGate(Func<long> clock,
                byte[] setWindow, ref long now)
        {
            AspiOperationalSequenceGate gate =
                new AspiOperationalSequenceGate(clock,
                    Sha256Hex(setWindow), false, true, true);
            gate.RecordOperationalD8Completion(
                (byte)AdapterStatus.Success, 0, 66, new byte[66]);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteInitializationCycle(gate, 0, ref now);
            gate.ObservePreviewSetWindow(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow);
            gate.RecordPreviewSetWindowCompletion(
                (byte)AdapterStatus.Success, 0, 84);
            return gate;
        }

        private static AspiOperationalSequenceGate
            ArmLivePreviewShortRetryReadinessGate(Func<long> clock,
                byte[] setWindow, ref long now)
        {
            AspiOperationalSequenceGate gate =
                new AspiOperationalSequenceGate(clock,
                    Sha256Hex(setWindow), false, true, true, true);
            gate.RecordOperationalD8Completion(
                (byte)AdapterStatus.Success, 0, 66, new byte[66]);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteInitializationCycle(gate, 0, ref now);
            gate.ObservePreviewSetWindow(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow);
            gate.RecordPreviewSetWindowCompletion(
                (byte)AdapterStatus.Success, 0, 84);
            return gate;
        }

        private static AspiOperationalSequenceGate
            ArmLivePreviewNaturalReadinessGate(Func<long> clock,
                byte[] setWindow, ref long now)
        {
            AspiOperationalSequenceGate gate =
                new AspiOperationalSequenceGate(clock,
                    Sha256Hex(setWindow), false, true, true, true,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumReadSubmissions,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumConsecutiveShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumScannerReadyPolls,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumMilliseconds);
            gate.RecordOperationalD8Completion(
                (byte)AdapterStatus.Success, 0, 66, new byte[66]);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteInitializationCycle(gate, 0, ref now);
            gate.ObservePreviewSetWindow(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow);
            gate.RecordPreviewSetWindowCompletion(
                (byte)AdapterStatus.Success, 0, 84);
            return gate;
        }

        private static AspiOperationalSequenceGate
            ArmLivePreviewNaturalPerRowReadinessGate(Func<long> clock,
                byte[] setWindow, ref long now)
        {
            AspiOperationalSequenceGate gate =
                new AspiOperationalSequenceGate(clock,
                    Sha256Hex(setWindow), false, true, true, true,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalPerRowMaximumReadSubmissions,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalPerRowMaximumShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumConsecutiveShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumScannerReadyPolls,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumMilliseconds);
            gate.RecordOperationalD8Completion(
                (byte)AdapterStatus.Success, 0, 66, new byte[66]);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteInitializationCycle(gate, 0, ref now);
            gate.ObservePreviewSetWindow(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow);
            gate.RecordPreviewSetWindowCompletion(
                (byte)AdapterStatus.Success, 0, 84);
            return gate;
        }

        private static AspiOperationalSequenceGate
            ArmLivePreviewPoweredNaturalReadinessGate(Func<long> clock,
                byte[] setWindow, ref long now)
        {
            AspiOperationalSequenceGate gate =
                new AspiOperationalSequenceGate(clock,
                    Sha256Hex(setWindow), false, true, true, true,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalRows,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalMaximumShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredMaximumConsecutiveShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumScannerReadyPolls,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumMilliseconds);
            gate.RecordOperationalD8Completion(
                (byte)AdapterStatus.Success, 0, 66, new byte[66]);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteInitializationCycle(gate, 0, ref now);
            gate.ObservePreviewSetWindow(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow);
            gate.RecordPreviewSetWindowCompletion(
                (byte)AdapterStatus.Success, 0, 84);
            return gate;
        }

        private static AspiOperationalSequenceGate
            ArmLiveFullScanInitialReadyGate(Func<long> clock,
                byte[] setWindow, ref long now)
        {
            AspiOperationalSequenceGate gate =
                ArmLivePreviewPoweredNaturalReadinessGate(
                    clock, setWindow, ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            byte[] rowCdb = ScsiFraming.
                BuildPrecisionTwoPreviewImageReadCdb(666);
            byte[] row = new byte[3996];
            for (int index = 0; index <
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalRows; ++index)
            {
                gate.BeginLivePreviewImageShortRetryRead(
                    5, 0, rowCdb, 3996);
                Equal(AspiLivePreviewImageCompletion.FullRow,
                    gate.CompleteLivePreviewImageShortRetryRead(
                        (byte)AdapterStatus.Success, 3996, 0, 3996, row));
            }
            gate.BeginLiveFullScanSuccessorObservation();
            return gate;
        }

        private static AspiOperationalSequenceGate
            ArmLiveFullScanImageStreamGate(Func<long> clock,
                byte[] setWindow, ref long now,
                bool naturalCompletionEnabled = false,
                bool row997CompletionEnabled = false,
                bool progressCompletionEnabled = false)
        {
            AspiOperationalSequenceGate gate =
                new AspiOperationalSequenceGate(clock,
                    Sha256Hex(setWindow), false, true, true, true,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalRows,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalMaximumShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredMaximumConsecutiveShortRetries,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumScannerReadyPolls,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumMilliseconds);
            gate.RecordOperationalD8Completion(
                (byte)AdapterStatus.Success, 0, 66, new byte[66]);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            for (int cycle = 0; cycle < 5; ++cycle)
            {
                CompleteInitializationCycle(gate, 0, ref now);
            }
            gate.ObservePreviewSetWindow(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow);
            gate.RecordPreviewSetWindowCompletion(
                (byte)AdapterStatus.Success, 0, 84);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            byte[] previewCdb = ScsiFraming.
                BuildPrecisionTwoPreviewImageReadCdb(666);
            byte[] previewRow = new byte[3996];
            for (int row = 0; row < PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows; ++row)
            {
                gate.BeginLivePreviewImageShortRetryRead(5, 0, previewCdb,
                    3996);
                gate.CompleteLivePreviewImageShortRetryRead(
                    (byte)AdapterStatus.Success, 3996, 0, 3996, previewRow);
            }
            gate.BeginLiveFullScanSuccessorObservation();
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteInitializationCycle(gate, 0, ref now);
            gate.BeginLiveFullScanSetWindowExecution(5, 0,
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                setWindow, Sha256Hex(setWindow));
            gate.RecordPreviewSetWindowCompletion(
                (byte)AdapterStatus.Success, 0, 84);
            gate.ArmLiveFullScanImageStream(naturalCompletionEnabled,
                row997CompletionEnabled, progressCompletionEnabled);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            return gate;
        }

        private static AspiOperationalSequenceGate ReadyOperationalGate()
        {
            AspiOperationalSequenceGate gate =
                new AspiOperationalSequenceGate(delegate { return 0; });
            gate.RecordOperationalD8Completion(
                (byte)AdapterStatus.Success, 0, 66, new byte[66]);
            return gate;
        }

        private static void CompleteInitializationCycle(
            AspiOperationalSequenceGate gate, int continuation,
            ref long now)
        {
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoFaultPixelReadBufferCdb(),
                KnownFaultPixelHeader(), ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoFaultPixelDataReadBufferCdb(),
                new byte[58], ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoCalibrationReadBufferCdb(),
                new byte[1024], ref now);
            if (continuation == 1)
            {
                CompleteOperationalRead(gate,
                    ScsiFraming.BuildPrecisionTwoD8Offset55ReadBufferCdb(),
                    new byte[10], ref now);
            }
            else if (continuation == 2)
            {
                CompleteOperationalRead(gate, ScsiFraming.
                    BuildPrecisionTwoDynamicConfigurationD8ReadBufferCdb(),
                    new byte[1024], ref now);
            }
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
        }

        private static void CompleteOperationalRead(
            AspiOperationalSequenceGate gate, byte[] cdb, byte[] data,
            ref long now)
        {
            gate.BeginRead(5, 0, cdb, checked((uint)data.Length));
            ++now;
            gate.CompleteRead((byte)AdapterStatus.Success, 0, data.Length,
                data);
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

        private static byte[] PreviewSetWindowData()
        {
            byte[] data = new byte[84];
            data[7] = 0x4C;
            return data;
        }

        private static void TestLoaderDataOutDisabled()
        {
            FakeDataOutTransport transport = new FakeDataOutTransport();
            byte[] data = new byte[
                PrecisionTwoLoaderRecordManifest.RecordLength];
            IntPtr buffer = Marshal.AllocHGlobal(data.Length);
            IntPtr srb = AllocateSrb();
            try
            {
                Marshal.Copy(data, 0, buffer, data.Length);
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionOut |
                        AspiConstants.FlagEventNotify,
                    checked((uint)data.Length), buffer,
                    ScsiFraming.BuildPrecisionTwoLoaderFirstRecordCdb());
                Equal((uint)AspiConstants.StatusInvalidSrb,
                    new AspiSrbProcessor(transport).Process(srb));
                Equal(0, transport.DataOutCallCount);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestLoaderDataOutSrbRouting()
        {
            FakeDataOutTransport transport = new FakeDataOutTransport();
            AspiSrbProcessor processor = new AspiSrbProcessor(transport, true);
            byte[] data = new byte[
                PrecisionTwoLoaderRecordManifest.RecordLength];
            for (int index = 0; index < data.Length; ++index)
            {
                data[index] = checked((byte)((index + 1) & 0xFF));
            }
            IntPtr buffer = Marshal.AllocHGlobal(data.Length);
            IntPtr srb = AllocateSrb();
            try
            {
                Marshal.Copy(data, 0, buffer, data.Length);
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionOut |
                        AspiConstants.FlagEventNotify,
                    checked((uint)data.Length), buffer,
                    ScsiFraming.BuildPrecisionTwoLoaderFirstRecordCdb());
                Equal((uint)AspiConstants.StatusComplete,
                    processor.Process(srb));
                Equal(1, transport.DataOutCallCount);
                Equal((byte)0x3B, transport.LastDataOutCdb[0]);
                Equal(data.Length, transport.LastDataReference.Length);
                for (int index = 0;
                    index < transport.LastDataReference.Length; ++index)
                {
                    Equal((byte)0, transport.LastDataReference[index]);
                }

                byte[] variant =
                    ScsiFraming.BuildPrecisionTwoLoaderFirstRecordCdb();
                variant[2] = 1;
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionOut |
                        AspiConstants.FlagEventNotify,
                    checked((uint)data.Length), buffer, variant);
                Equal((uint)AspiConstants.StatusInvalidCommand,
                    processor.Process(srb));

                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionOut,
                    checked((uint)data.Length), buffer,
                    ScsiFraming.BuildPrecisionTwoLoaderFirstRecordCdb());
                Equal((uint)AspiConstants.StatusInvalidSrb,
                    processor.Process(srb));

                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionOut |
                        AspiConstants.FlagDirectionIn |
                        AspiConstants.FlagEventNotify,
                    checked((uint)data.Length), buffer,
                    ScsiFraming.BuildPrecisionTwoLoaderFirstRecordCdb());
                Equal((uint)AspiConstants.StatusInvalidSrb,
                    processor.Process(srb));
                Equal(1, transport.DataOutCallCount);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestPreviewSetWindowSrbRouting()
        {
            FakeDataOutTransport transport = new FakeDataOutTransport();
            AspiSrbProcessor processor = new AspiSrbProcessor(transport, true);
            byte[] data = PreviewSetWindowData();
            IntPtr buffer = Marshal.AllocHGlobal(
                PrecisionTwoLoaderRecordManifest.RecordLength);
            IntPtr srb = AllocateSrb();
            try
            {
                Marshal.Copy(data, 0, buffer, data.Length);
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionOut |
                        AspiConstants.FlagEventNotify,
                    checked((uint)data.Length), buffer,
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb());
                Equal((uint)AspiConstants.StatusInvalidCommand,
                    processor.Process(srb));
                Equal(0, transport.DataOutCallCount);

                FakeDataOutTransport mismatchTransport =
                    new FakeDataOutTransport();
                TestUsbLog mismatchLog = new TestUsbLog();
                AspiSrbProcessor mismatchProcessor = new AspiSrbProcessor(
                    mismatchTransport, mismatchLog, false, false, true, true,
                    new string('0', 64));
                Equal((uint)AspiConstants.StatusInvalidCommand,
                    mismatchProcessor.Process(srb));
                Equal(0, mismatchTransport.DataOutCallCount);
                True(mismatchLog.LastWarning != null &&
                    mismatchLog.LastWarning.IndexOf(
                        "payload-sha256=" + Sha256Hex(data),
                        StringComparison.Ordinal) >= 0);

                FakeDataOutTransport fingerprintTransport =
                    new FakeDataOutTransport();
                TestUsbLog fingerprintLog = new TestUsbLog();
                AspiSrbProcessor fingerprintProcessor = new AspiSrbProcessor(
                    fingerprintTransport, fingerprintLog, false, false, true,
                    false, Sha256Hex(data));
                Equal((uint)AspiConstants.StatusInvalidCommand,
                    fingerprintProcessor.Process(srb));
                Equal(0, fingerprintTransport.DataOutCallCount);
                True(fingerprintLog.LastWarning != null &&
                    fingerprintLog.LastWarning.IndexOf(
                        "fingerprint captured and blocked before USB; " +
                        "payload-sha256=" + Sha256Hex(data),
                        StringComparison.Ordinal) >= 0);
                True(fingerprintLog.LastInfo != null &&
                    fingerprintLog.LastInfo.IndexOf(
                        "structured metadata captured without retaining " +
                        "the payload: regions-sha256=00-09:",
                        StringComparison.Ordinal) >= 0);
                Equal((uint)AspiConstants.StatusInvalidCommand,
                    fingerprintProcessor.Process(srb));
                True(fingerprintLog.LastWarning.IndexOf(
                    "fingerprint observation was already consumed",
                    StringComparison.Ordinal) >= 0);
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn, 3996, buffer,
                    ScsiFraming.BuildPrecisionTwoPreviewImageReadCdb(666));
                Equal((uint)AspiConstants.StatusInvalidCommand,
                    fingerprintProcessor.Process(srb));
                Equal(0, fingerprintTransport.DataOutCallCount);

                FakeDataOutTransport previewTransport =
                    new FakeDataOutTransport();
                TestUsbLog previewLog = new TestUsbLog();
                AspiSrbProcessor previewProcessor = new AspiSrbProcessor(
                    previewTransport, previewLog, false, false, true, true,
                    Sha256Hex(data));
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionOut |
                        AspiConstants.FlagEventNotify,
                    checked((uint)data.Length), buffer,
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb());
                Equal((uint)AspiConstants.StatusComplete,
                    previewProcessor.Process(srb));
                Equal(1, previewTransport.DataOutCallCount);
                True(previewLog.LastInfo != null &&
                    previewLog.LastInfo.IndexOf(
                        "Preview SET WINDOW candidate validated for " +
                        "transport; payload-sha256=" + Sha256Hex(data),
                        StringComparison.Ordinal) >= 0);

                FakeDataOutTransport statefulTransport =
                    new FakeDataOutTransport();
                statefulTransport.
                    StatefulPreviewSetWindowValidationEnabled = true;
                AspiSrbProcessor statefulProcessor = new AspiSrbProcessor(
                    statefulTransport, null, false, false, true, true,
                    Sha256Hex(data));
                byte[] cleanupCandidate = (byte[])data.Clone();
                cleanupCandidate[83] = 1;
                Marshal.Copy(cleanupCandidate, 0, buffer,
                    cleanupCandidate.Length);
                Equal((uint)AspiConstants.StatusComplete,
                    statefulProcessor.Process(srb));
                Equal(1, statefulTransport.DataOutCallCount);
                byte[] malformedCleanup = (byte[])cleanupCandidate.Clone();
                malformedCleanup[7] = 0x4B;
                Marshal.Copy(malformedCleanup, 0, buffer,
                    malformedCleanup.Length);
                Equal((uint)AspiConstants.StatusInvalidCommand,
                    statefulProcessor.Process(srb));
                Equal(1, statefulTransport.DataOutCallCount);

                Marshal.Copy(data, 0, buffer, data.Length);

                FakeDataOutTransport failedTransport =
                    new FakeDataOutTransport();
                failedTransport.DataOutFailure =
                    new InvalidOperationException("independent safety check");
                TestUsbLog failedLog = new TestUsbLog();
                AspiSrbProcessor failedProcessor = new AspiSrbProcessor(
                    failedTransport, failedLog, false, false, true, true,
                    Sha256Hex(data));
                Equal((uint)AspiConstants.StatusError,
                    failedProcessor.Process(srb));
                Equal(1, failedTransport.DataOutCallCount);
                True(failedLog.AllWarnings.IndexOf(
                    "Preview SET WINDOW transport failed after candidate " +
                    "validation; payload-sha256=" + Sha256Hex(data),
                    StringComparison.Ordinal) >= 0);

                byte[] loaderData = new byte[
                    PrecisionTwoLoaderRecordManifest.RecordLength];
                Marshal.Copy(loaderData, 0, buffer, loaderData.Length);
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionOut |
                        AspiConstants.FlagEventNotify,
                    checked((uint)loaderData.Length), buffer,
                    ScsiFraming.BuildPrecisionTwoLoaderFirstRecordCdb());
                Equal((uint)AspiConstants.StatusInvalidSrb,
                    previewProcessor.Process(srb));
                Equal(1, previewTransport.DataOutCallCount);

                byte[] startup = new byte[1082];
                Marshal.Copy(startup, 0, buffer, startup.Length);
                AspiSrbProcessor replayProcessor = new AspiSrbProcessor(
                    transport, null, false, true, true);
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionOut |
                        AspiConstants.FlagEventNotify,
                    1082, buffer,
                    ScsiFraming.BuildPrecisionTwoStartupWriteBuffer1082Cdb());
                Equal((uint)AspiConstants.StatusComplete,
                    replayProcessor.Process(srb));
                Equal(1, transport.DataOutCallCount);
                Equal((byte)0x3B, transport.LastDataOutCdb[0]);
                for (int index = 0;
                    index < transport.LastDataReference.Length; ++index)
                {
                    Equal((byte)0, transport.LastDataReference[index]);
                }

                data[83] = 1;
                Marshal.Copy(data, 0, buffer, data.Length);
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionOut |
                        AspiConstants.FlagEventNotify,
                    checked((uint)data.Length), buffer,
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb());
                Equal((uint)AspiConstants.StatusComplete,
                    replayProcessor.Process(srb));
                Equal(2, transport.DataOutCallCount);

                data[7] = 0x4B;
                Marshal.Copy(data, 0, buffer, data.Length);
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionOut |
                        AspiConstants.FlagEventNotify,
                    checked((uint)data.Length), buffer,
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb());
                Equal((uint)AspiConstants.StatusInvalidCommand,
                    replayProcessor.Process(srb));
                Equal(2, transport.DataOutCallCount);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestStartupWriteFingerprintSrbRouting()
        {
            var transport = new StartupFingerprintTransport();
            var log = new TestUsbLog();
            var processor = new AspiSrbProcessor(transport, log, false,
                false, true, true,
                PrecisionTwoPreviewCommandManifest.AspiLiveSetWindowSha256,
                true);
            IntPtr srb = AllocateSrb();
            IntPtr buffer = Marshal.AllocHGlobal(
                checked((int)ScsiFraming.
                    PrecisionTwoStartupWriteBuffer1082Length));
            try
            {
                byte[] data = new byte[ScsiFraming.
                    PrecisionTwoStartupWriteBuffer1082Length];
                for (int index = 0; index < data.Length; ++index)
                {
                    data[index] = checked((byte)(index & 0xFF));
                }
                Marshal.Copy(data, 0, buffer, data.Length);
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionOut |
                        AspiConstants.FlagEventNotify,
                    checked((uint)data.Length), buffer,
                    ScsiFraming.BuildPrecisionTwoStartupWriteBuffer1082Cdb());
                Equal(AspiConstants.StatusInvalidCommand,
                    processor.Process(srb));
                Equal(1, transport.FingerprintCallCount);
                Equal(0, transport.DataOutCallCount);
                True(log.LastWarning.IndexOf(
                    "startup WRITE BUFFER fingerprint captured and blocked " +
                    "before USB; payload-sha256=" + Sha256Hex(data),
                    StringComparison.Ordinal) >= 0);

                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionOut |
                        AspiConstants.FlagEventNotify,
                    checked((uint)data.Length), buffer,
                    ScsiFraming.BuildPrecisionTwoStartupWriteBuffer1082Cdb());
                Equal(AspiConstants.StatusInvalidCommand,
                    processor.Process(srb));
                Equal(1, transport.FingerprintCallCount);
                Equal(0, transport.DataOutCallCount);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestWinUsbPreviewManifestBinding()
        {
            ExpectArgument(delegate
            {
                Usb2XchangeDevice.Find(UsbConstants.OperationalProductId,
                    null, 1000, new string('G', 64));
            });
            ExpectArgument(delegate
            {
                Usb2XchangeDevice.Find(UsbConstants.OperationalProductId,
                    null, 1000, new string('0', 63));
            });
            ExpectArgument(delegate
            {
                new WinUsbAspiTransport(null, false, true, false,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveSetWindowSha256, true);
            });
            var firstRead = new WinUsbAspiTransport(null, false, true, true,
                PrecisionTwoPreviewCommandManifest.AspiLiveSetWindowSha256,
                true);
            True(firstRead.PreviewFirstImageReadEnabled);
            firstRead.Dispose();
            var first24x36Read = new WinUsbAspiTransport(null, false, true,
                true, PrecisionTwoPreviewCommandManifest.
                    AspiLive24x36SetWindowSha256, true);
            True(first24x36Read.PreviewFirstImageReadEnabled);
            Equal(PrecisionTwoPreviewCommandManifest.
                    AspiLive24x36SetWindowSha256,
                first24x36Read.PreviewSetWindowSha256);
            first24x36Read.Dispose();
            var first4x5Read = new WinUsbAspiTransport(null, false, true,
                true, PrecisionTwoPreviewCommandManifest.
                    AspiLive4x5SetWindowSha256, true);
            True(first4x5Read.PreviewFirstImageReadEnabled);
            Equal(PrecisionTwoPreviewCommandManifest.
                    AspiLive4x5SetWindowSha256,
                first4x5Read.PreviewSetWindowSha256);
            first4x5Read.Dispose();
            var burst = new WinUsbAspiTransport(null, false, true, true,
                PrecisionTwoPreviewCommandManifest.AspiLiveSetWindowSha256,
                PrecisionTwoPreviewCommandManifest.AspiLivePreviewBurstRows);
            Equal(PrecisionTwoPreviewCommandManifest.AspiLivePreviewBurstRows,
                burst.PreviewImageReadLimit);
            True(!burst.PreviewFirstImageReadEnabled);
            burst.Dispose();
            var stream = new WinUsbAspiTransport(null, false, true, true,
                PrecisionTwoPreviewCommandManifest.AspiLiveSetWindowSha256,
                PrecisionTwoPreviewCommandManifest.AspiLivePreviewStreamRows);
            Equal(PrecisionTwoPreviewCommandManifest.AspiLivePreviewStreamRows,
                stream.PreviewImageReadLimit);
            True(!stream.PreviewFirstImageReadEnabled);
            stream.Dispose();
            ExpectArgument(delegate
            {
                new WinUsbAspiTransport(null, false, true, true,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveSetWindowSha256, 2);
            });
        }

        private static void TestLoaderSequenceExact()
        {
            byte[] firmware = SyntheticLoaderFirmware();
            PrecisionTwoLoaderRecordManifest manifest =
                PrecisionTwoLoaderRecordManifest.Create(firmware,
                    Sha256Hex(firmware));
            long now = 0;
            AspiLoaderSequenceGate gate = new AspiLoaderSequenceGate(manifest,
                delegate { return now; });
            byte[] recordCdb =
                ScsiFraming.BuildPrecisionTwoLoaderFirstRecordCdb();
            for (int index = 0;
                index < PrecisionTwoLoaderRecordManifest.RecordCount; ++index)
            {
                byte[] record = PrecisionTwoLoaderRecordManifest.BuildRecord(
                    firmware, index);
                Equal(index, gate.Begin(5, 0, recordCdb, record, true));
                True(gate.CompletionPending);
                ++now;
                gate.Complete((byte)AdapterStatus.Success, 0, record.Length);
                Equal(index + 1, gate.AcceptedRecordCount);
            }
            byte[] terminal = PrecisionTwoLoaderRecordManifest.BuildTerminal();
            Equal(-1, gate.Begin(5, 0,
                ScsiFraming.BuildPrecisionTwoLoaderTerminalRecordCdb(),
                terminal, true));
            ++now;
            gate.Complete((byte)AdapterStatus.Success, 0, terminal.Length);
            Equal(AspiLoaderSequenceState.Complete, gate.State);
            Equal(PrecisionTwoLoaderRecordManifest.RecordCount,
                gate.AcceptedRecordCount);
            ExpectInvalid(delegate
            {
                gate.Begin(5, 0,
                    ScsiFraming.BuildPrecisionTwoLoaderTerminalRecordCdb(),
                    terminal, true);
            });
            Equal(AspiLoaderSequenceState.Failed, gate.State);
        }

        private static void TestLoaderSequenceFailures()
        {
            byte[] firmware = SyntheticLoaderFirmware();
            PrecisionTwoLoaderRecordManifest manifest =
                PrecisionTwoLoaderRecordManifest.Create(firmware,
                    Sha256Hex(firmware));
            byte[] cdb = ScsiFraming.BuildPrecisionTwoLoaderFirstRecordCdb();
            byte[] first = PrecisionTwoLoaderRecordManifest.BuildRecord(
                firmware, 0);

            AspiLoaderSequenceGate noD8 = new AspiLoaderSequenceGate(manifest,
                delegate { return 0; });
            ExpectInvalid(delegate { noD8.Begin(5, 0, cdb, first, false); });
            Equal(AspiLoaderSequenceState.Failed, noD8.State);

            AspiLoaderSequenceGate wrongOrder =
                new AspiLoaderSequenceGate(manifest, delegate { return 0; });
            byte[] second = PrecisionTwoLoaderRecordManifest.BuildRecord(
                firmware, 1);
            ExpectProtocol(delegate
            {
                wrongOrder.Begin(5, 0, cdb, second, true);
            });
            Equal(AspiLoaderSequenceState.Failed, wrongOrder.State);

            AspiLoaderSequenceGate mutated =
                new AspiLoaderSequenceGate(manifest, delegate { return 0; });
            byte[] changed = (byte[])first.Clone();
            changed[PrecisionTwoLoaderRecordManifest.RecordHeaderLength] ^=
                0x01;
            ExpectProtocol(delegate
            {
                mutated.Begin(5, 0, cdb, changed, true);
            });
            Equal(AspiLoaderSequenceState.Failed, mutated.State);

            AspiLoaderSequenceGate prematureTerminal =
                new AspiLoaderSequenceGate(manifest, delegate { return 0; });
            ExpectProtocol(delegate
            {
                prematureTerminal.Begin(5, 0,
                    ScsiFraming.BuildPrecisionTwoLoaderTerminalRecordCdb(),
                    PrecisionTwoLoaderRecordManifest.BuildTerminal(), true);
            });
            Equal(AspiLoaderSequenceState.Failed, prematureTerminal.State);

            AspiLoaderSequenceGate wrongTarget =
                new AspiLoaderSequenceGate(manifest, delegate { return 0; });
            ExpectInvalid(delegate
            {
                wrongTarget.Begin(4, 0, cdb, first, true);
            });
            Equal(AspiLoaderSequenceState.Failed, wrongTarget.State);

            AspiLoaderSequenceGate missingCompletion =
                new AspiLoaderSequenceGate(manifest, delegate { return 0; });
            missingCompletion.Begin(5, 0, cdb, first, true);
            ExpectInvalid(delegate
            {
                missingCompletion.Begin(5, 0, cdb, first, true);
            });
            Equal(AspiLoaderSequenceState.Failed, missingCompletion.State);

            AspiLoaderSequenceGate failedCompletion =
                new AspiLoaderSequenceGate(manifest, delegate { return 0; });
            failedCompletion.Begin(5, 0, cdb, first, true);
            ExpectInvalid(delegate
            {
                failedCompletion.Complete(
                    (byte)AdapterStatus.SelectionTimeout,
                    checked((uint)first.Length), 0);
            });
            Equal(AspiLoaderSequenceState.Failed, failedCompletion.State);

            AspiLoaderSequenceGate shortCompletion =
                new AspiLoaderSequenceGate(manifest, delegate { return 0; });
            shortCompletion.Begin(5, 0, cdb, first, true);
            ExpectInvalid(delegate
            {
                shortCompletion.Complete((byte)AdapterStatus.Success, 0,
                    first.Length - 1);
            });
            Equal(AspiLoaderSequenceState.Failed, shortCompletion.State);

            AspiLoaderSequenceGate residualCompletion =
                new AspiLoaderSequenceGate(manifest, delegate { return 0; });
            residualCompletion.Begin(5, 0, cdb, first, true);
            ExpectInvalid(delegate
            {
                residualCompletion.Complete((byte)AdapterStatus.Success, 1,
                    first.Length);
            });
            Equal(AspiLoaderSequenceState.Failed, residualCompletion.State);

            long now = 0;
            AspiLoaderSequenceGate expired = new AspiLoaderSequenceGate(
                manifest, delegate { return now; });
            expired.Begin(5, 0, cdb, first, true);
            now = AspiLoaderSequenceGate.MaximumCompletionMilliseconds + 1;
            ExpectInvalid(delegate
            {
                expired.Complete((byte)AdapterStatus.Success, 0,
                    first.Length);
            });
            Equal(AspiLoaderSequenceState.Failed, expired.State);

            now = 0;
            AspiLoaderSequenceGate sequenceExpired =
                new AspiLoaderSequenceGate(manifest, delegate { return now; });
            sequenceExpired.Begin(5, 0, cdb, first, true);
            sequenceExpired.Complete((byte)AdapterStatus.Success, 0,
                first.Length);
            now = AspiLoaderSequenceGate.MaximumSequenceMilliseconds + 1;
            ExpectInvalid(delegate
            {
                sequenceExpired.Begin(5, 0, cdb, second, true);
            });
            Equal(AspiLoaderSequenceState.Failed, sequenceExpired.State);

            now = 5;
            AspiLoaderSequenceGate backwards = new AspiLoaderSequenceGate(
                manifest, delegate { return now; });
            backwards.Begin(5, 0, cdb, first, true);
            now = 4;
            ExpectInvalid(delegate
            {
                backwards.Complete((byte)AdapterStatus.Success, 0,
                    first.Length);
            });
            Equal(AspiLoaderSequenceState.Failed, backwards.State);
        }

        private static void TestLiveStateChangingRuntimeApprovals()
        {
            string originalMode = Environment.GetEnvironmentVariable(
                AspiRuntime.TransportEnvironmentVariable);
            string originalApproval = Environment.GetEnvironmentVariable(
                AspiRuntime.LoaderApprovalEnvironmentVariable);
            string originalPreviewApproval = Environment.GetEnvironmentVariable(
                AspiRuntime.PreviewApprovalEnvironmentVariable);
            string original24x36SetWindowApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    Preview24x36SetWindowApprovalEnvironmentVariable);
            string original24x36FirstReadApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    Preview24x36FirstReadApprovalEnvironmentVariable);
            string original4x5FirstReadApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    Preview4x5FirstReadApprovalEnvironmentVariable);
            string originalFingerprintApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    PreviewFingerprintApprovalEnvironmentVariable);
            string originalFirstReadApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    PreviewFirstReadApprovalEnvironmentVariable);
            string originalBurstApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    PreviewBurstApprovalEnvironmentVariable);
            string originalStreamApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    PreviewStreamApprovalEnvironmentVariable);
            string originalShortRetryApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    PreviewShortRetryApprovalEnvironmentVariable);
            string originalNaturalApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    PreviewNaturalApprovalEnvironmentVariable);
            string originalNaturalPerRowApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    PreviewNaturalPerRowApprovalEnvironmentVariable);
            string originalPoweredNaturalApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredNaturalApprovalEnvironmentVariable);
            string originalPoweredCleanupApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredCleanupApprovalEnvironmentVariable);
            string originalPoweredCancellationApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredCancellationApprovalEnvironmentVariable);
            TestUsbLog log = new TestUsbLog();
            try
            {
                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.OfflineReplayMode,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext replay = AspiRuntime.Create(log);
                True(!replay.LoaderDataOutEnabled);
                True(replay.OperationalReplayEnabled);
                True(replay.PredictedPreviewObservationEnabled);
                True(!replay.PreviewImageReadEnabled);
                Equal(PrecisionTwoPreviewCommandManifest.SetWindowSha256,
                    replay.PreviewSetWindowSha256);

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LiveLoaderMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(
                    AspiRuntime.LoaderApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext missing = AspiRuntime.Create(log);
                Equal((byte)0, missing.AdapterCount);
                True(!missing.LoaderDataOutEnabled);
                True(!missing.OperationalReplayEnabled);
                True(!missing.PredictedPreviewObservationEnabled);
                True(missing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(
                    AspiRuntime.LoaderApprovalEnvironmentVariable,
                    "wrong-token", EnvironmentVariableTarget.Process);
                AspiRuntimeContext wrong = AspiRuntime.Create(log);
                Equal((byte)0, wrong.AdapterCount);
                True(!wrong.LoaderDataOutEnabled);
                True(!wrong.OperationalReplayEnabled);
                True(!wrong.PredictedPreviewObservationEnabled);
                True(wrong.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(
                    AspiRuntime.LoaderApprovalEnvironmentVariable,
                    AspiRuntime.LoaderApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext exact = AspiRuntime.Create(log);
                Equal((byte)1, exact.AdapterCount);
                True(exact.LoaderDataOutEnabled);
                True(!exact.OperationalReplayEnabled);
                True(!exact.PredictedPreviewObservationEnabled);
                True(exact.Transport is WinUsbAspiTransport);
                Equal(PrecisionTwoPreviewCommandManifest.SetWindowSha256,
                    ((WinUsbAspiTransport)exact.Transport).
                        PreviewSetWindowSha256);
                IDisposable disposable = exact.Transport as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LivePreview24x36FirstReadMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    Preview24x36FirstReadApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext first24x36Missing =
                    AspiRuntime.Create(log);
                Equal((byte)0, first24x36Missing.AdapterCount);
                True(!first24x36Missing.PreviewImageReadEnabled);
                True(first24x36Missing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    Preview24x36FirstReadApprovalEnvironmentVariable,
                    AspiRuntime.PreviewFirstReadApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext first24x36Wrong = AspiRuntime.Create(log);
                Equal((byte)0, first24x36Wrong.AdapterCount);
                True(!first24x36Wrong.PreviewImageReadEnabled);
                True(first24x36Wrong.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    Preview24x36FirstReadApprovalEnvironmentVariable,
                    AspiRuntime.Preview24x36SetWindowApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext first24x36RejectsSetWindowToken =
                    AspiRuntime.Create(log);
                Equal((byte)0,
                    first24x36RejectsSetWindowToken.AdapterCount);
                True(!first24x36RejectsSetWindowToken.
                    PreviewImageReadEnabled);
                True(first24x36RejectsSetWindowToken.Transport is
                    DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    Preview24x36FirstReadApprovalEnvironmentVariable,
                    AspiRuntime.Preview24x36FirstReadApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext first24x36Exact = AspiRuntime.Create(log);
                Equal((byte)1, first24x36Exact.AdapterCount);
                True(!first24x36Exact.LoaderDataOutEnabled);
                True(!first24x36Exact.OperationalReplayEnabled);
                True(first24x36Exact.PredictedPreviewObservationEnabled);
                True(first24x36Exact.PreviewSetWindowEnabled);
                True(first24x36Exact.PreviewImageReadEnabled);
                Equal(PrecisionTwoPreviewCommandManifest.
                        AspiLive24x36SetWindowSha256,
                    first24x36Exact.PreviewSetWindowSha256);
                True(first24x36Exact.Transport is WinUsbAspiTransport);
                True(((WinUsbAspiTransport)first24x36Exact.Transport).
                    PreviewFirstImageReadEnabled);
                disposable = first24x36Exact.Transport as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LivePreview4x5FirstReadMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    Preview4x5FirstReadApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext first4x5Missing = AspiRuntime.Create(log);
                Equal((byte)0, first4x5Missing.AdapterCount);
                True(!first4x5Missing.PreviewImageReadEnabled);
                True(first4x5Missing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    Preview4x5FirstReadApprovalEnvironmentVariable,
                    AspiRuntime.Preview24x36FirstReadApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext first4x5Wrong = AspiRuntime.Create(log);
                Equal((byte)0, first4x5Wrong.AdapterCount);
                True(!first4x5Wrong.PreviewImageReadEnabled);
                True(first4x5Wrong.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    Preview4x5FirstReadApprovalEnvironmentVariable,
                    AspiRuntime.Preview4x5FirstReadApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext first4x5Exact = AspiRuntime.Create(log);
                Equal((byte)1, first4x5Exact.AdapterCount);
                True(!first4x5Exact.LoaderDataOutEnabled);
                True(!first4x5Exact.OperationalReplayEnabled);
                True(first4x5Exact.PredictedPreviewObservationEnabled);
                True(first4x5Exact.PreviewSetWindowEnabled);
                True(first4x5Exact.PreviewImageReadEnabled);
                Equal(PrecisionTwoPreviewCommandManifest.
                        AspiLive4x5SetWindowSha256,
                    first4x5Exact.PreviewSetWindowSha256);
                True(first4x5Exact.Transport is WinUsbAspiTransport);
                True(((WinUsbAspiTransport)first4x5Exact.Transport).
                    PreviewFirstImageReadEnabled);
                disposable = first4x5Exact.Transport as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LivePreview24x36SetWindowObserveMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    Preview24x36SetWindowApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext preview24x36Missing =
                    AspiRuntime.Create(log);
                Equal((byte)0, preview24x36Missing.AdapterCount);
                True(!preview24x36Missing.PreviewSetWindowEnabled);
                True(preview24x36Missing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    Preview24x36SetWindowApprovalEnvironmentVariable,
                    AspiRuntime.PreviewApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext preview24x36Wrong =
                    AspiRuntime.Create(log);
                Equal((byte)0, preview24x36Wrong.AdapterCount);
                True(!preview24x36Wrong.PreviewSetWindowEnabled);
                True(preview24x36Wrong.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    Preview24x36SetWindowApprovalEnvironmentVariable,
                    AspiRuntime.Preview24x36SetWindowApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext preview24x36Exact =
                    AspiRuntime.Create(log);
                Equal((byte)1, preview24x36Exact.AdapterCount);
                True(!preview24x36Exact.LoaderDataOutEnabled);
                True(!preview24x36Exact.OperationalReplayEnabled);
                True(preview24x36Exact.
                    PredictedPreviewObservationEnabled);
                True(preview24x36Exact.PreviewSetWindowEnabled);
                True(!preview24x36Exact.PreviewImageReadEnabled);
                Equal(PrecisionTwoPreviewCommandManifest.
                        AspiLive24x36SetWindowSha256,
                    preview24x36Exact.PreviewSetWindowSha256);
                True(preview24x36Exact.Transport is WinUsbAspiTransport);
                Equal(PrecisionTwoPreviewCommandManifest.
                        AspiLive24x36SetWindowSha256,
                    ((WinUsbAspiTransport)preview24x36Exact.Transport).
                        PreviewSetWindowSha256);
                disposable = preview24x36Exact.Transport as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LivePreviewPoweredCancellationMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredCancellationApprovalEnvironmentVariable,
                    null, EnvironmentVariableTarget.Process);
                AspiRuntimeContext cancellationMissing =
                    AspiRuntime.Create(log);
                Equal((byte)0, cancellationMissing.AdapterCount);
                True(cancellationMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredCancellationApprovalEnvironmentVariable,
                    AspiRuntime.PreviewPoweredCleanupApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext cancellationRejectsCleanupToken =
                    AspiRuntime.Create(log);
                Equal((byte)0,
                    cancellationRejectsCleanupToken.AdapterCount);
                True(cancellationRejectsCleanupToken.Transport is
                    DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredCancellationApprovalEnvironmentVariable,
                    AspiRuntime.PreviewPoweredCancellationApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext cancellationExact =
                    AspiRuntime.Create(log);
                Equal((byte)1, cancellationExact.AdapterCount);
                True(cancellationExact.PreviewImageReadEnabled);
                True(cancellationExact.Transport is WinUsbAspiTransport);
                True(!((WinUsbAspiTransport)cancellationExact.Transport).
                    PreviewCleanupExecutionEnabled);
                True(((WinUsbAspiTransport)cancellationExact.Transport).
                    PreviewCancellationExecutionEnabled);
                True(((IAspiPreviewSetWindowValidationPolicy)
                    cancellationExact.Transport).
                        StatefulPreviewSetWindowValidationEnabled);
                disposable = cancellationExact.Transport as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LivePreviewPoweredCleanupMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredCleanupApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext cleanupMissing = AspiRuntime.Create(log);
                Equal((byte)0, cleanupMissing.AdapterCount);
                True(cleanupMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredCleanupApprovalEnvironmentVariable,
                    AspiRuntime.PreviewPoweredNaturalApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext cleanupRejectsObservationToken =
                    AspiRuntime.Create(log);
                Equal((byte)0, cleanupRejectsObservationToken.AdapterCount);
                True(cleanupRejectsObservationToken.Transport is
                    DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredCleanupApprovalEnvironmentVariable,
                    AspiRuntime.PreviewPoweredCleanupApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext cleanupExact = AspiRuntime.Create(log);
                Equal((byte)1, cleanupExact.AdapterCount);
                True(cleanupExact.PreviewImageReadEnabled);
                True(cleanupExact.Transport is WinUsbAspiTransport);
                True(((WinUsbAspiTransport)cleanupExact.Transport).
                    PreviewCleanupExecutionEnabled);
                Equal(PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalRows,
                    ((WinUsbAspiTransport)cleanupExact.Transport).
                        PreviewImageReadLimit);
                disposable = cleanupExact.Transport as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LivePreviewPoweredNaturalMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredNaturalApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext poweredMissing = AspiRuntime.Create(log);
                Equal((byte)0, poweredMissing.AdapterCount);
                True(poweredMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredNaturalApprovalEnvironmentVariable,
                    AspiRuntime.PreviewNaturalPerRowApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext poweredRejects996Token =
                    AspiRuntime.Create(log);
                Equal((byte)0, poweredRejects996Token.AdapterCount);
                True(poweredRejects996Token.Transport is
                    DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredNaturalApprovalEnvironmentVariable,
                    AspiRuntime.PreviewPoweredCleanupApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext poweredRejectsCleanupToken =
                    AspiRuntime.Create(log);
                Equal((byte)0, poweredRejectsCleanupToken.AdapterCount);
                True(poweredRejectsCleanupToken.Transport is
                    DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredNaturalApprovalEnvironmentVariable,
                    AspiRuntime.PreviewPoweredNaturalApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext poweredExact = AspiRuntime.Create(log);
                Equal((byte)1, poweredExact.AdapterCount);
                True(poweredExact.PreviewImageReadEnabled);
                True(poweredExact.Transport is WinUsbAspiTransport);
                Equal(PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalRows,
                    ((WinUsbAspiTransport)poweredExact.Transport).
                        PreviewImageReadLimit);
                Equal(PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                    ((WinUsbAspiTransport)poweredExact.Transport).
                        PreviewMaximumReadSubmissions);
                Equal(PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalMaximumShortRetries,
                    ((WinUsbAspiTransport)poweredExact.Transport).
                        PreviewMaximumShortRetries);
                disposable = poweredExact.Transport as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LivePreviewNaturalPerRowMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewNaturalPerRowApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext perRowMissing = AspiRuntime.Create(log);
                Equal((byte)0, perRowMissing.AdapterCount);
                True(perRowMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewNaturalPerRowApprovalEnvironmentVariable,
                    AspiRuntime.PreviewNaturalApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext perRowRejectsLegacyToken =
                    AspiRuntime.Create(log);
                Equal((byte)0, perRowRejectsLegacyToken.AdapterCount);
                True(perRowRejectsLegacyToken.Transport is
                    DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewNaturalPerRowApprovalEnvironmentVariable,
                    AspiRuntime.PreviewNaturalPerRowApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext perRowExact = AspiRuntime.Create(log);
                Equal((byte)1, perRowExact.AdapterCount);
                True(perRowExact.PreviewImageReadEnabled);
                True(perRowExact.Transport is WinUsbAspiTransport);
                Equal(PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows,
                    ((WinUsbAspiTransport)perRowExact.Transport).
                        PreviewImageReadLimit);
                Equal(PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalPerRowMaximumReadSubmissions,
                    ((WinUsbAspiTransport)perRowExact.Transport).
                        PreviewMaximumReadSubmissions);
                Equal(PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalPerRowMaximumShortRetries,
                    ((WinUsbAspiTransport)perRowExact.Transport).
                        PreviewMaximumShortRetries);
                disposable = perRowExact.Transport as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LivePreviewNaturalMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewNaturalApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext naturalMissing = AspiRuntime.Create(log);
                Equal((byte)0, naturalMissing.AdapterCount);
                True(naturalMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewNaturalApprovalEnvironmentVariable,
                    AspiRuntime.PreviewShortRetryApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext naturalRejects256Token =
                    AspiRuntime.Create(log);
                Equal((byte)0, naturalRejects256Token.AdapterCount);
                True(naturalRejects256Token.Transport is
                    DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewNaturalApprovalEnvironmentVariable,
                    AspiRuntime.PreviewNaturalApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext naturalExact = AspiRuntime.Create(log);
                Equal((byte)1, naturalExact.AdapterCount);
                True(naturalExact.PreviewImageReadEnabled);
                True(naturalExact.PreviewSetWindowEnabled);
                True(naturalExact.Transport is WinUsbAspiTransport);
                Equal(PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalRows,
                    ((WinUsbAspiTransport)naturalExact.Transport).
                        PreviewImageReadLimit);
                True(((WinUsbAspiTransport)naturalExact.Transport).
                    PreviewImageShortRetryEnabled);
                disposable = naturalExact.Transport as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LivePreviewStreamMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewStreamApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext streamMissing = AspiRuntime.Create(log);
                Equal((byte)0, streamMissing.AdapterCount);
                True(!streamMissing.PreviewImageReadEnabled);
                True(streamMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewStreamApprovalEnvironmentVariable,
                    AspiRuntime.PreviewBurstApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext streamStale = AspiRuntime.Create(log);
                Equal((byte)0, streamStale.AdapterCount);
                True(streamStale.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewStreamApprovalEnvironmentVariable,
                    AspiRuntime.PreviewStreamApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext streamExact = AspiRuntime.Create(log);
                Equal((byte)1, streamExact.AdapterCount);
                True(streamExact.PreviewImageReadEnabled);
                True(streamExact.PreviewSetWindowEnabled);
                True(streamExact.Transport is WinUsbAspiTransport);
                Equal(PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamRows,
                    ((WinUsbAspiTransport)streamExact.Transport).
                        PreviewImageReadLimit);
                True(!((WinUsbAspiTransport)streamExact.Transport).
                    PreviewImageShortRetryEnabled);
                disposable = streamExact.Transport as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewStreamApprovalEnvironmentVariable,
                    AspiRuntime.PreviewShortRetryApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext streamRejectsShortToken =
                    AspiRuntime.Create(log);
                Equal((byte)0, streamRejectsShortToken.AdapterCount);
                True(streamRejectsShortToken.Transport is
                    DisabledAspiTransport);

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LivePreviewShortRetryMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewShortRetryApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext shortRetryMissing = AspiRuntime.Create(log);
                Equal((byte)0, shortRetryMissing.AdapterCount);
                True(shortRetryMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewShortRetryApprovalEnvironmentVariable,
                    AspiRuntime.PreviewStreamApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext shortRetryRejectsStreamToken =
                    AspiRuntime.Create(log);
                Equal((byte)0, shortRetryRejectsStreamToken.AdapterCount);
                True(shortRetryRejectsStreamToken.Transport is
                    DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewShortRetryApprovalEnvironmentVariable,
                    AspiRuntime.PreviewShortRetryApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext shortRetryExact = AspiRuntime.Create(log);
                Equal((byte)1, shortRetryExact.AdapterCount);
                True(shortRetryExact.PreviewImageReadEnabled);
                True(shortRetryExact.PreviewSetWindowEnabled);
                True(shortRetryExact.Transport is WinUsbAspiTransport);
                Equal(PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewStreamRows,
                    ((WinUsbAspiTransport)shortRetryExact.Transport).
                        PreviewImageReadLimit);
                True(((WinUsbAspiTransport)shortRetryExact.Transport).
                    PreviewImageShortRetryEnabled);
                disposable = shortRetryExact.Transport as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LivePreviewBurstMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewBurstApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext burstMissing = AspiRuntime.Create(log);
                Equal((byte)0, burstMissing.AdapterCount);
                True(!burstMissing.PreviewImageReadEnabled);
                True(burstMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewBurstApprovalEnvironmentVariable,
                    AspiRuntime.PreviewFirstReadApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext burstStale = AspiRuntime.Create(log);
                Equal((byte)0, burstStale.AdapterCount);
                True(burstStale.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewBurstApprovalEnvironmentVariable,
                    AspiRuntime.PreviewBurstApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext burstExact = AspiRuntime.Create(log);
                Equal((byte)1, burstExact.AdapterCount);
                True(burstExact.PreviewImageReadEnabled);
                True(burstExact.PreviewSetWindowEnabled);
                True(burstExact.Transport is WinUsbAspiTransport);
                Equal(PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewBurstRows,
                    ((WinUsbAspiTransport)burstExact.Transport).
                        PreviewImageReadLimit);
                disposable = burstExact.Transport as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LivePreviewFingerprintMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewFingerprintApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext fingerprintMissing = AspiRuntime.Create(log);
                Equal((byte)0, fingerprintMissing.AdapterCount);
                True(!fingerprintMissing.PreviewSetWindowEnabled);
                True(fingerprintMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewFingerprintApprovalEnvironmentVariable,
                    AspiRuntime.PreviewFingerprintApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext fingerprintExact = AspiRuntime.Create(log);
                Equal((byte)1, fingerprintExact.AdapterCount);
                True(!fingerprintExact.LoaderDataOutEnabled);
                True(!fingerprintExact.OperationalReplayEnabled);
                True(fingerprintExact.PredictedPreviewObservationEnabled);
                True(!fingerprintExact.PreviewSetWindowEnabled);
                Equal(PrecisionTwoPreviewCommandManifest.
                    AspiLiveSetWindowSha256,
                    fingerprintExact.PreviewSetWindowSha256);
                True(fingerprintExact.Transport is WinUsbAspiTransport);
                Equal(PrecisionTwoPreviewCommandManifest.
                    AspiLiveSetWindowSha256,
                    ((WinUsbAspiTransport)fingerprintExact.Transport).
                        PreviewSetWindowSha256);
                disposable = fingerprintExact.Transport as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LivePreviewObserveMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(
                    AspiRuntime.PreviewApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext previewMissing = AspiRuntime.Create(log);
                Equal((byte)0, previewMissing.AdapterCount);
                True(!previewMissing.PreviewSetWindowEnabled);
                True(previewMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(
                    AspiRuntime.PreviewApprovalEnvironmentVariable,
                    "wrong-token", EnvironmentVariableTarget.Process);
                AspiRuntimeContext previewWrong = AspiRuntime.Create(log);
                Equal((byte)0, previewWrong.AdapterCount);
                True(!previewWrong.PreviewSetWindowEnabled);
                True(previewWrong.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(
                    AspiRuntime.PreviewApprovalEnvironmentVariable,
                    AspiRuntime.Preview24x36SetWindowApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext previewRejects24x36Token =
                    AspiRuntime.Create(log);
                Equal((byte)0, previewRejects24x36Token.AdapterCount);
                True(!previewRejects24x36Token.PreviewSetWindowEnabled);
                True(previewRejects24x36Token.Transport is
                    DisabledAspiTransport);

                Environment.SetEnvironmentVariable(
                    AspiRuntime.PreviewApprovalEnvironmentVariable,
                    "I-APPROVE-PRECISION2-ONE-SET-WINDOW-THEN-" +
                        "BLOCK-FIRST-READ-F45F2A91",
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext previewStale = AspiRuntime.Create(log);
                Equal((byte)0, previewStale.AdapterCount);
                True(!previewStale.PreviewSetWindowEnabled);
                True(previewStale.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(
                    AspiRuntime.PreviewApprovalEnvironmentVariable,
                    AspiRuntime.PreviewApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext previewExact = AspiRuntime.Create(log);
                Equal((byte)1, previewExact.AdapterCount);
                True(!previewExact.LoaderDataOutEnabled);
                True(!previewExact.OperationalReplayEnabled);
                True(previewExact.PredictedPreviewObservationEnabled);
                True(previewExact.PreviewSetWindowEnabled);
                True(!previewExact.PreviewImageReadEnabled);
                Equal(PrecisionTwoPreviewCommandManifest.
                    AspiLiveSetWindowSha256,
                    previewExact.PreviewSetWindowSha256);
                True(previewExact.Transport is WinUsbAspiTransport);
                Equal(PrecisionTwoPreviewCommandManifest.
                    AspiLiveSetWindowSha256,
                    ((WinUsbAspiTransport)previewExact.Transport).
                        PreviewSetWindowSha256);
                disposable = previewExact.Transport as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LivePreviewFirstReadMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewFirstReadApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext firstReadMissing = AspiRuntime.Create(log);
                Equal((byte)0, firstReadMissing.AdapterCount);
                True(!firstReadMissing.PreviewImageReadEnabled);
                True(firstReadMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewFirstReadApprovalEnvironmentVariable,
                    AspiRuntime.PreviewApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext firstReadStale = AspiRuntime.Create(log);
                Equal((byte)0, firstReadStale.AdapterCount);
                True(!firstReadStale.PreviewImageReadEnabled);
                True(firstReadStale.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewFirstReadApprovalEnvironmentVariable,
                    AspiRuntime.PreviewFirstReadApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext firstReadExact = AspiRuntime.Create(log);
                Equal((byte)1, firstReadExact.AdapterCount);
                True(!firstReadExact.LoaderDataOutEnabled);
                True(!firstReadExact.OperationalReplayEnabled);
                True(firstReadExact.PredictedPreviewObservationEnabled);
                True(firstReadExact.PreviewSetWindowEnabled);
                True(firstReadExact.PreviewImageReadEnabled);
                Equal(PrecisionTwoPreviewCommandManifest.
                    AspiLiveSetWindowSha256,
                    firstReadExact.PreviewSetWindowSha256);
                True(firstReadExact.Transport is WinUsbAspiTransport);
                True(((WinUsbAspiTransport)firstReadExact.Transport).
                    PreviewFirstImageReadEnabled);
                disposable = firstReadExact.Transport as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable, originalMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(
                    AspiRuntime.LoaderApprovalEnvironmentVariable,
                    originalApproval, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(
                    AspiRuntime.PreviewApprovalEnvironmentVariable,
                    originalPreviewApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    Preview24x36SetWindowApprovalEnvironmentVariable,
                    original24x36SetWindowApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    Preview24x36FirstReadApprovalEnvironmentVariable,
                    original24x36FirstReadApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    Preview4x5FirstReadApprovalEnvironmentVariable,
                    original4x5FirstReadApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewFingerprintApprovalEnvironmentVariable,
                    originalFingerprintApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewFirstReadApprovalEnvironmentVariable,
                    originalFirstReadApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewBurstApprovalEnvironmentVariable,
                    originalBurstApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewStreamApprovalEnvironmentVariable,
                    originalStreamApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewShortRetryApprovalEnvironmentVariable,
                    originalShortRetryApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewNaturalApprovalEnvironmentVariable,
                    originalNaturalApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewNaturalPerRowApprovalEnvironmentVariable,
                    originalNaturalPerRowApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredNaturalApprovalEnvironmentVariable,
                    originalPoweredNaturalApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredCleanupApprovalEnvironmentVariable,
                    originalPoweredCleanupApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    PreviewPoweredCancellationApprovalEnvironmentVariable,
                    originalPoweredCancellationApproval,
                    EnvironmentVariableTarget.Process);
            }
        }

        private static void TestLiveFullScanRuntimeApproval()
        {
            string originalMode = Environment.GetEnvironmentVariable(
                AspiRuntime.TransportEnvironmentVariable);
            string originalApproval = Environment.GetEnvironmentVariable(
                AspiRuntime.
                    FullScanStartupFingerprintApprovalEnvironmentVariable);
            string originalSetWindowApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    FullScanSetWindowApprovalEnvironmentVariable);
            string originalCompleteApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    FullScanCompleteApprovalEnvironmentVariable);
            string originalTerminalProbeApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    FullScanTerminalProbeApprovalEnvironmentVariable);
            string originalNaturalCompleteApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    FullScanNaturalCompleteApprovalEnvironmentVariable);
            string originalRow997CompleteApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    FullScanRow997CompleteApprovalEnvironmentVariable);
            string originalProgressCompleteApproval =
                Environment.GetEnvironmentVariable(AspiRuntime.
                    FullScanProgressCompleteApprovalEnvironmentVariable);
            var log = new TestUsbLog();
            try
            {
                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LiveFullScanStartupFingerprintMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanStartupFingerprintApprovalEnvironmentVariable,
                    null, EnvironmentVariableTarget.Process);
                AspiRuntimeContext missing = AspiRuntime.Create(log);
                Equal((byte)0, missing.AdapterCount);
                True(!missing.StartupWriteFingerprintEnabled);
                True(missing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanStartupFingerprintApprovalEnvironmentVariable,
                    AspiRuntime.PreviewPoweredCleanupApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext wrong = AspiRuntime.Create(log);
                Equal((byte)0, wrong.AdapterCount);
                True(wrong.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanStartupFingerprintApprovalEnvironmentVariable,
                    AspiRuntime.FullScanStartupFingerprintApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext exact = AspiRuntime.Create(log);
                Equal((byte)1, exact.AdapterCount);
                True(exact.PreviewImageReadEnabled);
                True(exact.StartupWriteFingerprintEnabled);
                True(exact.Transport is WinUsbAspiTransport);
                var transport = (WinUsbAspiTransport)exact.Transport;
                True(transport.PreviewCleanupExecutionEnabled);
                True(transport.FullScanSuccessorObservationEnabled);
                True(!transport.FullScanSetWindowExecutionEnabled);
                transport.Dispose();

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LiveFullScanSetWindowFirstReadMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanSetWindowApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext setWindowMissing =
                    AspiRuntime.Create(log);
                Equal((byte)0, setWindowMissing.AdapterCount);
                True(setWindowMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanSetWindowApprovalEnvironmentVariable,
                    AspiRuntime.FullScanStartupFingerprintApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext setWindowWrong = AspiRuntime.Create(log);
                Equal((byte)0, setWindowWrong.AdapterCount);
                True(setWindowWrong.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanSetWindowApprovalEnvironmentVariable,
                    AspiRuntime.FullScanSetWindowApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext setWindowExact = AspiRuntime.Create(log);
                Equal((byte)1, setWindowExact.AdapterCount);
                True(setWindowExact.PreviewImageReadEnabled);
                True(setWindowExact.StartupWriteFingerprintEnabled);
                True(setWindowExact.Transport is WinUsbAspiTransport);
                var setWindowTransport =
                    (WinUsbAspiTransport)setWindowExact.Transport;
                True(setWindowTransport.PreviewCleanupExecutionEnabled);
                True(setWindowTransport.
                    FullScanSuccessorObservationEnabled);
                True(setWindowTransport.FullScanSetWindowExecutionEnabled);
                setWindowTransport.Dispose();

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LiveFullScanCompleteMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanCompleteApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext completeMissing = AspiRuntime.Create(log);
                Equal((byte)0, completeMissing.AdapterCount);
                True(completeMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanCompleteApprovalEnvironmentVariable,
                    AspiRuntime.FullScanSetWindowApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext completeWrong = AspiRuntime.Create(log);
                Equal((byte)0, completeWrong.AdapterCount);
                True(completeWrong.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanCompleteApprovalEnvironmentVariable,
                    AspiRuntime.FullScanCompleteApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext completeExact = AspiRuntime.Create(log);
                Equal((byte)1, completeExact.AdapterCount);
                True(completeExact.Transport is WinUsbAspiTransport);
                var completeTransport =
                    (WinUsbAspiTransport)completeExact.Transport;
                True(completeTransport.FullScanSetWindowExecutionEnabled);
                True(completeTransport.FullScanImageExecutionEnabled);
                True(!completeTransport.LiveFullScanCompleted);
                completeTransport.Dispose();

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LiveFullScanTerminalProbeMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanTerminalProbeApprovalEnvironmentVariable, null,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext terminalMissing = AspiRuntime.Create(log);
                Equal((byte)0, terminalMissing.AdapterCount);
                True(terminalMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanTerminalProbeApprovalEnvironmentVariable,
                    AspiRuntime.FullScanCompleteApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext terminalWrong = AspiRuntime.Create(log);
                Equal((byte)0, terminalWrong.AdapterCount);
                True(terminalWrong.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanTerminalProbeApprovalEnvironmentVariable,
                    AspiRuntime.FullScanTerminalProbeApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext terminalExact = AspiRuntime.Create(log);
                Equal((byte)1, terminalExact.AdapterCount);
                True(terminalExact.Transport is WinUsbAspiTransport);
                var terminalTransport =
                    (WinUsbAspiTransport)terminalExact.Transport;
                True(terminalTransport.FullScanImageExecutionEnabled);
                True(terminalTransport.
                    FullScanTerminalProbeExecutionEnabled);
                terminalTransport.Dispose();

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LiveFullScanNaturalCompleteMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanNaturalCompleteApprovalEnvironmentVariable,
                    null, EnvironmentVariableTarget.Process);
                AspiRuntimeContext naturalMissing = AspiRuntime.Create(log);
                Equal((byte)0, naturalMissing.AdapterCount);
                True(naturalMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanNaturalCompleteApprovalEnvironmentVariable,
                    AspiRuntime.FullScanTerminalProbeApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext naturalWrong = AspiRuntime.Create(log);
                Equal((byte)0, naturalWrong.AdapterCount);
                True(naturalWrong.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanNaturalCompleteApprovalEnvironmentVariable,
                    AspiRuntime.FullScanNaturalCompleteApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext naturalExact = AspiRuntime.Create(log);
                Equal((byte)1, naturalExact.AdapterCount);
                True(naturalExact.Transport is WinUsbAspiTransport);
                var naturalTransport =
                    (WinUsbAspiTransport)naturalExact.Transport;
                True(naturalTransport.FullScanImageExecutionEnabled);
                True(!naturalTransport.
                    FullScanTerminalProbeExecutionEnabled);
                True(naturalTransport.FullScanNaturalCompletionEnabled);
                naturalTransport.Dispose();

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LiveFullScanRow997CompleteMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanRow997CompleteApprovalEnvironmentVariable,
                    null, EnvironmentVariableTarget.Process);
                AspiRuntimeContext row997Missing = AspiRuntime.Create(log);
                Equal((byte)0, row997Missing.AdapterCount);
                True(row997Missing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanRow997CompleteApprovalEnvironmentVariable,
                    AspiRuntime.FullScanNaturalCompleteApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext row997Wrong = AspiRuntime.Create(log);
                Equal((byte)0, row997Wrong.AdapterCount);
                True(row997Wrong.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanRow997CompleteApprovalEnvironmentVariable,
                    AspiRuntime.FullScanRow997CompleteApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext row997Exact = AspiRuntime.Create(log);
                Equal((byte)1, row997Exact.AdapterCount);
                True(row997Exact.Transport is WinUsbAspiTransport);
                var row997Transport =
                    (WinUsbAspiTransport)row997Exact.Transport;
                True(row997Transport.FullScanImageExecutionEnabled);
                True(!row997Transport.FullScanTerminalProbeExecutionEnabled);
                True(!row997Transport.FullScanNaturalCompletionEnabled);
                True(row997Transport.FullScanRow997CompletionEnabled);
                row997Transport.Dispose();

                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LiveFullScanProgressCompleteMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanProgressCompleteApprovalEnvironmentVariable,
                    null, EnvironmentVariableTarget.Process);
                AspiRuntimeContext progressMissing = AspiRuntime.Create(log);
                Equal((byte)0, progressMissing.AdapterCount);
                True(progressMissing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanProgressCompleteApprovalEnvironmentVariable,
                    AspiRuntime.FullScanRow997CompleteApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext progressWrong = AspiRuntime.Create(log);
                Equal((byte)0, progressWrong.AdapterCount);
                True(progressWrong.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanProgressCompleteApprovalEnvironmentVariable,
                    AspiRuntime.FullScanProgressCompleteApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext progressExact = AspiRuntime.Create(log);
                Equal((byte)1, progressExact.AdapterCount);
                True(progressExact.Transport is WinUsbAspiTransport);
                var progressTransport =
                    (WinUsbAspiTransport)progressExact.Transport;
                True(progressTransport.FullScanImageExecutionEnabled);
                True(!progressTransport.FullScanTerminalProbeExecutionEnabled);
                True(!progressTransport.FullScanNaturalCompletionEnabled);
                True(!progressTransport.FullScanRow997CompletionEnabled);
                True(progressTransport.FullScanProgressCompletionEnabled);
                progressTransport.Dispose();
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable, originalMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanStartupFingerprintApprovalEnvironmentVariable,
                    originalApproval, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanSetWindowApprovalEnvironmentVariable,
                    originalSetWindowApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanCompleteApprovalEnvironmentVariable,
                    originalCompleteApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanTerminalProbeApprovalEnvironmentVariable,
                    originalTerminalProbeApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanNaturalCompleteApprovalEnvironmentVariable,
                    originalNaturalCompleteApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanRow997CompleteApprovalEnvironmentVariable,
                    originalRow997CompleteApproval,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(AspiRuntime.
                    FullScanProgressCompleteApprovalEnvironmentVariable,
                    originalProgressCompleteApproval,
                    EnvironmentVariableTarget.Process);
            }
        }

        private static byte[] BuildInquiry(string vendor, string product,
            string revision)
        {
            byte[] data = new byte[36];
            data[0] = 0x06;
            WriteAscii(data, 8, 8, vendor);
            WriteAscii(data, 16, 16, product);
            WriteAscii(data, 32, 4, revision);
            return data;
        }

        private static void WriteAscii(byte[] data, int offset, int length,
            string value)
        {
            for (int i = 0; i < length; ++i)
            {
                data[offset + i] = (byte)' ';
            }
            byte[] source = System.Text.Encoding.ASCII.GetBytes(value);
            Buffer.BlockCopy(source, 0, data, offset,
                Math.Min(length, source.Length));
        }

        private static void ExpectInvalid(Action action)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            throw new Exception("Expected InvalidOperationException.");
        }

        private static void ExpectProtocol(Action action)
        {
            try
            {
                action();
            }
            catch (ProtocolException)
            {
                return;
            }
            throw new Exception("Expected ProtocolException.");
        }

        private static void ExpectArgument(Action action)
        {
            try
            {
                action();
            }
            catch (ArgumentException)
            {
                return;
            }
            throw new Exception("Expected ArgumentException.");
        }

        private static byte[] SyntheticLoaderFirmware()
        {
            byte[] firmware = new byte[
                PrecisionTwoLoaderRecordManifest.FirmwareLength];
            for (int index = 0; index < firmware.Length; ++index)
            {
                firmware[index] = checked((byte)((index * 37 + 11) & 0xFF));
            }
            return firmware;
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(bytes)).
                    Replace("-", string.Empty);
            }
        }

        private static void TestSelectionTimeout()
        {
            FakeTransport transport = new FakeTransport();
            transport.Enqueue(new byte[0],
                (byte)AdapterStatus.SelectionTimeout);
            IntPtr srb = AllocateSrb();
            try
            {
                ConfigureExecuteSrb(srb, 2, 0, 0, IntPtr.Zero,
                    new byte[] { 0, 0, 0, 0, 0, 0 });
                uint result = new AspiSrbProcessor(transport).Process(srb);
                Equal((uint)AspiConstants.StatusNoDevice, result);
                Equal(AspiConstants.HostStatusSelectionTimeout,
                    Marshal.ReadByte(srb, AspiConstants.HostStatusOffset));
            }
            finally
            {
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestCheckCondition()
        {
            FakeTransport transport = new FakeTransport();
            transport.Enqueue(new byte[0],
                (byte)AdapterStatus.CheckCondition);
            byte[] sense = new byte[18];
            sense[0] = 0x70;
            sense[2] = 0x06;
            transport.Enqueue(sense, (byte)AdapterStatus.Success);
            IntPtr srb = AllocateSrb();
            try
            {
                ConfigureExecuteSrb(srb, 5, 0, 0, IntPtr.Zero,
                    new byte[] { 0, 0, 0, 0, 0, 0 });
                Marshal.WriteByte(srb, AspiConstants.SenseLengthOffset, 18);
                uint result = new AspiSrbProcessor(transport).Process(srb);
                Equal((uint)AspiConstants.StatusError, result);
                Equal(AspiConstants.TargetStatusCheckCondition,
                    Marshal.ReadByte(srb, AspiConstants.TargetStatusOffset));
                Equal((byte)0x70,
                    Marshal.ReadByte(srb, AspiConstants.SenseAreaOffset));
                Equal(2, transport.CallCount);
                Equal((byte)0x03, transport.LastCdb[0]);
            }
            finally
            {
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestBusyDoesNotCopyData()
        {
            FakeTransport transport = new FakeTransport();
            byte[] shortData = new byte[10];
            for (int index = 0; index < shortData.Length; ++index)
            {
                shortData[index] = checked((byte)(index + 1));
            }
            transport.Enqueue(shortData, (byte)AdapterStatus.Busy);
            IntPtr buffer = Marshal.AllocHGlobal(36);
            IntPtr srb = AllocateSrb();
            try
            {
                for (int index = 0; index < 36; ++index)
                {
                    Marshal.WriteByte(buffer, index, 0xA5);
                }
                ConfigureExecuteSrb(srb, 5,
                    AspiConstants.FlagDirectionIn, 36, buffer,
                    ScsiFraming.BuildInquiryCdb(36));
                uint result = new AspiSrbProcessor(transport).Process(srb);
                Equal((uint)AspiConstants.StatusError, result);
                Equal(AspiConstants.HostStatusOk,
                    Marshal.ReadByte(srb, AspiConstants.HostStatusOffset));
                Equal(AspiConstants.TargetStatusBusy,
                    Marshal.ReadByte(srb, AspiConstants.TargetStatusOffset));
                for (int index = 0; index < 36; ++index)
                {
                    Equal((byte)0xA5, Marshal.ReadByte(buffer, index));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(srb);
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static void TestEventNotification()
        {
            FakeTransport transport = new FakeTransport();
            transport.Enqueue(new byte[0], (byte)AdapterStatus.Success);
            using (EventWaitHandle completed = new EventWaitHandle(false,
                EventResetMode.ManualReset))
            {
                IntPtr srb = AllocateSrb();
                try
                {
                    ConfigureExecuteSrb(srb, 5,
                        AspiConstants.FlagEventNotify, 0, IntPtr.Zero,
                        new byte[] { 0, 0, 0, 0, 0, 0 });
                    WritePointer32(srb, AspiConstants.PostProcedureOffset,
                        completed.SafeWaitHandle.DangerousGetHandle());
                    new AspiSrbProcessor(transport).Process(srb);
                    True(completed.WaitOne(0));
                }
                finally
                {
                    Marshal.FreeHGlobal(srb);
                }
            }
        }

        private static void TestFailureLogMetadata()
        {
            FakeTransport transport = new FakeTransport();
            TestUsbLog log = new TestUsbLog();
            IntPtr srb = AllocateSrb();
            try
            {
                ConfigureExecuteSrb(srb, 5, 0, 0, IntPtr.Zero,
                    new byte[] { 0, 0, 0, 0, 0, 0 });
                Equal((uint)AspiConstants.StatusError,
                    new AspiSrbProcessor(transport, log).Process(srb));
                Equal(AspiConstants.TargetStatusCheckCondition,
                    Marshal.ReadByte(srb,
                        AspiConstants.TargetStatusOffset));
                True(log.LastWarning != null &&
                    log.LastWarning.IndexOf("target=5, lun=0",
                        StringComparison.Ordinal) >= 0 &&
                    log.LastWarning.IndexOf("CDB=00 00 00 00 00 00",
                        StringComparison.Ordinal) >= 0);
            }
            finally
            {
                Marshal.FreeHGlobal(srb);
            }
        }

        private static void TestPreviewObserveLogClassification()
        {
            const string initialHash =
                "96EB9049828909D06A5E8AB32861124D1FE59B67005D3BF0EBDA235584D0C6FB";
            const string cleanupHash =
                "3092FA15361A422D558EF968164EE6CB00B854413630CC0C07A4F62877515A9D";
            var acceptedBuilder = new StringBuilder();
            acceptedBuilder.AppendLine("GetASPI32SupportInfo");
            acceptedBuilder.AppendLine("LIVE ASPI target-5 identity:");
            acceptedBuilder.AppendLine(
                PreviewObserveLogInspector.StructuredMetadataMarker);
            acceptedBuilder.AppendLine(
                PreviewObserveLogInspector.CandidateValidatedMarker +
                initialHash);
            acceptedBuilder.AppendLine(
                PreviewObserveLogInspector.SetWindowCompletedMarker);
            for (int attempt = 1; attempt <= 104; ++attempt)
            {
                acceptedBuilder.AppendFormat(
                    "LIVE ASPI operational ScannerReady response: " +
                    "origin=AwaitFirstPostWindowScannerReady, " +
                    "attempt={0}/256, phase-elapsed-ms={1}/60000, " +
                    "first=0x08, second=0x00.\n", attempt,
                    attempt * 265);
            }
            acceptedBuilder.AppendLine(
                "LIVE ASPI operational ScannerReady response: " +
                "origin=AwaitFirstPostWindowScannerReady, attempt=105/256, " +
                "phase-elapsed-ms=27907/60000, first=0x00, second=0x00.");
            acceptedBuilder.AppendLine(
                "LIVE ASPI operational ScannerReady response: " +
                "origin=AwaitSecondPostWindowScannerReady, attempt=1/256, " +
                "phase-elapsed-ms=3/60000, first=0x00, second=0x00.");
            acceptedBuilder.AppendLine(
                PreviewObserveLogInspector.PostWindowNotReadyMarker);
            acceptedBuilder.AppendLine(
                PreviewObserveLogInspector.ManifestRejectedMarker +
                cleanupHash);
            string accepted = acceptedBuilder.ToString();
            PreviewObserveLogMetadata acceptedMetadata =
                PreviewObserveLogInspector.Inspect(accepted);
            True(acceptedMetadata.Support);
            True(acceptedMetadata.Identity);
            True(acceptedMetadata.SetWindowCompleted);
            True(acceptedMetadata.CandidateValidated);
            True(acceptedMetadata.StructuredMetadataCaptured);
            True(acceptedMetadata.PostWindowNotReadyObserved);
            True(!acceptedMetadata.ManifestRejected);
            True(!acceptedMetadata.TransportRejected);
            True(!acceptedMetadata.ReachedTerminalBoundary);
            Equal(initialHash, acceptedMetadata.PayloadSha256);
            Equal(105, acceptedMetadata.FirstPostWindowAttempts);
            Equal(104, acceptedMetadata.FirstPostWindowNotReadyCount);
            Equal(27907,
                acceptedMetadata.FirstPostWindowElapsedMilliseconds);
            Equal(0, acceptedMetadata.FirstPostWindowFinalFirstByte);
            Equal(0, acceptedMetadata.FirstPostWindowFinalSecondByte);
            Equal(1, acceptedMetadata.SecondPostWindowAttempts);
            Equal(0, acceptedMetadata.SecondPostWindowNotReadyCount);
            Equal(3,
                acceptedMetadata.SecondPostWindowElapsedMilliseconds);
            Equal(0, acceptedMetadata.SecondPostWindowFinalFirstByte);
            Equal(0, acceptedMetadata.SecondPostWindowFinalSecondByte);

            PreviewObserveLogMetadata bounded =
                PreviewObserveLogInspector.Inspect(accepted +
                    PreviewObserveLogInspector.PostWindowRetryLimitMarker);
            True(bounded.PostWindowRetryLimitReached);
            True(bounded.ReachedTerminalBoundary);
            True(!bounded.ManifestRejected);
            Equal(initialHash, bounded.PayloadSha256);

            PreviewObserveLogMetadata rejected =
                PreviewObserveLogInspector.Inspect(
                    PreviewObserveLogInspector.StructuredMetadataMarker +
                    "\n" + PreviewObserveLogInspector.
                        ManifestRejectedMarker + initialHash);
            True(rejected.ManifestRejected);
            True(rejected.ReachedTerminalBoundary);
            True(!rejected.SetWindowCompleted);
            Equal(initialHash, rejected.PayloadSha256);
        }

        private static void TestPreviewFirstReadLogClassification()
        {
            const string imageHash =
                "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
            string completed =
                PreviewObserveLogInspector.FirstImageReadCompletedMarker +
                " status=0x00, requested=3996, actual=3996, residue=0, " +
                "width=666, selector=0x28, payload-sha256=" + imageHash +
                ". All later image and cleanup commands remain blocked.";
            PreviewObserveLogMetadata metadata =
                PreviewObserveLogInspector.Inspect(completed);
            True(metadata.FirstImageReadCompleted);
            True(metadata.ReachedTerminalBoundary);
            True(!metadata.PredictedReadBlocked);
            Equal(0, metadata.FirstImageReadStatus);
            Equal(3996, metadata.FirstImageReadRequested);
            Equal(3996, metadata.FirstImageReadActual);
            Equal(0, metadata.FirstImageReadResidue);
            Equal(666, metadata.FirstImageReadWidth);
            Equal(0x28, metadata.FirstImageReadSelector);
            Equal(imageHash, metadata.FirstImageReadSha256);

            PreviewObserveLogMetadata partial =
                PreviewObserveLogInspector.Inspect(
                    PreviewObserveLogInspector.FirstImageReadCompletedMarker +
                    " status=0x00, requested=3996");
            True(!partial.FirstImageReadCompleted);
            True(!partial.ReachedTerminalBoundary);
        }

        private static void TestPreviewBurstLogClassification()
        {
            const string imageHash =
                "89ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF01234567";
            StringBuilder text = new StringBuilder();
            for (int row = 1; row <= PreviewObserveLogInspector.
                    PreviewBurstRows; ++row)
            {
                text.Append("LIVE ASPI exact bounded Preview image READ " +
                    "completed: row=");
                text.Append(row);
                text.Append("/8, status=0x00, requested=3996, actual=3996, " +
                    "residue=0, width=666, selector=0x28, payload-sha256=");
                text.Append(imageHash);
                text.Append(".\n");
            }
            text.Append(PreviewObserveLogInspector.
                BurstImageReadCompletedMarker);
            PreviewObserveLogMetadata completed =
                PreviewObserveLogInspector.Inspect(text.ToString());
            True(completed.BurstImageReadCompleted);
            True(completed.BurstImageReadsExact);
            Equal(8, completed.BurstImageReadCount);
            Equal(8, completed.BurstImageReadLimit);
            True(completed.ReachedTerminalBoundary);

            string outOfOrder = text.ToString().Replace("row=4/8", "row=5/8");
            PreviewObserveLogMetadata rejected =
                PreviewObserveLogInspector.Inspect(outOfOrder);
            True(!rejected.BurstImageReadCompleted);
            True(!rejected.BurstImageReadsExact);
            True(!rejected.ReachedTerminalBoundary);

            PreviewObserveLogMetadata partial =
                PreviewObserveLogInspector.Inspect(text.ToString().Replace(
                    "row=8/8", "row=8/7"));
            True(!partial.BurstImageReadCompleted);
            True(!partial.BurstImageReadsExact);

            PreviewObserveLogMetadata failed =
                PreviewObserveLogInspector.Inspect(
                    PreviewObserveLogInspector.BurstImageReadFailedMarker +
                    " row=3/8.");
            True(failed.BurstImageReadFailed);
            True(failed.ReachedTerminalBoundary);
            True(!failed.BurstImageReadCompleted);
        }

        private static void TestPreviewStreamLogClassification()
        {
            const string imageHash =
                "76543210FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210FEDCBA98";
            StringBuilder text = new StringBuilder();
            for (int row = 1; row <= PreviewObserveLogInspector.
                    PreviewStreamRows; ++row)
            {
                text.Append("LIVE ASPI exact bounded Preview stream image " +
                    "READ completed: row=");
                text.Append(row);
                text.Append("/256, status=0x00, requested=3996, " +
                    "actual=3996, residue=0, width=666, selector=0x28, " +
                    "payload-sha256=");
                text.Append(imageHash);
                text.Append(".\n");
                if (row == 180 || row == 230)
                {
                    int poll = row == 180 ? 1 : 2;
                    text.Append("LIVE ASPI in-stream ScannerReady response: " +
                        "after-rows=");
                    text.Append(row);
                    text.Append(", poll=");
                    text.Append(poll);
                    text.Append("/256, status=0x00, actual=2, residue=0, " +
                        "first=0x");
                    text.Append(row == 180 ? "08" : "00");
                    text.Append(", second=0x00.\n");
                }
            }
            text.Append(PreviewObserveLogInspector.
                StreamImageReadCompletedMarker);
            PreviewObserveLogMetadata completed =
                PreviewObserveLogInspector.Inspect(text.ToString());
            True(completed.StreamImageReadCompleted);
            True(completed.StreamImageReadsExact);
            True(completed.InStreamScannerReadyPollsExact);
            Equal(256, completed.StreamImageReadCount);
            Equal(256, completed.StreamImageReadLimit);
            Equal(2, completed.InStreamScannerReadyPollCount);
            True(completed.ReachedTerminalBoundary);

            PreviewObserveLogMetadata missingPoll =
                PreviewObserveLogInspector.Inspect(
                    text.ToString().Replace(
                        "LIVE ASPI in-stream ScannerReady response: " +
                        "after-rows=180, poll=1/256, status=0x00, actual=2, " +
                        "residue=0, first=0x08, second=0x00.\n", string.Empty).
                    Replace("poll=2/256", "poll=1/256"));
            True(missingPoll.StreamImageReadCompleted);
            True(missingPoll.InStreamScannerReadyPollsExact);
            Equal(1, missingPoll.InStreamScannerReadyPollCount);

            PreviewObserveLogMetadata noPoll =
                PreviewObserveLogInspector.Inspect(
                    System.Text.RegularExpressions.Regex.Replace(
                        text.ToString(),
                        "LIVE ASPI in-stream ScannerReady response:[^\\r\\n]+[\\r\\n]*",
                        string.Empty));
            True(noPoll.StreamImageReadCompleted);
            True(!noPoll.InStreamScannerReadyPollsExact);

            PreviewObserveLogMetadata badResponse =
                PreviewObserveLogInspector.Inspect(text.ToString().Replace(
                    "first=0x08, second=0x00",
                    "first=0x08, second=0x01"));
            True(badResponse.StreamImageReadCompleted);
            True(!badResponse.InStreamScannerReadyPollsExact);

            PreviewObserveLogMetadata failed =
                PreviewObserveLogInspector.Inspect(
                    PreviewObserveLogInspector.StreamImageReadFailedMarker +
                    " row=200/256.");
            True(failed.StreamImageReadFailed);
            True(failed.ReachedTerminalBoundary);
            True(!failed.StreamImageReadCompleted);
        }

        private static void TestPreviewShortRetryLogClassification()
        {
            const string imageHash =
                "1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF";
            const string shortHash =
                "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";
            StringBuilder text = new StringBuilder();
            int retryCount = 0;
            int submission = 0;
            for (int row = 1; row <= PreviewObserveLogInspector.
                    PreviewStreamRows; ++row)
            {
                int shortsForRow = row == 53 ? 1 : row == 100 ? 2 : 0;
                for (int consecutive = 1; consecutive <= shortsForRow;
                        ++consecutive)
                {
                    ++retryCount;
                    ++submission;
                    text.Append("LIVE ASPI exact Preview short completion " +
                        "converted to bounded target BUSY: row=");
                    text.Append(row);
                    text.Append("/256, short-retry=");
                    text.Append(retryCount);
                    text.Append("/64, consecutive=");
                    text.Append(consecutive);
                    text.Append("/8, submission=");
                    text.Append(submission);
                    text.Append("/320, status=0x00, requested=3996, " +
                        "actual=10, residue=3986, width=666, " +
                        "selector=0x28, payload-sha256=");
                    text.Append(shortHash);
                    text.Append(". No short payload bytes were copied to " +
                        "FlexColor.\n");
                }
                ++submission;
                text.Append("LIVE ASPI exact bounded Preview short-retry " +
                    "stream image READ completed: row=");
                text.Append(row);
                text.Append("/256, status=0x00, requested=3996, " +
                    "actual=3996, residue=0, width=666, selector=0x28, " +
                    "submission=");
                text.Append(submission);
                text.Append("/320, short-retries=");
                text.Append(retryCount);
                text.Append("/64, payload-sha256=");
                text.Append(imageHash);
                text.Append(".\n");
                if (row == 180)
                {
                    text.Append("LIVE ASPI in-stream ScannerReady response: " +
                        "after-rows=180, poll=1/256, status=0x00, actual=2, " +
                        "residue=0, first=0x00, second=0x00.\n");
                }
            }
            text.Append("LIVE ASPI exact bounded Preview short-retry stream " +
                "completed: rows=256/256, submissions=259/320, " +
                "short-retries=3/64. Row 257 and cleanup remain blocked " +
                "before USB.");
            PreviewObserveLogMetadata completed =
                PreviewObserveLogInspector.Inspect(text.ToString());
            True(completed.ShortRetryStreamImageReadCompleted);
            True(completed.ShortRetryStreamImageReadsExact);
            True(completed.ShortRetriesExact);
            Equal(256, completed.ShortRetryStreamImageReadCount);
            Equal(3, completed.ShortRetryCount);
            Equal(259, completed.ShortRetryStreamSubmissionCount);
            True(completed.ReachedTerminalBoundary);

            PreviewObserveLogMetadata wrongShort =
                PreviewObserveLogInspector.Inspect(text.ToString().Replace(
                    "actual=10, residue=3986", "actual=11, residue=3985"));
            True(!wrongShort.ShortRetryStreamImageReadCompleted);
            True(!wrongShort.ShortRetriesExact);

            PreviewObserveLogMetadata wrongCompletionCount =
                PreviewObserveLogInspector.Inspect(text.ToString().Replace(
                    "submissions=259/320, short-retries=3/64",
                    "submissions=258/320, short-retries=2/64"));
            True(!wrongCompletionCount.ShortRetryStreamImageReadCompleted);

            string incompleteText = text.ToString().Substring(0,
                text.ToString().IndexOf(
                    "LIVE ASPI exact bounded Preview short-retry stream " +
                    "image READ completed: row=181/256",
                    StringComparison.Ordinal));
            PreviewObserveLogMetadata incomplete =
                PreviewObserveLogInspector.Inspect(incompleteText);
            Equal(180, incomplete.ShortRetryStreamImageReadCount);
            Equal(183, incomplete.ShortRetryStreamSubmissionCount);
            Equal(3, incomplete.ShortRetryCount);
            True(!incomplete.ShortRetryStreamImageReadCompleted);

            StringBuilder noShort = new StringBuilder();
            for (int row = 1; row <= PreviewObserveLogInspector.
                    PreviewStreamRows; ++row)
            {
                noShort.Append("LIVE ASPI exact bounded Preview short-retry " +
                    "stream image READ completed: row=");
                noShort.Append(row);
                noShort.Append("/256, status=0x00, requested=3996, " +
                    "actual=3996, residue=0, width=666, selector=0x28, " +
                    "submission=");
                noShort.Append(row);
                noShort.Append("/320, short-retries=0/64, payload-sha256=");
                noShort.Append(imageHash);
                noShort.Append(".\n");
            }
            noShort.Append("LIVE ASPI exact bounded Preview short-retry " +
                "stream completed: rows=256/256, submissions=256/320, " +
                "short-retries=0/64.");
            PreviewObserveLogMetadata noShortCompleted =
                PreviewObserveLogInspector.Inspect(noShort.ToString());
            True(noShortCompleted.ShortRetryStreamImageReadCompleted);
            True(noShortCompleted.ShortRetriesExact);
            Equal(0, noShortCompleted.ShortRetryCount);

            PreviewObserveLogMetadata failed =
                PreviewObserveLogInspector.Inspect(
                    PreviewObserveLogInspector.
                        ShortRetryStreamImageReadFailedMarker +
                    " row=53/256, submissions=53/320, " +
                    "short-retries=1/64, consecutive=1/8.");
            True(failed.ShortRetryStreamImageReadFailed);
            True(failed.ReachedTerminalBoundary);
            True(!failed.ShortRetryStreamImageReadCompleted);
        }

        private static void TestPreviewNaturalLogClassification()
        {
            const string imageHash =
                "1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF";
            const string shortHash =
                "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";
            StringBuilder text = new StringBuilder();
            int retryCount = 0;
            int submission = 0;
            for (int row = 1; row <= PreviewObserveLogInspector.
                    PreviewNaturalRows; ++row)
            {
                int shortsForRow = row == 257 ? 2 : row == 900 ? 1 : 0;
                for (int consecutive = 1; consecutive <= shortsForRow;
                        ++consecutive)
                {
                    ++retryCount;
                    ++submission;
                    text.Append("LIVE ASPI exact Preview short completion " +
                        "converted to bounded target BUSY: row=");
                    text.Append(row);
                    text.Append("/996, short-retry=");
                    text.Append(retryCount);
                    text.Append("/512, consecutive=");
                    text.Append(consecutive);
                    text.Append("/8, submission=");
                    text.Append(submission);
                    text.Append("/1508, status=0x00, requested=3996, " +
                        "actual=10, residue=3986, width=666, " +
                        "selector=0x28, payload-sha256=");
                    text.Append(shortHash);
                    text.Append(". No short payload bytes were copied to " +
                        "FlexColor.\n");
                }
                ++submission;
                text.Append("LIVE ASPI exact bounded Preview short-retry " +
                    "stream image READ completed: row=");
                text.Append(row);
                text.Append("/996, status=0x00, requested=3996, " +
                    "actual=3996, residue=0, width=666, selector=0x28, " +
                    "submission=");
                text.Append(submission);
                text.Append("/1508, short-retries=");
                text.Append(retryCount);
                text.Append("/512, payload-sha256=");
                text.Append(imageHash);
                text.Append(".\n");
                if (row == 180)
                {
                    text.Append("LIVE ASPI in-stream ScannerReady response: " +
                        "after-rows=180, poll=1/1024, status=0x00, " +
                        "actual=2, residue=0, first=0x00, " +
                        "second=0x00.\n");
                }
            }
            text.Append("LIVE ASPI exact bounded Preview short-retry stream " +
                "completed: rows=996/996, submissions=999/1508, " +
                "short-retries=3/512. Row 997 and cleanup remain blocked " +
                "before USB.\n");
            text.Append("LIVE ASPI exact post-image cleanup SET WINDOW " +
                "successor observed and blocked before USB: ordinal=1/2, " +
                "payload-sha256=" +
                "3092FA15361A422D558EF968164EE6CB00B854413630CC0C07A4F62877515A9D" +
                ". Cleanup permission is not enabled.");

            PreviewObserveLogMetadata completed =
                PreviewObserveLogInspector.Inspect(text.ToString());
            True(completed.ShortRetryStreamImageReadCompleted);
            True(completed.ShortRetryStreamImageReadsExact);
            True(completed.ShortRetriesExact);
            True(completed.InStreamScannerReadyPollsExact);
            Equal(996, completed.ShortRetryStreamImageReadCount);
            Equal(996, completed.ShortRetryStreamImageReadLimit);
            Equal(3, completed.ShortRetryCount);
            Equal(512, completed.ShortRetryMaximum);
            Equal(999, completed.ShortRetryStreamSubmissionCount);
            Equal(1508,
                completed.ShortRetryStreamMaximumSubmissions);
            Equal(1, completed.CleanupCandidateCount);
            True(completed.CleanupCandidatesExact);
            True(completed.ReachedTerminalBoundary);

            string perRowPolicyText = text.ToString().
                Replace("/512", "/7968").Replace("/1508", "/8964");
            PreviewObserveLogMetadata perRowCompleted =
                PreviewObserveLogInspector.Inspect(perRowPolicyText);
            True(perRowCompleted.ShortRetryStreamImageReadCompleted);
            True(perRowCompleted.ShortRetryStreamImageReadsExact);
            True(perRowCompleted.ShortRetriesExact);
            Equal(7968, perRowCompleted.ShortRetryMaximum);
            Equal(8964,
                perRowCompleted.ShortRetryStreamMaximumSubmissions);

            PreviewObserveLogMetadata mixedNaturalPolicy =
                PreviewObserveLogInspector.Inspect(
                    perRowPolicyText.Replace("/8964", "/1508"));
            True(!mixedNaturalPolicy.ShortRetryStreamImageReadCompleted);
            True(!mixedNaturalPolicy.ShortRetryStreamImageReadsExact);

            PreviewObserveLogMetadata wrongSubmissionLimit =
                PreviewObserveLogInspector.Inspect(text.ToString().Replace(
                    "/1508", "/1507"));
            True(!wrongSubmissionLimit.ShortRetryStreamImageReadCompleted);
            True(!wrongSubmissionLimit.ShortRetryStreamImageReadsExact);

            PreviewObserveLogMetadata stalePollLimit =
                PreviewObserveLogInspector.Inspect(text.ToString().Replace(
                    "poll=1/1024", "poll=1/256"));
            True(stalePollLimit.ShortRetryStreamImageReadCompleted);
            True(!stalePollLimit.InStreamScannerReadyPollsExact);

            PreviewObserveLogMetadata wrongCleanup =
                PreviewObserveLogInspector.Inspect(text.ToString().Replace(
                    "3092FA15361A422D558EF968164EE6CB00B854413630CC0C07A4F62877515A9D",
                    "4092FA15361A422D558EF968164EE6CB00B854413630CC0C07A4F62877515A9D"));
            Equal(1, wrongCleanup.CleanupCandidateCount);
            True(!wrongCleanup.CleanupCandidatesExact);
        }

        private static void TestPreviewPoweredNaturalLogClassification()
        {
            const string imageHash =
                "1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF";
            const string shortHash =
                "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";
            StringBuilder text = new StringBuilder();
            int retryCount = 0;
            int submission = 0;
            for (int row = 1; row <= PreviewObserveLogInspector.
                    PreviewPoweredNaturalRows; ++row)
            {
                int shortsForRow = row == 257 ? 2 : row == 900 ? 1 : 0;
                for (int consecutive = 1; consecutive <= shortsForRow;
                        ++consecutive)
                {
                    ++retryCount;
                    ++submission;
                    text.Append("LIVE ASPI exact Preview short completion " +
                        "converted to bounded target BUSY: row=");
                    text.Append(row);
                    text.Append("/911, short-retry=");
                    text.Append(retryCount);
                    text.Append("/7288, consecutive=");
                    text.Append(consecutive);
                    text.Append("/32, submission=");
                    text.Append(submission);
                    text.Append("/8199, status=0x00, requested=3996, " +
                        "actual=10, residue=3986, width=666, " +
                        "selector=0x28, payload-sha256=");
                    text.Append(shortHash);
                    text.Append(". No short payload bytes were copied to " +
                        "FlexColor.\n");
                }
                ++submission;
                text.Append("LIVE ASPI exact bounded Preview short-retry " +
                    "stream image READ completed: row=");
                text.Append(row);
                text.Append("/911, status=0x00, requested=3996, " +
                    "actual=3996, residue=0, width=666, selector=0x28, " +
                    "submission=");
                text.Append(submission);
                text.Append("/8199, short-retries=");
                text.Append(retryCount);
                text.Append("/7288, payload-sha256=");
                text.Append(imageHash);
                text.Append(".\n");
                if (row == 180)
                {
                    text.Append("LIVE ASPI in-stream ScannerReady response: " +
                        "after-rows=180, poll=1/1024, status=0x00, " +
                        "actual=2, residue=0, first=0x00, " +
                        "second=0x00.\n");
                }
            }
            text.Append("LIVE ASPI exact bounded Preview short-retry stream " +
                "completed: rows=911/911, submissions=914/8199, " +
                "short-retries=3/7288. Row 912 and cleanup remain blocked " +
                "before USB.\n");
            text.Append("LIVE ASPI exact post-image cleanup SET WINDOW " +
                "successor observed and blocked before USB: ordinal=1/2, " +
                "payload-sha256=" +
                "3092FA15361A422D558EF968164EE6CB00B854413630CC0C07A4F62877515A9D" +
                ". Cleanup permission is not enabled.");

            PreviewObserveLogMetadata completed =
                PreviewObserveLogInspector.Inspect(text.ToString());
            if (!completed.ShortRetryStreamImageReadCompleted ||
                !completed.ShortRetryStreamImageReadsExact ||
                !completed.ShortRetriesExact ||
                !completed.InStreamScannerReadyPollsExact ||
                !completed.CleanupCandidatesExact)
            {
                throw new Exception(string.Format(
                    "Powered log classification mismatch: completed={0}, " +
                    "reads={1}, shorts={2}, polls={3}, cleanup={4}/{5}, " +
                    "rows={6}/{7}, retry={8}/{9}, submissions={10}/{11}.",
                    completed.ShortRetryStreamImageReadCompleted,
                    completed.ShortRetryStreamImageReadsExact,
                    completed.ShortRetriesExact,
                    completed.InStreamScannerReadyPollsExact,
                    completed.CleanupCandidateCount,
                    completed.CleanupCandidatesExact,
                    completed.ShortRetryStreamImageReadCount,
                    completed.ShortRetryStreamImageReadLimit,
                    completed.ShortRetryCount, completed.ShortRetryMaximum,
                    completed.ShortRetryStreamSubmissionCount,
                    completed.ShortRetryStreamMaximumSubmissions));
            }
            True(completed.ShortRetryStreamImageReadCompleted);
            True(completed.ShortRetryStreamImageReadsExact);
            True(completed.ShortRetriesExact);
            True(completed.InStreamScannerReadyPollsExact);
            Equal(911, completed.ShortRetryStreamImageReadCount);
            Equal(911, completed.ShortRetryStreamImageReadLimit);
            Equal(3, completed.ShortRetryCount);
            Equal(7288, completed.ShortRetryMaximum);
            Equal(914, completed.ShortRetryStreamSubmissionCount);
            Equal(8199,
                completed.ShortRetryStreamMaximumSubmissions);
            Equal(1, completed.CleanupCandidateCount);
            True(completed.CleanupCandidatesExact);
            True(completed.ReachedTerminalBoundary);

            PreviewObserveLogMetadata mixedPolicy =
                PreviewObserveLogInspector.Inspect(text.ToString().Replace(
                    "/8199", "/8964"));
            True(!mixedPolicy.ShortRetryStreamImageReadCompleted);
            True(!mixedPolicy.ShortRetryStreamImageReadsExact);

            PreviewObserveLogMetadata staleConsecutivePolicy =
                PreviewObserveLogInspector.Inspect(text.ToString().Replace(
                    "/32, submission=", "/8, submission="));
            True(!staleConsecutivePolicy.ShortRetryStreamImageReadCompleted);
            True(!staleConsecutivePolicy.ShortRetriesExact);

            PreviewObserveLogMetadata postTerminalPoll =
                PreviewObserveLogInspector.Inspect(text.ToString().Replace(
                    "after-rows=180", "after-rows=911"));
            True(postTerminalPoll.ShortRetryStreamImageReadCompleted);
            True(!postTerminalPoll.InStreamScannerReadyPollsExact);
        }

        private static void TestPreviewPoweredCleanupLogClassification()
        {
            const string hash =
                "3092FA15361A422D558EF968164EE6CB00B854413630CC0C07A4F62877515A9D";
            string text =
                "LIVE ASPI exact post-image cleanup SET WINDOW completed: " +
                "ordinal=1/2, status=0x00, actual=84, residue=0, " +
                "payload-sha256=" + hash + ".\n" +
                "LIVE ASPI exact post-image cleanup SET WINDOW completed: " +
                "ordinal=2/2, status=0x00, actual=84, residue=0, " +
                "payload-sha256=" + hash + ".";
            PreviewObserveLogMetadata completed =
                PreviewObserveLogInspector.Inspect(text);
            Equal(2, completed.CleanupCompletionCount);
            True(completed.CleanupCompletionsExact);
            Equal(0, completed.CancellationCleanupCompletionCount);
            Equal(0, completed.CleanupCandidateCount);
            True(completed.ReachedTerminalBoundary);

            PreviewObserveLogMetadata cancelled =
                PreviewObserveLogInspector.Inspect(text.Replace(
                    "post-image", "cancellation"));
            Equal(2, cancelled.CleanupCompletionCount);
            Equal(2, cancelled.CancellationCleanupCompletionCount);
            True(cancelled.CleanupCompletionsExact);
            True(cancelled.ReachedTerminalBoundary);

            PreviewObserveLogMetadata mixedCompletionKinds =
                PreviewObserveLogInspector.Inspect(text.Replace(
                    "LIVE ASPI exact post-image cleanup SET WINDOW " +
                    "completed: ordinal=2/2",
                    "LIVE ASPI exact cancellation cleanup SET WINDOW " +
                    "completed: ordinal=2/2"));
            Equal(2, mixedCompletionKinds.CleanupCompletionCount);
            Equal(1,
                mixedCompletionKinds.CancellationCleanupCompletionCount);
            True(mixedCompletionKinds.CleanupCompletionsExact);

            PreviewObserveLogMetadata wrongOrdinal =
                PreviewObserveLogInspector.Inspect(text.Replace(
                    "ordinal=2/2", "ordinal=1/2"));
            Equal(2, wrongOrdinal.CleanupCompletionCount);
            True(!wrongOrdinal.CleanupCompletionsExact);

            PreviewObserveLogMetadata wrongStatus =
                PreviewObserveLogInspector.Inspect(text.Replace(
                    "status=0x00, actual=84", "status=0x08, actual=84"));
            Equal(2, wrongStatus.CleanupCompletionCount);
            True(!wrongStatus.CleanupCompletionsExact);

            PreviewObserveLogMetadata wrongLength =
                PreviewObserveLogInspector.Inspect(text.Replace(
                    "actual=84, residue=0", "actual=83, residue=1"));
            Equal(2, wrongLength.CleanupCompletionCount);
            True(!wrongLength.CleanupCompletionsExact);

            PreviewObserveLogMetadata wrongHash =
                PreviewObserveLogInspector.Inspect(text.Replace(hash,
                    "4092FA15361A422D558EF968164EE6CB00B854413630CC0C07A4F62877515A9D"));
            Equal(2, wrongHash.CleanupCompletionCount);
            True(!wrongHash.CleanupCompletionsExact);

            PreviewObserveLogMetadata mixed =
                PreviewObserveLogInspector.Inspect(text + "\n" +
                    "LIVE ASPI exact post-image cleanup SET WINDOW " +
                    "successor observed and blocked before USB: ordinal=1/2, " +
                    "payload-sha256=" + hash + ". Cleanup permission is " +
                    "not enabled.");
            Equal(2, mixed.CleanupCompletionCount);
            Equal(1, mixed.CleanupCandidateCount);

            PreviewObserveLogMetadata onlyOne =
                PreviewObserveLogInspector.Inspect(text.Substring(0,
                    text.IndexOf('\n')));
            Equal(1, onlyOne.CleanupCompletionCount);
            True(onlyOne.CleanupCompletionsExact);
            True(!onlyOne.ReachedTerminalBoundary);

            PreviewObserveLogMetadata failed =
                PreviewObserveLogInspector.Inspect(
                    "Post-image cleanup SET WINDOW transport failed after " +
                    "candidate validation; payload-sha256=" + hash +
                    "; error=System.InvalidOperationException: rejected");
            True(failed.CleanupTransportFailed);
            True(failed.ReachedTerminalBoundary);
        }

        private static IntPtr AllocateSrb()
        {
            IntPtr srb = Marshal.AllocHGlobal(96);
            for (int i = 0; i < 96; i++)
            {
                Marshal.WriteByte(srb, i, 0);
            }
            return srb;
        }

        private static void TestOperatorSessionCycleRotation()
        {
            var cycles = new Queue<FakeOperatorCycleTransport>();
            var first = new FakeOperatorCycleTransport();
            var second = new FakeOperatorCycleTransport();
            cycles.Enqueue(first);
            cycles.Enqueue(second);
            int factoryCalls = 0;
            using (var session = new AspiOperatorSessionTransport(delegate
            {
                ++factoryCalls;
                return cycles.Dequeue();
            }))
            {
                AspiTransportResult read = session.Execute(5, 0,
                    new byte[] { 0x12, 0, 0, 0, 36, 0 }, 36);
                Equal((byte)AdapterStatus.Success, read.AdapterStatus);
                Equal(1, factoryCalls);
                Equal(1, first.ReadCalls);
                Equal(0, session.CompletedCycles);

                first.CompleteOnNextDataOut = true;
                session.ExecuteDataOut(5, 0,
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                    new byte[ScsiFraming.
                        PrecisionTwoPreviewSetWindowLength]);
                Equal(1, first.DataOutCalls);
                Equal(1, first.DisposeCalls);
                Equal(1, session.CompletedCycles);
                True(!session.Failed);

                session.Execute(5, 0,
                    new byte[] { 0x12, 0, 0, 0, 36, 0 }, 36);
                Equal(2, factoryCalls);
                Equal(1, second.ReadCalls);
                Equal(1, session.CompletedCycles);
            }
            Equal(1, second.DisposeCalls);
        }

        private static void TestOperatorSessionFailureIsTerminal()
        {
            var failedCycle = new FakeOperatorCycleTransport();
            failedCycle.ReadFailure = new InvalidOperationException(
                "synthetic cycle failure");
            int factoryCalls = 0;
            var session = new AspiOperatorSessionTransport(delegate
            {
                ++factoryCalls;
                return failedCycle;
            });
            bool firstRejected = false;
            try
            {
                session.Execute(5, 0,
                    new byte[] { 0x12, 0, 0, 0, 36, 0 }, 36);
            }
            catch (InvalidOperationException ex)
            {
                firstRejected = ex.Message == "synthetic cycle failure";
            }
            True(firstRejected);
            True(session.Failed);
            Equal(1, failedCycle.DisposeCalls);

            bool laterRejected = false;
            try
            {
                session.Execute(5, 0,
                    new byte[] { 0x12, 0, 0, 0, 36, 0 }, 36);
            }
            catch (InvalidOperationException ex)
            {
                laterRejected = ex.Message.IndexOf("failed closed",
                    StringComparison.Ordinal) >= 0;
            }
            True(laterRejected);
            Equal(1, factoryCalls);
            session.Dispose();
            Equal(1, failedCycle.DisposeCalls);
        }

        private static void TestOperatorSessionFactoryFailureIsTerminal()
        {
            int factoryCalls = 0;
            var session = new AspiOperatorSessionTransport(delegate
            {
                ++factoryCalls;
                throw new InvalidOperationException("factory failed");
            });
            bool firstRejected = false;
            try
            {
                session.Execute(5, 0,
                    new byte[] { 0x12, 0, 0, 0, 36, 0 }, 36);
            }
            catch (InvalidOperationException ex)
            {
                firstRejected = ex.Message == "factory failed";
            }
            True(firstRejected);
            True(session.Failed);

            bool laterRejected = false;
            try
            {
                session.Execute(5, 0,
                    new byte[] { 0x12, 0, 0, 0, 36, 0 }, 36);
            }
            catch (InvalidOperationException ex)
            {
                laterRejected = ex.Message.IndexOf("failed closed",
                    StringComparison.Ordinal) >= 0;
            }
            True(laterRejected);
            Equal(1, factoryCalls);
            session.Dispose();
        }

        private static void TestOperatorCycleConfiguration()
        {
            WinUsbAspiTransport cycle =
                WinUsbAspiTransport.CreateOperatorCycle(new TestUsbLog());
            try
            {
                True(cycle.OperatorCycleEnabled);
                True(!cycle.WarmOperatorCycleEnabled);
                True(cycle.PreviewCleanupExecutionEnabled);
                True(cycle.PreviewCancellationExecutionEnabled);
                True(cycle.FullScanSuccessorObservationEnabled);
                True(cycle.FullScanSetWindowExecutionEnabled);
                True(cycle.FullScanImageExecutionEnabled);
                True(cycle.FullScanProgressCompletionEnabled);
                True(!((IAspiOperatorCycleTransport)cycle).CycleCompleted);
            }
            finally
            {
                cycle.Dispose();
            }

            bool incompletePolicyRejected = false;
            try
            {
                new WinUsbAspiTransport(new TestUsbLog(), false, true, true,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveSetWindowSha256,
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
                    true, false, true, true, true, false, false, false,
                    true, true);
            }
            catch (ArgumentException ex)
            {
                incompletePolicyRejected = ex.ParamName ==
                    "operatorCycleEnabled";
            }
            True(incompletePolicyRejected);

            WinUsbAspiTransport warm =
                WinUsbAspiTransport.CreateWarmOperatorCycle(
                    new TestUsbLog());
            try
            {
                True(warm.OperatorCycleEnabled);
                True(warm.WarmOperatorCycleEnabled);
                ExpectInvalid(delegate
                {
                    // This must fail on the exact warm-prefix gate before
                    // EnsureConnected can construct or contact WinUSB.
                    warm.Execute(5, 0, ScsiFraming.
                        BuildPrecisionTwoLoaderReadBufferD8Cdb(), 66);
                });
            }
            finally
            {
                warm.Dispose();
            }
        }

        private static void TestLiveOperatorSessionRuntimeApproval()
        {
            string originalMode = Environment.GetEnvironmentVariable(
                AspiRuntime.TransportEnvironmentVariable);
            string originalApproval = Environment.GetEnvironmentVariable(
                AspiRuntime.OperatorSessionApprovalEnvironmentVariable);
            var log = new TestUsbLog();
            try
            {
                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.LiveOperatorSessionMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(
                    AspiRuntime.OperatorSessionApprovalEnvironmentVariable,
                    null, EnvironmentVariableTarget.Process);
                AspiRuntimeContext missing = AspiRuntime.Create(log);
                Equal((byte)0, missing.AdapterCount);
                True(missing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(
                    AspiRuntime.OperatorSessionApprovalEnvironmentVariable,
                    AspiRuntime.FullScanProgressCompleteApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext wrong = AspiRuntime.Create(log);
                Equal((byte)0, wrong.AdapterCount);
                True(wrong.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(
                    AspiRuntime.OperatorSessionApprovalEnvironmentVariable,
                    AspiRuntime.OperatorSessionApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext exact = AspiRuntime.Create(log);
                Equal((byte)1, exact.AdapterCount);
                True(exact.Transport is AspiOperatorSessionTransport);
                True(exact.PreviewSetWindowEnabled);
                True(exact.PreviewImageReadEnabled);
                ((IDisposable)exact.Transport).Dispose();
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable, originalMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(
                    AspiRuntime.OperatorSessionApprovalEnvironmentVariable,
                    originalApproval, EnvironmentVariableTarget.Process);
            }
        }

        private static void TestWarmOperatorInquiryGate()
        {
            byte[] inquiry = new byte[] { 0x12, 0, 0, 0, 0x60, 0 };
            True(WinUsbAspiTransport.
                IsWarmOperatorPreviewInitializationRead(ScsiFraming.
                    BuildPrecisionTwoScannerReadyCdb(), 2));
            True(WinUsbAspiTransport.
                IsWarmOperatorPreviewInitializationRead(ScsiFraming.
                    BuildPrecisionTwoFaultPixelReadBufferCdb(), 22));
            True(WinUsbAspiTransport.
                IsWarmOperatorPreviewInitializationRead(
                    new byte[] { 0x03, 0, 0, 0, 18, 0 }, 18));
            True(!WinUsbAspiTransport.
                IsWarmOperatorPreviewInitializationRead(inquiry, 96));
            True(!WinUsbAspiTransport.
                IsWarmOperatorPreviewInitializationRead(ScsiFraming.
                    BuildPrecisionTwoLoaderReadBufferD8Cdb(), 66));
            var exact = new AspiWarmOperatorInquiryGate();
            exact.Arm();
            exact.Begin(5, 0, inquiry, 96);
            exact.Complete((byte)AdapterStatus.Success, 96, 0, true);
            True(exact.Completed);
            exact.BeginInitialScannerReady(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            True(exact.InitialScannerReadyAccepted);
            exact.BeginStabilizationInquiry(5, 0, inquiry, 96,
                AspiOperationalSequenceGate.MaximumInitializationCycles - 3);
            exact.CompleteStabilizationInquiry(
                (byte)AdapterStatus.Success, 96, 0, true);
            Equal(1, exact.StabilizationInquiriesCompleted);
            True(!exact.StabilizationSequenceCompleted);
            exact.BeginStabilizationInquiry(5, 0, inquiry, 96,
                AspiOperationalSequenceGate.MaximumInitializationCycles - 2);
            exact.CompleteStabilizationInquiry(
                (byte)AdapterStatus.Success, 96, 0, true);
            Equal(2, exact.StabilizationInquiriesCompleted);
            True(exact.StabilizationSequenceCompleted);
            ExpectInvalid(delegate
            {
                exact.BeginStabilizationInquiry(5, 0, inquiry, 96,
                    AspiOperationalSequenceGate.MaximumInitializationCycles - 1);
            });
            True(!exact.Completed);

            var wrongCounter = CompletedWarmPrefix(inquiry);
            ExpectInvalid(delegate
            {
                wrongCounter.BeginStabilizationInquiry(5, 0, inquiry, 96,
                    AspiOperationalSequenceGate.MaximumInitializationCycles - 2);
            });

            var wrongStabilizationIdentity = CompletedWarmPrefix(inquiry);
            wrongStabilizationIdentity.BeginStabilizationInquiry(5, 0,
                inquiry, 96,
                AspiOperationalSequenceGate.MaximumInitializationCycles - 3);
            ExpectInvalid(delegate
            {
                wrongStabilizationIdentity.CompleteStabilizationInquiry(
                    (byte)AdapterStatus.Success, 96, 0, false);
            });

            var wrongStabilizationLength = CompletedWarmPrefix(inquiry);
            wrongStabilizationLength.BeginStabilizationInquiry(5, 0,
                inquiry, 96,
                AspiOperationalSequenceGate.MaximumInitializationCycles - 3);
            ExpectInvalid(delegate
            {
                wrongStabilizationLength.CompleteStabilizationInquiry(
                    (byte)AdapterStatus.Success, 95, 1, true);
            });

            var unarmed = new AspiWarmOperatorInquiryGate();
            ExpectInvalid(delegate
            {
                unarmed.Begin(5, 0, inquiry, 96);
            });

            var wrongCdb = new AspiWarmOperatorInquiryGate();
            wrongCdb.Arm();
            byte[] changed = (byte[])inquiry.Clone();
            changed[4] = 36;
            ExpectInvalid(delegate
            {
                wrongCdb.Begin(5, 0, changed, 36);
            });

            var wrongIdentity = new AspiWarmOperatorInquiryGate();
            wrongIdentity.Arm();
            wrongIdentity.Begin(5, 0, inquiry, 96);
            ExpectInvalid(delegate
            {
                wrongIdentity.Complete((byte)AdapterStatus.Success, 96, 0,
                    false);
            });
            True(!wrongIdentity.Completed);

            var wrongStatus = new AspiWarmOperatorInquiryGate();
            wrongStatus.Arm();
            wrongStatus.Begin(5, 0, inquiry, 96);
            ExpectInvalid(delegate
            {
                wrongStatus.Complete(
                    (byte)AdapterStatus.SelectionTimeout, 96, 0, true);
            });

            var wrongLength = new AspiWarmOperatorInquiryGate();
            wrongLength.Arm();
            wrongLength.Begin(5, 0, inquiry, 96);
            ExpectInvalid(delegate
            {
                wrongLength.Complete((byte)AdapterStatus.Success, 95, 1,
                    true);
            });

            var wrongSuccessor = new AspiWarmOperatorInquiryGate();
            wrongSuccessor.Arm();
            wrongSuccessor.Begin(5, 0, inquiry, 96);
            wrongSuccessor.Complete((byte)AdapterStatus.Success, 96, 0,
                true);
            ExpectInvalid(delegate
            {
                wrongSuccessor.BeginInitialScannerReady(5, 0,
                    ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb(),
                    66);
            });
        }

        private static AspiWarmOperatorInquiryGate CompletedWarmPrefix(
            byte[] inquiry)
        {
            var gate = new AspiWarmOperatorInquiryGate();
            gate.Arm();
            gate.Begin(5, 0, inquiry, 96);
            gate.Complete((byte)AdapterStatus.Success, 96, 0, true);
            gate.BeginInitialScannerReady(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            return gate;
        }

        private static void TestLiveWarmOperatorGate()
        {
            long now = 0;
            byte[] inquiry = new byte[] { 0x12, 0, 0, 0, 0x60, 0 };
            AspiWarmOperatorInquiryGate inquiryGate =
                CompletedWarmPrefix(inquiry);
            var gate = new AspiOperationalSequenceGate(delegate { return now; },
                PrecisionTwoPreviewCommandManifest.AspiLiveSetWindowSha256,
                false, true, true, true,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalRows,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalMaximumReadSubmissions,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredNaturalMaximumShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewPoweredMaximumConsecutiveShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalMaximumScannerReadyPolls,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalMaximumMilliseconds);
            gate.BeginLiveWarmOperatorCycle();
            Equal(AspiOperationalSequenceState.Complete, gate.State);
            Equal(AspiOperationalSequenceGate.MaximumInitializationCycles - 3,
                gate.CompletedInitializationCycles);
            gate.BeginLiveWarmOperatorPreviewInitialization();
            Equal(AspiOperationalSequenceState.AwaitInitialScannerReady,
                gate.State);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), new byte[2],
                ref now);
            CompleteWarmInitializationCycle(gate, inquiryGate, inquiry,
                ref now);
            CompleteWarmInitializationCycle(gate, inquiryGate, inquiry,
                ref now);
            Equal(AspiOperationalSequenceGate.MaximumInitializationCycles - 1,
                gate.CompletedInitializationCycles);
            Equal(AspiOperationalSequenceState.PreviewSetWindowReady,
                gate.State);
            True(inquiryGate.StabilizationSequenceCompleted);

            var incompatible = new AspiOperationalSequenceGate();
            ExpectInvalid(delegate
            {
                incompatible.BeginLiveWarmOperatorCycle();
            });
        }

        private static void CompleteWarmInitializationCycle(
            AspiOperationalSequenceGate gate,
            AspiWarmOperatorInquiryGate inquiryGate, byte[] inquiry,
            ref long now)
        {
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoFaultPixelReadBufferCdb(),
                KnownFaultPixelHeader(), ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoFaultPixelDataReadBufferCdb(),
                new byte[58], ref now);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoCalibrationReadBufferCdb(),
                new byte[1024], ref now);
            inquiryGate.BeginStabilizationInquiry(5, 0, inquiry, 96,
                gate.CompletedInitializationCycles);
            inquiryGate.CompleteStabilizationInquiry(
                (byte)AdapterStatus.Success, 96, 0, true);
            CompleteOperationalRead(gate,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(),
                new byte[2], ref now);
        }

        private static void TestOfflineOperatorRuntimeApproval()
        {
            string originalMode = Environment.GetEnvironmentVariable(
                AspiRuntime.TransportEnvironmentVariable);
            string originalApproval = Environment.GetEnvironmentVariable(
                AspiRuntime.OfflineOperatorApprovalEnvironmentVariable);
            string originalIdentifier = Environment.GetEnvironmentVariable(
                AspiRuntime.ReplayIdentifierEnvironmentVariable);
            var log = new TestUsbLog();
            try
            {
                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable,
                    AspiRuntime.OfflineOperatorReplayMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(
                    AspiRuntime.ReplayIdentifierEnvironmentVariable,
                    "FP12345678", EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(
                    AspiRuntime.OfflineOperatorApprovalEnvironmentVariable,
                    null, EnvironmentVariableTarget.Process);
                AspiRuntimeContext missing = AspiRuntime.Create(log);
                Equal((byte)0, missing.AdapterCount);
                True(missing.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(
                    AspiRuntime.OfflineOperatorApprovalEnvironmentVariable,
                    AspiRuntime.OfflineCancelApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext wrong = AspiRuntime.Create(log);
                Equal((byte)0, wrong.AdapterCount);
                True(wrong.Transport is DisabledAspiTransport);

                Environment.SetEnvironmentVariable(
                    AspiRuntime.OfflineOperatorApprovalEnvironmentVariable,
                    AspiRuntime.OfflineOperatorApprovalToken,
                    EnvironmentVariableTarget.Process);
                AspiRuntimeContext exact = AspiRuntime.Create(log);
                Equal((byte)1, exact.AdapterCount);
                True(exact.OperationalReplayEnabled);
                True(exact.PredictedPreviewObservationEnabled);
                True(!exact.LoaderDataOutEnabled);
                True(!exact.PreviewImageReadEnabled);
                True(exact.Transport is AspiOperatorSessionTransport);
                ((IDisposable)exact.Transport).Dispose();
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    AspiRuntime.TransportEnvironmentVariable, originalMode,
                    EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(
                    AspiRuntime.OfflineOperatorApprovalEnvironmentVariable,
                    originalApproval, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable(
                    AspiRuntime.ReplayIdentifierEnvironmentVariable,
                    originalIdentifier, EnvironmentVariableTarget.Process);
            }
        }

        private static void TestOfflineWarmOperatorPrefixGuards()
        {
            string setWindowHash = Sha256Hex(PreviewSetWindowData());
            var missingInquiry = new OfflineReplayAspiTransport(null,
                setWindowHash, "FP12345678", 0, true);
            ExpectInvalid(delegate
            {
                missingInquiry.Execute(5, 0,
                    ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            });
            missingInquiry.Dispose();

            var unexpectedD8 = new OfflineReplayAspiTransport(null,
                setWindowHash, "FP12345678", 0, true);
            ExpectInvalid(delegate
            {
                unexpectedD8.Execute(5, 0,
                    ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb(), 66);
            });
            unexpectedD8.Dispose();

            var gate = new AspiOperationalSequenceGate(delegate { return 0; },
                setWindowHash, true);
            gate.BeginOfflineWarmOperatorCycle();
            Equal(AspiOperationalSequenceState.Complete, gate.State);
            Equal(AspiOperationalSequenceGate.MaximumInitializationCycles - 3,
                gate.CompletedInitializationCycles);
            Equal(0, gate.CompletedOfflineImageStreams);
            ExpectInvalid(delegate
            {
                gate.BeginOfflineWarmOperatorCycle();
            });
        }

        private static void TestOfflineOperatorSessionRepeat()
        {
            byte[] setWindow = PreviewSetWindowData();
            string setWindowHash = Sha256Hex(setWindow);
            int factoryCalls = 0;
            var session = new AspiOperatorSessionTransport(delegate
            {
                ++factoryCalls;
                return new OfflineReplayAspiTransport(null, setWindowHash,
                    "FP12345678", 0, factoryCalls > 1);
            });
            try
            {
                CompleteOfflineTransaction(session, setWindow);
                Equal(1, factoryCalls);
                Equal(1, session.CompletedCycles);
                True(!session.Failed);

                CompleteOfflineWarmTransaction(session, setWindow);
                Equal(2, factoryCalls);
                Equal(2, session.CompletedCycles);
                True(!session.Failed);
            }
            finally
            {
                session.Dispose();
            }
        }

        private static void CompleteOfflineTransaction(
            IAspiDataOutTransport transport, byte[] setWindow)
        {
            transport.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb(), 66);
            transport.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            for (int cycle = 0; cycle < 5; ++cycle)
            {
                ReplayInitializationCycle(transport, 2);
            }
            CompleteOfflineImageStream(transport, setWindow, 640, 2);
            transport.Execute(5, 0,
                new byte[] { 0x12, 0, 0, 0, 96, 0 }, 96);
            transport.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            ReplayInitializationCycle(transport, 2);
            CompleteOfflineImageStream(transport, setWindow, 641, 1);
        }

        private static void CompleteOfflineWarmTransaction(
            IAspiDataOutTransport transport, byte[] setWindow)
        {
            transport.Execute(5, 0,
                new byte[] { 0x12, 0, 0, 0, 96, 0 }, 96);
            transport.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            ReplayInitializationCycle(transport, 2);
            CompleteOfflineImageStream(transport, setWindow, 640, 2);
            transport.Execute(5, 0,
                new byte[] { 0x12, 0, 0, 0, 96, 0 }, 96);
            transport.Execute(5, 0,
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb(), 2);
            ReplayInitializationCycle(transport, 2);
            CompleteOfflineImageStream(transport, setWindow, 641, 1);
        }

        private static void ConfigureExecuteSrb(IntPtr srb, byte target,
            byte flags, uint length, IntPtr buffer, byte[] cdb)
        {
            for (int i = 0; i < 96; i++)
            {
                Marshal.WriteByte(srb, i, 0);
            }
            Marshal.WriteByte(srb, AspiConstants.CommandOffset,
                AspiConstants.ExecuteScsiCommand);
            Marshal.WriteByte(srb, AspiConstants.FlagsOffset, flags);
            Marshal.WriteByte(srb, AspiConstants.TargetOffset, target);
            Marshal.WriteInt32(srb, AspiConstants.BufferLengthOffset,
                unchecked((int)length));
            WritePointer32(srb, AspiConstants.BufferPointerOffset, buffer);
            Marshal.WriteByte(srb, AspiConstants.CdbLengthOffset,
                checked((byte)cdb.Length));
            Marshal.Copy(cdb, 0, IntPtr.Add(srb, AspiConstants.CdbOffset),
                cdb.Length);
        }

        private static void WritePointer32(IntPtr srb, int offset, IntPtr value)
        {
            Marshal.WriteInt32(srb, offset, value.ToInt32());
        }

        private static string ReadAscii(IntPtr pointer, int offset, int length)
        {
            byte[] bytes = new byte[length];
            Marshal.Copy(IntPtr.Add(pointer, offset), bytes, 0, length);
            return System.Text.Encoding.ASCII.GetString(bytes).TrimEnd('\0', ' ');
        }

        private static string ReadAscii(byte[] bytes, int offset, int length)
        {
            return System.Text.Encoding.ASCII.GetString(bytes, offset, length).
                TrimEnd('\0', ' ');
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                passed++;
                Console.WriteLine("PASS {0}", name);
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine("FAIL {0}: {1}", name, ex);
            }
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception(string.Format(
                    "Expected {0}, got {1}.", expected, actual));
            }
        }

        private static void True(bool value)
        {
            if (!value)
            {
                throw new Exception("Expected true.");
            }
        }

        [DllImport("wnaspi32.dll", EntryPoint = "GetASPI32SupportInfo",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint NativeGetSupportInfo();

        private sealed class FakeTransport : IAspiReadOnlyTransport
        {
            private readonly Queue<AspiTransportResult> results =
                new Queue<AspiTransportResult>();

            public int CallCount { get; private set; }
            public byte[] LastCdb { get; private set; }
            public byte LastTarget { get; private set; }

            public void Enqueue(byte[] data, byte adapterStatus)
            {
                results.Enqueue(new AspiTransportResult(data, adapterStatus));
            }

            public AspiTransportResult Execute(byte target, byte lun,
                byte[] cdb, uint requestedLength)
            {
                CallCount++;
                LastTarget = target;
                LastCdb = (byte[])cdb.Clone();
                if (results.Count == 0)
                {
                    throw new InvalidOperationException("No fake result queued.");
                }
                return results.Dequeue();
            }
        }

        private sealed class FakeTransparentTransport :
            IAspiTransparentPassThroughTransport
        {
            private readonly Queue<AspiTransportResult> results =
                new Queue<AspiTransportResult>();
            private byte[] lastDataOutReference;

            public int CallCount { get; private set; }
            public byte[] LastCdb { get; private set; }
            public byte[] LastDataOut { get; private set; }
            public bool LastDataOutReferenceCleared
            {
                get
                {
                    if (lastDataOutReference == null)
                    {
                        return false;
                    }
                    for (int index = 0;
                        index < lastDataOutReference.Length; ++index)
                    {
                        if (lastDataOutReference[index] != 0)
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }

            public void Enqueue(AspiTransportResult result)
            {
                results.Enqueue(result);
            }

            public AspiTransportResult Execute(byte target, byte lun,
                byte[] cdb, uint requestedLength)
            {
                ++CallCount;
                LastCdb = (byte[])cdb.Clone();
                return Next();
            }

            public AspiTransportResult ExecuteDataOut(byte target, byte lun,
                byte[] cdb, byte[] data)
            {
                ++CallCount;
                LastCdb = (byte[])cdb.Clone();
                LastDataOut = (byte[])data.Clone();
                lastDataOutReference = data;
                return Next();
            }

            private AspiTransportResult Next()
            {
                if (results.Count == 0)
                {
                    throw new InvalidOperationException(
                        "No transparent result was queued.");
                }
                return results.Dequeue();
            }
        }

        private sealed class BlockingTransparentTransport :
            IAspiTransparentPassThroughTransport, IDisposable
        {
            public readonly ManualResetEvent FirstEntered =
                new ManualResetEvent(false);
            public readonly ManualResetEvent SecondEntered =
                new ManualResetEvent(false);
            public readonly ManualResetEvent ReleaseFirst =
                new ManualResetEvent(false);
            private int calls;

            public int CallCount { get { return calls; } }

            public AspiTransportResult Execute(byte target, byte lun,
                byte[] cdb, uint requestedLength)
            {
                int ordinal = Interlocked.Increment(ref calls);
                if (ordinal == 1)
                {
                    FirstEntered.Set();
                    if (!ReleaseFirst.WaitOne(5000))
                    {
                        throw new TimeoutException(
                            "Serialization test did not release first SRB.");
                    }
                }
                else
                {
                    SecondEntered.Set();
                }
                return new AspiTransportResult(new byte[0],
                    (byte)AdapterStatus.Success, requestedLength, 0);
            }

            public AspiTransportResult ExecuteDataOut(byte target, byte lun,
                byte[] cdb, byte[] data)
            {
                throw new InvalidOperationException(
                    "Serialization fixture does not accept data-out.");
            }

            public void Dispose()
            {
                FirstEntered.Dispose();
                SecondEntered.Dispose();
                ReleaseFirst.Dispose();
            }
        }

        private sealed class FakeOperatorCycleTransport :
            IAspiOperatorCycleTransport
        {
            public int ReadCalls { get; private set; }
            public int DataOutCalls { get; private set; }
            public int DisposeCalls { get; private set; }
            public bool CompleteOnNextDataOut { get; set; }
            public bool CycleCompleted { get; private set; }
            public Exception ReadFailure { get; set; }

            public bool StatefulPreviewSetWindowValidationEnabled
            {
                get { return true; }
            }

            public AspiTransportResult Execute(byte target, byte lun,
                byte[] cdb, uint requestedLength)
            {
                ++ReadCalls;
                if (ReadFailure != null)
                {
                    throw ReadFailure;
                }
                return new AspiTransportResult(new byte[requestedLength],
                    (byte)AdapterStatus.Success);
            }

            public AspiTransportResult ExecuteDataOut(byte target, byte lun,
                byte[] cdb, byte[] data)
            {
                ++DataOutCalls;
                if (CompleteOnNextDataOut)
                {
                    CycleCompleted = true;
                }
                return new AspiTransportResult(new byte[0],
                    (byte)AdapterStatus.Success);
            }

            public void Dispose()
            {
                ++DisposeCalls;
            }
        }

        private sealed class FakeDataOutTransport : IAspiDataOutTransport,
            IAspiPreviewSetWindowValidationPolicy
        {
            public int DataOutCallCount { get; private set; }
            public byte[] LastDataOutCdb { get; private set; }
            public byte[] LastDataReference { get; private set; }
            public Exception DataOutFailure { get; set; }
            public bool StatefulPreviewSetWindowValidationEnabled
            {
                get;
                set;
            }

            public AspiTransportResult Execute(byte target, byte lun,
                byte[] cdb, uint requestedLength)
            {
                throw new InvalidOperationException(
                    "No read result was configured.");
            }

            public AspiTransportResult ExecuteDataOut(byte target, byte lun,
                byte[] cdb, byte[] data)
            {
                DataOutCallCount++;
                LastDataOutCdb = (byte[])cdb.Clone();
                LastDataReference = data;
                if (DataOutFailure != null)
                {
                    throw DataOutFailure;
                }
                return new AspiTransportResult(new byte[0],
                    (byte)AdapterStatus.Success);
            }
        }

        private sealed class StartupFingerprintTransport :
            IAspiDataOutTransport, IAspiStartupWriteFingerprintPolicy
        {
            public int FingerprintCallCount { get; private set; }
            public int DataOutCallCount { get; private set; }

            public AspiTransportResult Execute(byte target, byte lun,
                byte[] cdb, uint requestedLength)
            {
                throw new InvalidOperationException(
                    "No read request is permitted in this test.");
            }

            public AspiTransportResult ExecuteDataOut(byte target, byte lun,
                byte[] cdb, byte[] data)
            {
                ++DataOutCallCount;
                throw new InvalidOperationException(
                    "Fingerprint data reached the data-out transport.");
            }

            public void ObserveStartupWriteFingerprint(byte target, byte lun,
                byte[] cdb, byte[] data)
            {
                Equal((byte)5, target);
                Equal((byte)0, lun);
                Equal(BitConverter.ToString(ScsiFraming.
                    BuildPrecisionTwoStartupWriteBuffer1082Cdb()),
                    BitConverter.ToString(cdb));
                Equal(checked((int)ScsiFraming.
                    PrecisionTwoStartupWriteBuffer1082Length), data.Length);
                ++FingerprintCallCount;
            }
        }

        private sealed class TestUsbLog : IUsbLog
        {
            private readonly List<string> warnings = new List<string>();
            public string LastWarning { get; private set; }
            public string LastInfo { get; private set; }
            public string AllWarnings
            {
                get { return string.Join("\n", warnings.ToArray()); }
            }
            public void Info(string message) { LastInfo = message; }
            public void Trace(string message) { }
            public void Warning(string message)
            {
                LastWarning = message;
                warnings.Add(message);
            }
        }
    }
}
