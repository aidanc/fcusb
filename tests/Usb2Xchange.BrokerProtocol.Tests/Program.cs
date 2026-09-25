// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Usb2Xchange.BrokerProtocol;

namespace Usb2Xchange.BrokerProtocol.Tests
{
    internal static class Program
    {
        private static int passed;
        private static int failed;

        private static int Main()
        {
            Run("request layout and round trip", TestRequestRoundTrip);
            Run("request model is immutable", TestRequestImmutability);
            Run("request captures but execution rejects data-out and vendor CDBs",
                TestBlockedRequests);
            Run("Preview SET WINDOW requires the exact captured envelope and hash",
                TestPreviewSetWindowEnvelope);
            Run("predicted post-window image READ requires the observed envelope",
                TestPredictedPreviewImageReadEnvelope);
            Run("request preserves the FlexColor loader transfer envelope",
                TestLoaderTransferEnvelope);
            Run("loader sequence manifest fingerprints without retaining firmware",
                TestLoaderSequenceManifest);
            Run("loader sequence accepts sixteen ordered records and terminal",
                TestLoaderSequenceExact);
            Run("loader sequence rejects variants and fails closed",
                TestLoaderSequenceFailClosed);
            Run("loader sequence requires exact successful completions",
                TestLoaderSequenceCompletions);
            Run("loader sequence enforces finite monotonic deadlines",
                TestLoaderSequenceDeadlines);
            Run("loader sequence rejects terminal variants and repetition",
                TestLoaderSequenceTerminal);
            Run("observed single-LUN REPORT LUNS is read-only",
                TestReportLuns);
            Run("request rejects invalid address and timeout", TestRequestBounds);
            Run("request parser rejects incompatible headers", TestRequestHeader);
            Run("request parser rejects inconsistent data envelopes",
                TestRequestDataEnvelope);
            Run("request parser rejects nonzero reserved bytes", TestReservedBytes);
            Run("completion layout and short transfer", TestCompletionRoundTrip);
            Run("completion rejects stale request and generation", TestStaleCompletion);
            Run("completion validates selection timeout", TestSelectionTimeout);
            Run("busy and absent targets cannot return data", TestBusyHasNoData);
            Run("completion validates CHECK CONDITION sense", TestCheckCondition);
            Run("failed transport cannot return device data", TestFailedTransport);
            Run("completion parser rejects inconsistent length", TestCompletionLength);
            Run("online scanner state round trip", TestOnlineState);
            Run("offline state round trip", TestOfflineState);
            Run("adapter state rejects non-scanner identity", TestInvalidState);
            Run("service wait is generation bound", TestServiceWait);
            Run("SRB_IO_CONTROL wrapper round trip", TestMiniportWrapper);
            Run("SRB_IO_CONTROL wrapper rejects corruption", TestMiniportWrapperCorruption);
            Run("SRB mapping covers transport and adapter status", TestSrbMapping);
            Run("tracker serializes one outstanding request", TestTrackerQueue);
            Run("tracker cancellation rejects late completion", TestTrackerCancel);
            Run("tracker generation rollover flushes request", TestGenerationRollover);
            Run("tracker timeout is deterministic", TestTrackerTimeout);

            Console.WriteLine("Passed: {0}; Failed: {1}", passed, failed);
            return failed == 0 ? 0 : 1;
        }

        private static void TestRequestRoundTrip()
        {
            BrokerRequest request = InquiryRequest();
            byte[] bytes = BrokerWireProtocol.SerializeRequest(request);
            Equal(BrokerWireProtocol.RequestPrefixSize, bytes.Length);
            Equal((byte)0x55, bytes[0]);
            Equal((byte)0x32, bytes[1]);
            Equal((byte)0x42, bytes[2]);
            Equal((byte)0x58, bytes[3]);
            Equal((byte)2, bytes[6]);
            Equal((byte)1, bytes[8]);
            Equal((byte)80, bytes[12]);
            Equal((byte)0x88, bytes[16]);
            Equal((byte)0x08, bytes[24]);
            Equal((byte)5, bytes[35]);
            Equal((byte)6, bytes[37]);
            Equal((byte)BrokerDirection.In, bytes[38]);
            Equal((byte)36, bytes[40]);
            Equal((byte)0x12, bytes[52]);
            Equal((byte)36, bytes[56]);
            Equal(7, BrokerWireProtocol.SrbIoControlSignature.Length);
            Equal((uint)1, BrokerWireProtocol.SrbControlBrokerMessage);

            BrokerRequest parsed = BrokerWireProtocol.ParseRequest(bytes);
            Equal(request.RequestId, parsed.RequestId);
            Equal(request.Generation, parsed.Generation);
            Equal((byte)5, parsed.PhysicalTargetId);
            Equal((uint)36, parsed.TransferLength);
            Equal((ushort)18, parsed.SenseAllocationLength);
            Equal((byte)0x12, parsed.Cdb[0]);
        }

        private static void TestServiceWait()
        {
            byte[] bytes = BrokerWireProtocol.SerializeServiceWait(
                0x0102030405060708);
            Equal(BrokerWireProtocol.ServiceWaitSize, bytes.Length);
            Equal((byte)4, bytes[8]);
            Equal((byte)32, bytes[12]);
            Equal((byte)0x08, bytes[24]);
            Equal(0x0102030405060708UL,
                BrokerWireProtocol.ParseServiceWait(bytes));
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.SerializeServiceWait(0);
            });
        }

        private static void TestMiniportWrapper()
        {
            byte[] payload = BrokerWireProtocol.SerializeAdapterState(
                new BrokerAdapterState(7, BrokerAdapterStateKind.Offline,
                    0, 0, 0, new byte[0]));
            byte[] bytes = MiniportWireProtocol.WrapMessage(payload);
            Equal(28 + BrokerWireProtocol.AdapterStateSize, bytes.Length);
            Equal((byte)28, bytes[0]);
            Equal((byte)'U', bytes[4]);
            Equal((byte)0, bytes[11]);
            Equal((byte)1, bytes[16]);
            Equal((byte)80, bytes[24]);
            MiniportResponse response =
                MiniportWireProtocol.ParseResponse(bytes);
            Equal(MiniportReturnCode.Success, response.ReturnCode);
            Equal((ulong)7,
                BrokerWireProtocol.ParseAdapterState(response.Payload).
                    Generation);
        }

        private static void TestMiniportWrapperCorruption()
        {
            byte[] payload = BrokerWireProtocol.SerializeServiceWait(1);
            byte[] bytes = MiniportWireProtocol.WrapMessage(payload);
            bytes[4] = (byte)'X';
            Throws<BrokerProtocolException>(delegate
            {
                MiniportWireProtocol.ParseResponse(bytes);
            });

            bytes = MiniportWireProtocol.WrapMessage(payload);
            bytes[24] = 31;
            Throws<BrokerProtocolException>(delegate
            {
                MiniportWireProtocol.ParseResponse(bytes);
            });
        }

        private static void TestRequestImmutability()
        {
            byte[] cdb = InquiryCdb();
            BrokerRequest request = new BrokerRequest(1, 1, 0, 0, 0, 5, 0,
                BrokerDirection.In, 36, 20000, 18, cdb);
            cdb[0] = 0xFF;
            Equal((byte)0x12, request.Cdb[0]);
            byte[] copy = request.Cdb;
            copy[0] = 0xEE;
            Equal((byte)0x12, request.Cdb[0]);
            Equal(0, request.Data.Length);
        }

        private static void TestBlockedRequests()
        {
            BrokerRequest dataOut = new BrokerRequest(1, 1,
                0, 0, 0, 5, 0, BrokerDirection.Out, 1, 1000, 0,
                new byte[] { 0x0A, 0, 0, 0, 1, 0 },
                new byte[] { 0xA5 });
            BrokerRequest parsedDataOut = BrokerWireProtocol.ParseRequest(
                BrokerWireProtocol.SerializeRequest(dataOut));
            Equal(BrokerDirection.Out, parsedDataOut.Direction);
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.ValidateReadOnlyRequest(dataOut);
            });

            BrokerRequest vendor = new BrokerRequest(2, 1,
                0, 0, 0, 5, 0, BrokerDirection.None, 0, 1000, 0,
                new byte[] { 0xE1, 0, 0, 0, 0, 0 });
            BrokerRequest parsedVendor = BrokerWireProtocol.ParseRequest(
                BrokerWireProtocol.SerializeRequest(vendor));
            Equal((byte)0xE1, parsedVendor.Cdb[0]);
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.ValidateReadOnlyRequest(vendor);
            });
        }

        private static void TestLoaderTransferEnvelope()
        {
            byte[] cdb = new byte[] {
                0x3B, 0x01, 0, 0, 0, 0, 0, 0x10, 0x0A, 0
            };
            byte[] data = new byte[0x100A];
            data[0] = 0x01;
            data[1] = 0x30;
            data[2] = 0x00;
            data[40] = 0xFA;
            data[41] = 0xFC;
            BrokerRequest request = new BrokerRequest(25, 1,
                0, 0, 0, 5, 0, BrokerDirection.Out, 0x100A, 60000, 32,
                cdb, data);
            data[0] = 0xFF;
            BrokerRequest parsed = BrokerWireProtocol.ParseRequest(
                BrokerWireProtocol.SerializeRequest(request));
            Equal(BrokerWireProtocol.RequestPrefixSize + 0x100A,
                BrokerWireProtocol.SerializeRequest(request).Length);
            Equal((uint)0x100A, parsed.TransferLength);
            Equal((byte)0x10, parsed.Cdb[7]);
            Equal((byte)0x0A, parsed.Cdb[8]);
            Equal(0x100A, parsed.Data.Length);
            Equal(0x100A, parsed.DataLength);
            Equal((byte)0x01, parsed.Data[0]);
            Equal((byte)0x30, parsed.Data[1]);
            byte[] parsedData = parsed.Data;
            parsedData[0] = 0xEE;
            Equal((byte)0x01, parsed.Data[0]);
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.ValidateReadOnlyRequest(parsed);
            });
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.SerializeRequest(new BrokerRequest(26, 1,
                    0, 0, 0, 5, 0, BrokerDirection.Out,
                    BrokerWireProtocol.MaximumTransferLength + 1U,
                    60000, 32, cdb,
                    new byte[BrokerWireProtocol.MaximumTransferLength + 1]));
            });
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.SerializeRequest(new BrokerRequest(27, 1,
                    0, 0, 0, 5, 0, BrokerDirection.Out, 0x100A,
                    60000, 32, cdb, new byte[0x1000]));
            });
        }

        private static void TestLoaderSequenceManifest()
        {
            byte[] firmware = SyntheticFirmware();
            string firmwareHash = Sha256Hex(firmware);
            byte[] firstRecord = LoaderRecordData(firmware, 0);
            string firstRecordHash = Sha256Hex(firstRecord);
            string terminalHash = Sha256Hex(LoaderTerminalData());

            PrecisionTwoLoaderSequenceManifest manifest =
                PrecisionTwoLoaderSequenceManifest.Create(firmware,
                    firmwareHash.ToLowerInvariant());
            Equal(firmwareHash, manifest.FirmwareSha256);
            Equal(firstRecordHash, manifest.GetRecordSha256(0));
            Equal(terminalHash, manifest.TerminalRecordSha256);
            Equal(65536, PrecisionTwoLoaderSequenceManifest.FirmwareLength);
            Equal(16, PrecisionTwoLoaderSequenceManifest.RecordCount);
            Equal(4106, PrecisionTwoLoaderSequenceManifest.RecordLength);

            firmware[0] ^= 0xFF;
            Equal(firstRecordHash, manifest.GetRecordSha256(0));
            Throws<BrokerProtocolException>(delegate
            {
                PrecisionTwoLoaderSequenceManifest.Create(firmware,
                    firmwareHash);
            });
            Throws<BrokerProtocolException>(delegate
            {
                PrecisionTwoLoaderSequenceManifest.Create(new byte[65535],
                    new string('0', 64));
            });
            Throws<BrokerProtocolException>(delegate
            {
                PrecisionTwoLoaderSequenceManifest.Create(new byte[65536],
                    "not-a-sha256");
            });
            Throws<ArgumentOutOfRangeException>(delegate
            {
                manifest.GetRecordSha256(16);
            });

            System.Reflection.AssemblyName[] references =
                typeof(PrecisionTwoLoaderSequenceValidator).Assembly.
                    GetReferencedAssemblies();
            foreach (System.Reflection.AssemblyName reference in references)
            {
                Equal(false, string.Equals(reference.Name,
                    "Usb2Xchange.WinUsb", StringComparison.Ordinal));
            }
        }

        private static void TestPreviewSetWindowEnvelope()
        {
            byte[] data = new byte[84];
            data[7] = 0x4C;
            string expectedHash = Sha256Hex(data);
            BrokerRequest exact = PreviewSetWindowRequest(data,
                new byte[] { 0x24, 0, 0, 0, 0, 0, 0, 0, 0x54, 0 },
                BrokerDirection.Out, 84, 60000, 32, 5);
            BrokerWireProtocol.ValidateCapturedOperationalPreviewSetWindow(
                exact, expectedHash);
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.ValidateReadOnlyRequest(exact);
            });

            byte[] variantData = (byte[])data.Clone();
            variantData[83] = 1;
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.
                    ValidateCapturedOperationalPreviewSetWindow(
                        PreviewSetWindowRequest(variantData, exact.Cdb,
                            BrokerDirection.Out, 84, 60000, 32, 5),
                        expectedHash);
            });

            byte[] variantCdb = exact.Cdb;
            variantCdb[8] = 0x53;
            BrokerRequest[] variants = new BrokerRequest[]
            {
                PreviewSetWindowRequest(data, variantCdb,
                    BrokerDirection.Out, 84, 60000, 32, 5),
                PreviewSetWindowRequest(data, exact.Cdb,
                    BrokerDirection.In, 84, 60000, 32, 5),
                PreviewSetWindowRequest(data, exact.Cdb,
                    BrokerDirection.Out, 84, 59999, 32, 5),
                PreviewSetWindowRequest(data, exact.Cdb,
                    BrokerDirection.Out, 84, 60000, 31, 5),
                PreviewSetWindowRequest(data, exact.Cdb,
                    BrokerDirection.Out, 84, 60000, 32, 4)
            };
            for (int index = 0; index < variants.Length; index++)
            {
                BrokerRequest variant = variants[index];
                Throws<BrokerProtocolException>(delegate
                {
                    BrokerWireProtocol.
                        ValidateCapturedOperationalPreviewSetWindow(
                            variant, expectedHash);
                });
            }

            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.ValidateCapturedOperationalPreviewSetWindow(
                    exact);
            });
        }

        private static void TestPredictedPreviewImageReadEnvelope()
        {
            BrokerRequest exact = PredictedPreviewImageReadRequest(
                new byte[] { 0x28, 0, 0, 0, 0x28, 0, 0, 0x0F, 0, 0 },
                BrokerDirection.In, 3840, 60000, 32, 5);
            Equal((uint)640, BrokerWireProtocol.
                ValidatePredictedOperationalPreviewImageRead(exact));
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.ValidateReadOnlyRequest(exact);
            });

            byte[] wrongSelector = exact.Cdb;
            wrongSelector[4] = 0x50;
            byte[] wrongEncodedLength = exact.Cdb;
            wrongEncodedLength[8] = 1;
            BrokerRequest[] variants = new BrokerRequest[]
            {
                PredictedPreviewImageReadRequest(wrongSelector,
                    BrokerDirection.In, 3840, 60000, 32, 5),
                PredictedPreviewImageReadRequest(wrongEncodedLength,
                    BrokerDirection.In, 3840, 60000, 32, 5),
                PredictedPreviewImageReadRequest(exact.Cdb,
                    BrokerDirection.Out, 3840, 60000, 32, 5),
                PredictedPreviewImageReadRequest(exact.Cdb,
                    BrokerDirection.In, 3839, 60000, 32, 5),
                PredictedPreviewImageReadRequest(exact.Cdb,
                    BrokerDirection.In, 3840, 59999, 32, 5),
                PredictedPreviewImageReadRequest(exact.Cdb,
                    BrokerDirection.In, 3840, 60000, 31, 5),
                PredictedPreviewImageReadRequest(exact.Cdb,
                    BrokerDirection.In, 3840, 60000, 32, 4)
            };
            for (int index = 0; index < variants.Length; index++)
            {
                BrokerRequest variant = variants[index];
                Throws<BrokerProtocolException>(delegate
                {
                    BrokerWireProtocol.
                        ValidatePredictedOperationalPreviewImageRead(variant);
                });
            }
        }

        private static void TestLoaderSequenceExact()
        {
            byte[] firmware = SyntheticFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                LoaderManifest(firmware);
            PrecisionTwoLoaderSequenceValidator validator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            Equal(PrecisionTwoLoaderSequenceState.Ready, validator.State);

            for (int index = 0;
                index < PrecisionTwoLoaderSequenceManifest.RecordCount;
                index++)
            {
                BrokerRequest request = LoaderRecordRequest(firmware, index,
                    checked((ulong)(100 + index)), 9);
                PrecisionTwoLoaderSequenceStep step =
                    validator.ValidateNext(request);
                Equal(PrecisionTwoLoaderSequenceStepKind.FirmwareRecord,
                    step.Kind);
                Equal(index, step.RecordIndex);
                Equal(checked((ushort)(0x3000 + (index * 0x100))),
                    step.AddressLikeValue);
                Equal(manifest.GetRecordSha256(index), step.Sha256);
                Equal(index, validator.AcceptedRecordCount);
                Equal(true, validator.CompletionPending);
                PrecisionTwoLoaderSequenceStep completed =
                    validator.ValidateCompletion(
                        LoaderSuccessCompletion(request));
                Equal(step.Kind, completed.Kind);
                Equal(step.RecordIndex, completed.RecordIndex);
                Equal(index + 1, validator.AcceptedRecordCount);
                Equal(false, validator.CompletionPending);
                Equal((ulong)9, validator.Generation);
                Equal(PrecisionTwoLoaderSequenceState.Records,
                    validator.State);
            }

            BrokerRequest terminalRequest = LoaderTerminalRequest(200, 9);
            PrecisionTwoLoaderSequenceStep terminal =
                validator.ValidateNext(terminalRequest);
            Equal(PrecisionTwoLoaderSequenceStepKind.TerminalRecord,
                terminal.Kind);
            Equal(-1, terminal.RecordIndex);
            Equal((ushort)0x3001, terminal.AddressLikeValue);
            Equal(manifest.TerminalRecordSha256, terminal.Sha256);
            Equal(PrecisionTwoLoaderSequenceState.Records, validator.State);
            Equal(true, validator.CompletionPending);
            validator.ValidateCompletion(
                LoaderSuccessCompletion(terminalRequest));
            Equal(PrecisionTwoLoaderSequenceState.Complete,
                validator.State);
            Equal(false, validator.CompletionPending);
            Equal(16, validator.AcceptedRecordCount);
        }

        private static void TestLoaderSequenceFailClosed()
        {
            byte[] firmware = SyntheticFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                LoaderManifest(firmware);

            byte[] wrongPayload = LoaderRecordData(firmware, 0);
            wrongPayload[100] ^= 1;
            PrecisionTwoLoaderSequenceValidator payloadValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            Throws<BrokerProtocolException>(delegate
            {
                payloadValidator.ValidateNext(LoaderRequest(1, 4,
                    wrongPayload, 5, 60000));
            });
            Equal(PrecisionTwoLoaderSequenceState.Failed,
                payloadValidator.State);
            Throws<BrokerProtocolException>(delegate
            {
                payloadValidator.ValidateNext(
                    LoaderRecordRequest(firmware, 0, 2, 4));
            });

            PrecisionTwoLoaderSequenceValidator orderValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            Throws<BrokerProtocolException>(delegate
            {
                orderValidator.ValidateNext(
                    LoaderRecordRequest(firmware, 1, 3, 4));
            });

            PrecisionTwoLoaderSequenceValidator targetValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            Throws<BrokerProtocolException>(delegate
            {
                targetValidator.ValidateNext(LoaderRequest(4, 4,
                    LoaderRecordData(firmware, 0), 4, 60000));
            });

            PrecisionTwoLoaderSequenceValidator generationValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            BrokerRequest generationFirst =
                LoaderRecordRequest(firmware, 0, 10, 4);
            generationValidator.ValidateNext(generationFirst);
            generationValidator.ValidateCompletion(
                LoaderSuccessCompletion(generationFirst));
            Throws<BrokerProtocolException>(delegate
            {
                generationValidator.ValidateNext(
                    LoaderRecordRequest(firmware, 1, 11, 5));
            });

            PrecisionTwoLoaderSequenceValidator requestIdValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            BrokerRequest requestIdFirst =
                LoaderRecordRequest(firmware, 0, 20, 4);
            requestIdValidator.ValidateNext(requestIdFirst);
            requestIdValidator.ValidateCompletion(
                LoaderSuccessCompletion(requestIdFirst));
            Throws<BrokerProtocolException>(delegate
            {
                requestIdValidator.ValidateNext(
                    LoaderRecordRequest(firmware, 1, 20, 4));
            });

            PrecisionTwoLoaderSequenceValidator timeoutValidator =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            Throws<BrokerProtocolException>(delegate
            {
                timeoutValidator.ValidateNext(LoaderRequest(30, 4,
                    LoaderRecordData(firmware, 0), 5, 59999));
            });
        }

        private static void TestLoaderSequenceCompletions()
        {
            byte[] firmware = SyntheticFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                LoaderManifest(firmware);

            PrecisionTwoLoaderSequenceValidator noPending =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            BrokerRequest first = LoaderRecordRequest(firmware, 0, 1, 6);
            Throws<BrokerProtocolException>(delegate
            {
                noPending.ValidateCompletion(LoaderSuccessCompletion(first));
            });
            Equal(PrecisionTwoLoaderSequenceState.Failed, noPending.State);

            PrecisionTwoLoaderSequenceValidator missingCompletion =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            missingCompletion.ValidateNext(first);
            Throws<BrokerProtocolException>(delegate
            {
                missingCompletion.ValidateNext(
                    LoaderRecordRequest(firmware, 1, 2, 6));
            });
            Equal(PrecisionTwoLoaderSequenceState.Failed,
                missingCompletion.State);

            PrecisionTwoLoaderSequenceValidator transportFailure =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            transportFailure.ValidateNext(first);
            Throws<BrokerProtocolException>(delegate
            {
                transportFailure.ValidateCompletion(new BrokerCompletion(
                    first.RequestId, first.Generation,
                    BrokerTransportStatus.ProtocolError, 0, 0, 0, 0,
                    new byte[0], new byte[0]));
            });
            Equal(PrecisionTwoLoaderSequenceState.Failed,
                transportFailure.State);

            PrecisionTwoLoaderSequenceValidator shortTransfer =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            shortTransfer.ValidateNext(first);
            Throws<BrokerProtocolException>(delegate
            {
                shortTransfer.ValidateCompletion(new BrokerCompletion(
                    first.RequestId, first.Generation,
                    BrokerTransportStatus.Success, 0, 0,
                    first.TransferLength - 1, 1,
                    new byte[0], new byte[0]));
            });
            Equal(PrecisionTwoLoaderSequenceState.Failed,
                shortTransfer.State);

            PrecisionTwoLoaderSequenceValidator staleCompletion =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            staleCompletion.ValidateNext(first);
            Throws<BrokerProtocolException>(delegate
            {
                staleCompletion.ValidateCompletion(new BrokerCompletion(
                    first.RequestId + 1, first.Generation,
                    BrokerTransportStatus.Success, 0, 0,
                    first.TransferLength, 0,
                    new byte[0], new byte[0]));
            });
            Equal(PrecisionTwoLoaderSequenceState.Failed,
                staleCompletion.State);
        }

        private static void TestLoaderSequenceDeadlines()
        {
            byte[] firmware = SyntheticFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                LoaderManifest(firmware);
            long now = 100;
            PrecisionTwoLoaderSequenceValidator exactDeadline =
                new PrecisionTwoLoaderSequenceValidator(manifest,
                    delegate { return now; });
            BrokerRequest first = LoaderRecordRequest(firmware, 0, 1, 7);
            exactDeadline.ValidateNext(first);
            now += PrecisionTwoLoaderSequenceValidator.
                MaximumCompletionWaitMilliseconds;
            exactDeadline.ValidateCompletion(LoaderSuccessCompletion(first));
            Equal(1, exactDeadline.AcceptedRecordCount);

            now = 0;
            PrecisionTwoLoaderSequenceValidator expiredCompletion =
                new PrecisionTwoLoaderSequenceValidator(manifest,
                    delegate { return now; });
            expiredCompletion.ValidateNext(first);
            now = PrecisionTwoLoaderSequenceValidator.
                MaximumCompletionWaitMilliseconds + 1;
            Throws<BrokerProtocolException>(delegate
            {
                expiredCompletion.ValidateCompletion(
                    LoaderSuccessCompletion(first));
            });
            Equal(PrecisionTwoLoaderSequenceState.Failed,
                expiredCompletion.State);
            Equal(false, expiredCompletion.CompletionPending);

            now = 10;
            PrecisionTwoLoaderSequenceValidator backwardsClock =
                new PrecisionTwoLoaderSequenceValidator(manifest,
                    delegate { return now; });
            backwardsClock.ValidateNext(first);
            now = 9;
            Throws<BrokerProtocolException>(delegate
            {
                backwardsClock.ValidateCompletion(
                    LoaderSuccessCompletion(first));
            });
            Equal(PrecisionTwoLoaderSequenceState.Failed,
                backwardsClock.State);

            now = 0;
            PrecisionTwoLoaderSequenceValidator totalDeadline =
                new PrecisionTwoLoaderSequenceValidator(manifest,
                    delegate { return now; });
            totalDeadline.ValidateNext(first);
            totalDeadline.ValidateCompletion(LoaderSuccessCompletion(first));
            now = PrecisionTwoLoaderSequenceValidator.
                MaximumSequenceDurationMilliseconds + 1;
            Throws<BrokerProtocolException>(delegate
            {
                totalDeadline.ValidateNext(
                    LoaderRecordRequest(firmware, 1, 2, 7));
            });
            Equal(PrecisionTwoLoaderSequenceState.Failed,
                totalDeadline.State);

            now = 0;
            PrecisionTwoLoaderSequenceValidator failClosed =
                new PrecisionTwoLoaderSequenceValidator(manifest,
                    delegate { return now; });
            failClosed.ValidateNext(first);
            failClosed.FailClosed();
            Equal(PrecisionTwoLoaderSequenceState.Failed, failClosed.State);
            Equal(false, failClosed.CompletionPending);
            Throws<BrokerProtocolException>(delegate
            {
                failClosed.ValidateCompletion(LoaderSuccessCompletion(first));
            });
        }

        private static void TestLoaderSequenceTerminal()
        {
            byte[] firmware = SyntheticFirmware();
            PrecisionTwoLoaderSequenceManifest manifest =
                LoaderManifest(firmware);
            PrecisionTwoLoaderSequenceValidator wrongTerminal =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FeedLoaderRecords(wrongTerminal, firmware, 8);
            byte[] terminal = LoaderTerminalData();
            terminal[4] = 0x0F;
            Throws<BrokerProtocolException>(delegate
            {
                wrongTerminal.ValidateNext(
                    LoaderRequest(100, 8, terminal, 5, 60000));
            });
            Equal(PrecisionTwoLoaderSequenceState.Failed,
                wrongTerminal.State);

            PrecisionTwoLoaderSequenceValidator complete =
                new PrecisionTwoLoaderSequenceValidator(manifest);
            FeedLoaderRecords(complete, firmware, 8);
            BrokerRequest terminalRequest = LoaderTerminalRequest(100, 8);
            complete.ValidateNext(terminalRequest);
            complete.ValidateCompletion(
                LoaderSuccessCompletion(terminalRequest));
            Throws<BrokerProtocolException>(delegate
            {
                complete.ValidateNext(LoaderTerminalRequest(101, 8));
            });
            Equal(PrecisionTwoLoaderSequenceState.Failed, complete.State);
        }

        private static PrecisionTwoLoaderSequenceManifest LoaderManifest(
            byte[] firmware)
        {
            return PrecisionTwoLoaderSequenceManifest.Create(firmware,
                Sha256Hex(firmware));
        }

        private static void FeedLoaderRecords(
            PrecisionTwoLoaderSequenceValidator validator, byte[] firmware,
            ulong generation)
        {
            for (int index = 0;
                index < PrecisionTwoLoaderSequenceManifest.RecordCount;
                index++)
            {
                BrokerRequest request = LoaderRecordRequest(firmware, index,
                    checked((ulong)(1 + index)), generation);
                validator.ValidateNext(request);
                validator.ValidateCompletion(LoaderSuccessCompletion(request));
            }
        }

        private static BrokerCompletion LoaderSuccessCompletion(
            BrokerRequest request)
        {
            return new BrokerCompletion(request.RequestId,
                request.Generation, BrokerTransportStatus.Success, 0, 0,
                request.TransferLength, 0, new byte[0], new byte[0]);
        }

        private static BrokerRequest LoaderRecordRequest(byte[] firmware,
            int recordIndex, ulong requestId, ulong generation)
        {
            return LoaderRequest(requestId, generation,
                LoaderRecordData(firmware, recordIndex), 5, 60000);
        }

        private static BrokerRequest LoaderTerminalRequest(ulong requestId,
            ulong generation)
        {
            return LoaderRequest(requestId, generation, LoaderTerminalData(),
                5, 60000);
        }

        private static BrokerRequest LoaderRequest(ulong requestId,
            ulong generation, byte[] data, byte physicalTarget,
            uint timeoutMilliseconds)
        {
            int length = data.Length;
            byte[] cdb = new byte[]
            {
                0x3B, 0x01, 0x00, 0x00, 0x00,
                0x00, (byte)((length >> 16) & 0xFF),
                (byte)((length >> 8) & 0xFF),
                (byte)(length & 0xFF), 0x00
            };
            return new BrokerRequest(requestId, generation, 0, 0, 0,
                physicalTarget, 0, BrokerDirection.Out,
                checked((uint)length),  timeoutMilliseconds, 32, cdb, data);
        }

        private static byte[] LoaderRecordData(byte[] firmware,
            int recordIndex)
        {
            ushort address = checked((ushort)(0x3000 + (recordIndex * 0x100)));
            byte[] data = new byte[4106];
            data[0] = 0x01;
            data[1] = (byte)(address >> 8);
            data[2] = (byte)(address & 0xFF);
            Buffer.BlockCopy(firmware, recordIndex * 4096, data, 10, 4096);
            return data;
        }

        private static byte[] LoaderTerminalData()
        {
            return new byte[]
            {
                0x00, 0x30, 0x01, 0x00, 0x0E,
                0x00, 0x00, 0x00, 0x00, 0x00
            };
        }

        private static byte[] SyntheticFirmware()
        {
            byte[] firmware = new byte[65536];
            for (int index = 0; index < firmware.Length; index++)
            {
                firmware[index] = (byte)(((index * 31) + (index / 17)) & 0xFF);
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

        private static void TestReportLuns()
        {
            BrokerRequest request = new BrokerRequest(3, 1,
                0, 0, 0, 5, 0, BrokerDirection.In, 16, 1000, 0,
                new byte[] {
                    0xA0, 0, 0, 0, 0, 0, 0, 0, 0, 16, 0, 0
                });
            BrokerWireProtocol.ValidateReadOnlyRequest(request);
            BrokerRequest parsed = BrokerWireProtocol.ParseRequest(
                BrokerWireProtocol.SerializeRequest(request));
            Equal((byte)0xA0, parsed.Cdb[0]);
            Equal(12, parsed.Cdb.Length);

            byte[] wrong = request.Cdb;
            wrong[9] = 32;
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.ValidateReadOnlyRequest(
                    new BrokerRequest(4, 1, 0, 0, 0, 5, 0,
                        BrokerDirection.In, 16, 1000, 0, wrong));
            });
        }

        private static void TestRequestBounds()
        {
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.SerializeRequest(new BrokerRequest(1, 1,
                    0, 1, 0, 5, 0, BrokerDirection.In, 36, 1000, 0,
                    InquiryCdb()));
            });
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.SerializeRequest(new BrokerRequest(1, 1,
                    0, 0, 0, 5, 0, BrokerDirection.In, 36, 60001, 0,
                    InquiryCdb()));
            });
            BrokerWireProtocol.SerializeRequest(new BrokerRequest(1, 1,
                0, 0, 0, 5, 0, BrokerDirection.In, 36, 60000, 0,
                InquiryCdb()));
            Throws<BrokerProtocolException>(delegate
            {
                byte[] cdb = InquiryCdb();
                cdb[4] = 35;
                BrokerWireProtocol.ValidateReadOnlyRequest(
                    new BrokerRequest(1, 1,
                    0, 0, 0, 5, 0, BrokerDirection.In, 36, 1000, 0, cdb));
            });
        }

        private static void TestRequestHeader()
        {
            byte[] bytes = BrokerWireProtocol.SerializeRequest(InquiryRequest());
            bytes[6] = 3;
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.ParseRequest(bytes);
            });

            bytes = BrokerWireProtocol.SerializeRequest(InquiryRequest());
            bytes[12] = 79;
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.ParseRequest(bytes);
            });
        }

        private static void TestRequestDataEnvelope()
        {
            byte[] cdb = new byte[] {
                0x3B, 0x01, 0, 0, 0, 0, 0, 0, 1, 0
            };
            byte[] bytes = BrokerWireProtocol.SerializeRequest(
                new BrokerRequest(30, 1, 0, 0, 0, 5, 0,
                    BrokerDirection.Out, 1, 1000, 0, cdb,
                    new byte[] { 0xA5 }));
            bytes[68] = 2;
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.ParseRequest(bytes);
            });

            bytes = BrokerWireProtocol.SerializeRequest(
                new BrokerRequest(31, 1, 0, 0, 0, 5, 0,
                    BrokerDirection.Out, 1, 1000, 0, cdb,
                    new byte[] { 0xA5 }));
            bytes[40] = 2;
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.ParseRequest(bytes);
            });

            bytes = BrokerWireProtocol.SerializeRequest(
                new BrokerRequest(32, 1, 0, 0, 0, 5, 0,
                    BrokerDirection.Out, 1, 1000, 0, cdb,
                    new byte[] { 0xA5 }));
            bytes[38] = (byte)BrokerDirection.In;
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.ParseRequest(bytes);
            });
        }

        private static void TestReservedBytes()
        {
            byte[] bytes = BrokerWireProtocol.SerializeRequest(InquiryRequest());
            bytes[79] = 1;
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.ParseRequest(bytes);
            });
        }

        private static void TestCompletionRoundTrip()
        {
            BrokerRequest request = InquiryRequest();
            byte[] data = new byte[32];
            data[8] = (byte)'I';
            BrokerCompletion completion = new BrokerCompletion(
                request.RequestId, request.Generation,
                BrokerTransportStatus.Success, 0, 0, 32, 4, new byte[0], data);
            byte[] bytes = BrokerWireProtocol.SerializeCompletion(request,
                completion);
            Equal(BrokerWireProtocol.CompletionPrefixSize + 32, bytes.Length);
            Equal((byte)2, bytes[8]);
            Equal((byte)32, bytes[40]);
            Equal((byte)4, bytes[44]);
            Equal((byte)32, bytes[48]);
            Equal((byte)'I', bytes[96]);

            BrokerCompletion parsed = BrokerWireProtocol.ParseCompletion(
                request, bytes);
            Equal((uint)32, parsed.TransferLength);
            Equal((uint)4, parsed.Residue);
            Equal((byte)'I', parsed.Data[8]);
        }

        private static void TestStaleCompletion()
        {
            BrokerRequest request = InquiryRequest();
            BrokerCompletion wrongId = new BrokerCompletion(9,
                request.Generation, BrokerTransportStatus.Success, 0, 0, 36,
                0, new byte[0], new byte[36]);
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.SerializeCompletion(request, wrongId);
            });
            BrokerCompletion wrongGeneration = new BrokerCompletion(
                request.RequestId, 99, BrokerTransportStatus.Success, 0, 0,
                36, 0, new byte[0], new byte[36]);
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.SerializeCompletion(request,
                    wrongGeneration);
            });
        }

        private static void TestBusyHasNoData()
        {
            BrokerRequest request = InquiryRequest();
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.SerializeCompletion(request,
                    new BrokerCompletion(request.RequestId,
                        request.Generation, BrokerTransportStatus.Success,
                        0x08, 0x08, 1, 35, new byte[0], new byte[1]));
            });
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.SerializeCompletion(request,
                    new BrokerCompletion(request.RequestId,
                        request.Generation, BrokerTransportStatus.Success,
                        0x8A, 0, 1, 35, new byte[0], new byte[1]));
            });
        }

        private static void TestSelectionTimeout()
        {
            BrokerRequest request = InquiryRequest();
            BrokerCompletion completion = new BrokerCompletion(
                request.RequestId, request.Generation,
                BrokerTransportStatus.Success, 0x8A, 0, 0, 36,
                new byte[0], new byte[0]);
            BrokerCompletion parsed = BrokerWireProtocol.ParseCompletion(
                request, BrokerWireProtocol.SerializeCompletion(request,
                    completion));
            Equal((byte)0x8A, parsed.AdapterStatus);
            Equal((uint)36, parsed.Residue);
        }

        private static void TestCheckCondition()
        {
            BrokerRequest request = TestUnitReadyRequest();
            byte[] sense = new byte[18];
            sense[0] = 0x70;
            BrokerCompletion completion = new BrokerCompletion(
                request.RequestId, request.Generation,
                BrokerTransportStatus.Success, 0x02, 0x02, 0, 0, sense,
                new byte[0]);
            BrokerCompletion parsed = BrokerWireProtocol.ParseCompletion(
                request, BrokerWireProtocol.SerializeCompletion(request,
                    completion));
            Equal(18, parsed.Sense.Length);
            Equal((byte)0x70, parsed.Sense[0]);
        }

        private static void TestFailedTransport()
        {
            BrokerRequest request = InquiryRequest();
            BrokerCompletion cleanFailure = new BrokerCompletion(
                request.RequestId, request.Generation,
                BrokerTransportStatus.NoDevice, 0, 0, 0, 0, new byte[0],
                new byte[0]);
            BrokerWireProtocol.SerializeCompletion(request, cleanFailure);

            BrokerCompletion dirtyFailure = new BrokerCompletion(
                request.RequestId, request.Generation,
                BrokerTransportStatus.IoError, 0, 0, 1, 0, new byte[0],
                new byte[] { 1 });
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.SerializeCompletion(request, dirtyFailure);
            });
        }

        private static void TestCompletionLength()
        {
            BrokerRequest request = InquiryRequest();
            BrokerCompletion completion = new BrokerCompletion(
                request.RequestId, request.Generation,
                BrokerTransportStatus.Success, 0, 0, 36, 0, new byte[0],
                new byte[36]);
            byte[] bytes = BrokerWireProtocol.SerializeCompletion(request,
                completion);
            bytes[48] = 35;
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.ParseCompletion(request, bytes);
            });
        }

        private static void TestOnlineState()
        {
            byte[] inquiry = ScannerInquiry();
            BrokerAdapterState state = new BrokerAdapterState(7,
                BrokerAdapterStateKind.Online, 5, 0, 6, inquiry);
            byte[] bytes = BrokerWireProtocol.SerializeAdapterState(state);
            Equal(BrokerWireProtocol.AdapterStateSize, bytes.Length);
            Equal((byte)3, bytes[8]);
            Equal((byte)1, bytes[32]);
            Equal((byte)5, bytes[36]);
            Equal((byte)36, bytes[40]);
            BrokerAdapterState parsed =
                BrokerWireProtocol.ParseAdapterState(bytes);
            Equal(BrokerAdapterStateKind.Online, parsed.State);
            Equal((byte)5, parsed.PhysicalTargetId);
            Equal((byte)'I', parsed.Inquiry[8]);
        }

        private static void TestOfflineState()
        {
            BrokerAdapterState state = new BrokerAdapterState(8,
                BrokerAdapterStateKind.Offline, 0, 0, 0, new byte[0]);
            BrokerAdapterState parsed = BrokerWireProtocol.ParseAdapterState(
                BrokerWireProtocol.SerializeAdapterState(state));
            Equal(BrokerAdapterStateKind.Offline, parsed.State);
            Equal(0, parsed.Inquiry.Length);
        }

        private static void TestInvalidState()
        {
            byte[] inquiry = ScannerInquiry();
            inquiry[0] = 0;
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.SerializeAdapterState(
                    new BrokerAdapterState(1, BrokerAdapterStateKind.Online,
                        5, 0, 6, inquiry));
            });
            Throws<BrokerProtocolException>(delegate
            {
                BrokerWireProtocol.SerializeAdapterState(
                    new BrokerAdapterState(1, BrokerAdapterStateKind.Offline,
                        5, 0, 6, ScannerInquiry()));
            });
        }

        private static void TestSrbMapping()
        {
            BrokerRequest request = InquiryRequest();
            Equal(BrokerSrbStatus.Success, Map(request,
                BrokerTransportStatus.Success, 0, 0, 36, 0).SrbStatus);
            Equal(BrokerSrbStatus.Busy, Map(request,
                BrokerTransportStatus.Success, 0x08, 0x08, 0, 36).SrbStatus);
            Equal(BrokerSrbStatus.SelectionTimeout, Map(request,
                BrokerTransportStatus.Success, 0x8A, 0, 0, 36).SrbStatus);
            Equal(BrokerSrbStatus.NoDevice, MapTransportFailure(request,
                BrokerTransportStatus.NoDevice).SrbStatus);
            Equal(BrokerSrbStatus.Timeout, MapTransportFailure(request,
                BrokerTransportStatus.Timeout).SrbStatus);
            Equal(BrokerSrbStatus.Aborted, MapTransportFailure(request,
                BrokerTransportStatus.Cancelled).SrbStatus);
            Equal(BrokerSrbStatus.InvalidRequest, MapTransportFailure(request,
                BrokerTransportStatus.Blocked).SrbStatus);
            Equal(BrokerSrbStatus.RequestFlushed, MapTransportFailure(request,
                BrokerTransportStatus.StaleGeneration).SrbStatus);
            Equal(BrokerSrbStatus.Error, MapTransportFailure(request,
                BrokerTransportStatus.ProtocolError).SrbStatus);
        }

        private static void TestTrackerQueue()
        {
            BrokerRequestTracker tracker = OnlineTracker(1);
            BrokerRequest request = tracker.Queue(0, 0, 0,
                BrokerDirection.In, 36, 20000, 18, InquiryCdb(), 100);
            Equal((ulong)1, request.RequestId);
            Equal((ulong)1, request.Generation);
            Equal((byte)5, request.PhysicalTargetId);
            Throws<InvalidOperationException>(delegate
            {
                tracker.Queue(0, 0, 0, BrokerDirection.In, 36, 20000, 18,
                    InquiryCdb(), 101);
            });
            Equal(request.RequestId, tracker.Dispatch().RequestId);
            Throws<InvalidOperationException>(delegate { tracker.Dispatch(); });
            BrokerCompletion completion = new BrokerCompletion(
                request.RequestId, request.Generation,
                BrokerTransportStatus.Success, 0, 0, 36, 0, new byte[0],
                new byte[36]);
            Equal(BrokerSrbStatus.Success,
                tracker.Complete(completion).SrbStatus);
            Equal(false, tracker.HasPendingRequest);
        }

        private static void TestTrackerCancel()
        {
            BrokerRequestTracker tracker = OnlineTracker(1);
            BrokerRequest request = tracker.Queue(0, 0, 0,
                BrokerDirection.None, 0, 1000, 18,
                new byte[] { 0, 0, 0, 0, 0, 0 }, 0);
            tracker.Dispatch();
            Equal(BrokerSrbStatus.Aborted,
                tracker.Cancel(request.RequestId).SrbStatus);
            Throws<InvalidOperationException>(delegate
            {
                tracker.Complete(new BrokerCompletion(request.RequestId,
                    request.Generation, BrokerTransportStatus.Success, 0, 0,
                    0, 0, new byte[0], new byte[0]));
            });
        }

        private static void TestGenerationRollover()
        {
            BrokerRequestTracker tracker = OnlineTracker(3);
            BrokerRequest request = tracker.Queue(0, 0, 0,
                BrokerDirection.In, 36, 1000, 18, InquiryCdb(), 0);
            tracker.Dispatch();
            BrokerSrbResult flushed = tracker.ApplyAdapterState(
                new BrokerAdapterState(4, BrokerAdapterStateKind.Offline,
                    0, 0, 0, new byte[0]));
            Equal(BrokerSrbStatus.RequestFlushed, flushed.SrbStatus);
            Equal(false, tracker.IsOnline);
            Throws<InvalidOperationException>(delegate
            {
                tracker.Complete(new BrokerCompletion(request.RequestId,
                    request.Generation, BrokerTransportStatus.Success, 0, 0,
                    36, 0, new byte[0], new byte[36]));
            });
            Throws<BrokerProtocolException>(delegate
            {
                tracker.ApplyAdapterState(new BrokerAdapterState(4,
                    BrokerAdapterStateKind.Offline, 0, 0, 0, new byte[0]));
            });
        }

        private static void TestTrackerTimeout()
        {
            BrokerRequestTracker tracker = OnlineTracker(1);
            tracker.Queue(0, 0, 0, BrokerDirection.None, 0, 1000, 18,
                new byte[] { 0, 0, 0, 0, 0, 0 }, 5000);
            Equal<BrokerSrbResult>(null, tracker.Expire(5999));
            Equal(BrokerSrbStatus.Timeout, tracker.Expire(6000).SrbStatus);
            Equal(false, tracker.HasPendingRequest);
            Throws<InvalidOperationException>(delegate { tracker.Dispatch(); });
        }

        private static BrokerSrbResult Map(BrokerRequest request,
            BrokerTransportStatus transportStatus, byte adapterStatus,
            byte scsiStatus, uint transferLength, uint residue)
        {
            byte[] data = transferLength == 0 ? new byte[0] :
                new byte[checked((int)transferLength)];
            return BrokerSrbMapper.Map(request, new BrokerCompletion(
                request.RequestId, request.Generation, transportStatus,
                adapterStatus, scsiStatus, transferLength, residue,
                new byte[0], data));
        }

        private static BrokerSrbResult MapTransportFailure(
            BrokerRequest request, BrokerTransportStatus status)
        {
            return BrokerSrbMapper.Map(request, new BrokerCompletion(
                request.RequestId, request.Generation, status, 0, 0, 0, 0,
                new byte[0], new byte[0]));
        }

        private static BrokerRequestTracker OnlineTracker(ulong generation)
        {
            BrokerRequestTracker tracker = new BrokerRequestTracker();
            tracker.ApplyAdapterState(new BrokerAdapterState(generation,
                BrokerAdapterStateKind.Online, 5, 0, 6, ScannerInquiry()));
            return tracker;
        }

        private static BrokerRequest InquiryRequest()
        {
            return new BrokerRequest(0x1122334455667788,
                0x0102030405060708, 0, 0, 0, 5, 0, BrokerDirection.In, 36,
                20000, 18, InquiryCdb());
        }

        private static BrokerRequest TestUnitReadyRequest()
        {
            return new BrokerRequest(1, 2, 0, 0, 0, 5, 0,
                BrokerDirection.None, 0, 20000, 18,
                new byte[] { 0, 0, 0, 0, 0, 0 });
        }

        private static BrokerRequest PreviewSetWindowRequest(byte[] data,
            byte[] cdb, BrokerDirection direction, uint transferLength,
            uint timeoutMilliseconds, ushort senseLength,
            byte physicalTargetId)
        {
            return new BrokerRequest(481, 3, 0, 0, 0, physicalTargetId, 0,
                direction, transferLength, timeoutMilliseconds, senseLength,
                cdb, data);
        }

        private static BrokerRequest PredictedPreviewImageReadRequest(
            byte[] cdb, BrokerDirection direction, uint transferLength,
            uint timeoutMilliseconds, ushort senseLength,
            byte physicalTargetId)
        {
            return new BrokerRequest(482, 3, 0, 0, 0, physicalTargetId, 0,
                direction, transferLength, timeoutMilliseconds, senseLength,
                cdb);
        }

        private static byte[] InquiryCdb()
        {
            return new byte[] { 0x12, 0, 0, 0, 36, 0 };
        }

        private static byte[] ScannerInquiry()
        {
            byte[] bytes = new byte[36];
            bytes[0] = 0x06;
            bytes[8] = (byte)'I';
            return bytes;
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
                Console.WriteLine("FAIL {0}: {1}", name, ex.Message);
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

        private static void Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            throw new Exception("Expected exception " + typeof(T).Name + ".");
        }
    }
}
