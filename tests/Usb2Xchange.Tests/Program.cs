// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Usb2Xchange.Protocol;

namespace Usb2Xchange.Tests
{
    internal static class Program
    {
        private static int passed;
        private static int failed;

        private static int Main()
        {
            Run("firmware parses records and terminator", TestFirmwareParse);
            Run("firmware model is immutable", TestFirmwareImmutability);
            Run("firmware rejects non-multiple length", TestFirmwareBadLength);
            Run("firmware rejects oversized record", TestFirmwareOversizedRecord);
            Run("firmware requires final terminator", TestFirmwareTerminator);
            Run("CBW fields and target/LUN encoding", TestCommandWrapper);
            Run("CBW rejects target outside verified scan range", TestCommandTargetRange);
            Run("CBW validates direction", TestCommandDirection);
            Run("Precision II loader READ BUFFER D8 is exact",
                TestPrecisionTwoLoaderReadBufferD8);
            Run("Precision II ScannerReady poll is exact",
                TestPrecisionTwoScannerReady);
            Run("Precision II fault-pixel READ BUFFER is exact",
                TestPrecisionTwoFaultPixelReadBuffer);
            Run("Precision II startup configuration CDBs are exact",
                TestPrecisionTwoStartupConfigurationCdbs);
            Run("Precision II Preview SET WINDOW is exact",
                TestPrecisionTwoPreviewSetWindow);
            Run("Precision II predicted Preview image READ is exact",
                TestPrecisionTwoPreviewImageRead);
            Run("Precision II Preview command manifest is exact",
                TestPrecisionTwoPreviewCommandManifest);
            Run("Precision II loader zero-length WRITE BUFFER is exact",
                TestPrecisionTwoLoaderWriteBufferModeOneZero);
            Run("Precision II loader first-record WRITE BUFFER is exact",
                TestPrecisionTwoLoaderFirstRecord);
            Run("Precision II loader terminal WRITE BUFFER is exact",
                TestPrecisionTwoLoaderTerminalRecord);
            Run("Precision II known loader manifest is pinned",
                TestPrecisionTwoKnownLoaderManifest);
            Run("Precision II loader manifest validates complete fixtures",
                TestPrecisionTwoLoaderManifestFixtures);
            Run("CSW parses and validates", TestStatusWrapper);
            Run("CSW rejects bad signature", TestStatusSignature);
            Run("CSW rejects bad tag", TestStatusTag);
            Run("CSW rejects impossible residue", TestStatusResidue);
            Run("INQUIRY parser extracts identity", TestInquiry);

            Console.WriteLine("Passed: {0}; Failed: {1}", passed, failed);
            return failed == 0 ? 0 : 1;
        }

        private static void TestFirmwareParse()
        {
            byte[] bytes = BuildFirmware(
                new SyntheticRecord(0x0100, new byte[] { 1, 2, 3 }),
                new SyntheticRecord(0x0200, new byte[] { 4, 5 }));
            FirmwareImage image = FirmwareImage.Parse(bytes);
            Equal(2, image.Records.Count);
            Equal(5, image.PayloadLength);
            Equal((uint)0x0100, image.Records[0].Address);
            Equal((byte)3, image.Records[0].Data[2]);
            Equal((uint)1, image.TerminatorType);
            True(!image.IsKnownUsb2XchangeImage);
        }

        private static void TestPrecisionTwoKnownLoaderManifest()
        {
            PrecisionTwoLoaderRecordManifest manifest =
                PrecisionTwoLoaderRecordManifest.CreateKnown();
            Equal(PrecisionTwoLoaderRecordManifest.KnownFirmwareSha256,
                manifest.FirmwareSha256);
            Equal(
                "851841B23FCA4D22E4C3C2DB966323BC9192AF3CECE430AAB5D7D8BFBF950729",
                manifest.GetRecordSha256(0));
            Equal(
                "5B30D224B9784DD96499A72F277D3954B6A863EDBEB433F103F2758EECC6E2E6",
                manifest.GetRecordSha256(15));
            Equal(
                "7C3D21A44BEBF711C21E354134850F317A3CC9F65922176F5E014B97C0E7094E",
                manifest.TerminalSha256);
        }

        private static void TestPrecisionTwoLoaderManifestFixtures()
        {
            byte[] firmware = new byte[
                PrecisionTwoLoaderRecordManifest.FirmwareLength];
            for (int index = 0; index < firmware.Length; ++index)
            {
                firmware[index] = (byte)((index * 37 + 11) & 0xFF);
            }
            string firmwareHash = Sha256Hex(firmware);
            PrecisionTwoLoaderRecordManifest manifest =
                PrecisionTwoLoaderRecordManifest.Create(firmware,
                    firmwareHash);
            byte[] recordCdb =
                ScsiFraming.BuildPrecisionTwoLoaderFirstRecordCdb();
            for (int index = 0;
                index < PrecisionTwoLoaderRecordManifest.RecordCount; ++index)
            {
                byte[] record = PrecisionTwoLoaderRecordManifest.BuildRecord(
                    firmware, index);
                manifest.ValidateRecord(index, recordCdb, record);
                Equal(Sha256Hex(record), manifest.GetRecordSha256(index));
            }

            byte[] wrong = PrecisionTwoLoaderRecordManifest.BuildRecord(
                firmware, 0);
            wrong[10] ^= 0x01;
            Throws<ProtocolException>(delegate
            {
                manifest.ValidateRecord(0, recordCdb, wrong);
            });
            byte[] wrongCdb = (byte[])recordCdb.Clone();
            wrongCdb[8] = 0x09;
            Throws<ProtocolException>(delegate
            {
                manifest.ValidateRecord(0, wrongCdb,
                    PrecisionTwoLoaderRecordManifest.BuildRecord(firmware, 0));
            });

            byte[] terminal = PrecisionTwoLoaderRecordManifest.BuildTerminal();
            manifest.ValidateTerminal(
                ScsiFraming.BuildPrecisionTwoLoaderTerminalRecordCdb(),
                terminal);
            terminal[4] ^= 0x01;
            Throws<ProtocolException>(delegate
            {
                manifest.ValidateTerminal(
                    ScsiFraming.BuildPrecisionTwoLoaderTerminalRecordCdb(),
                    terminal);
            });
        }

        private static void TestFirmwareBadLength()
        {
            Throws<ProtocolException>(delegate
            {
                FirmwareImage.Parse(new byte[29]);
            });
        }

        private static void TestFirmwareImmutability()
        {
            byte[] bytes = BuildFirmware(
                new SyntheticRecord(0x0100, new byte[] { 1, 2, 3 }));
            FirmwareImage image = FirmwareImage.Parse(bytes);
            bytes[12] = 0xEE;
            Equal((byte)1, image.Records[0].Data[0]);

            byte[] raw = image.RawBytes;
            raw[12] = 0xDD;
            Equal((byte)1, image.RawBytes[12]);

            byte[] data = image.Records[0].Data;
            data[0] = 0xCC;
            Equal((byte)1, image.Records[0].Data[0]);
        }

        private static void TestFirmwareOversizedRecord()
        {
            byte[] bytes = BuildFirmware(new SyntheticRecord(0, new byte[] { 1 }));
            WriteUInt32(bytes, 0, 17);
            Throws<ProtocolException>(delegate { FirmwareImage.Parse(bytes); });
        }

        private static void TestFirmwareTerminator()
        {
            byte[] bytes = new byte[28];
            WriteUInt32(bytes, 0, 1);
            bytes[12] = 0xAA;
            Throws<ProtocolException>(delegate { FirmwareImage.Parse(bytes); });

            byte[] trailing = BuildFirmware(new SyntheticRecord(0, new byte[] { 1 }));
            Array.Resize(ref trailing, trailing.Length + 28);
            Throws<ProtocolException>(delegate { FirmwareImage.Parse(trailing); });
        }

        private static void TestCommandWrapper()
        {
            byte[] cbw = ScsiFraming.BuildCommandWrapper(0x12345678, 36,
                DataDirection.In, 3, 2, ScsiFraming.BuildInquiryCdb(36));
            Equal(31, cbw.Length);
            Equal((byte)0x55, cbw[0]);
            Equal((byte)0x53, cbw[1]);
            Equal((byte)0x42, cbw[2]);
            Equal((byte)0x43, cbw[3]);
            Equal((byte)0x78, cbw[4]);
            Equal((byte)0x56, cbw[5]);
            Equal((byte)0x34, cbw[6]);
            Equal((byte)0x12, cbw[7]);
            Equal((byte)36, cbw[8]);
            Equal((byte)0x80, cbw[12]);
            Equal((byte)3, cbw[13]);
            Equal((byte)6, cbw[14]);
            Equal((byte)0x12, cbw[15]);
            Equal((byte)0x40, cbw[16]);
            Equal((byte)36, cbw[19]);
        }

        private static void TestCommandDirection()
        {
            Throws<ArgumentException>(delegate
            {
                ScsiFraming.BuildCommandWrapper(1, 0, DataDirection.In,
                    0, 0, ScsiFraming.BuildInquiryCdb(36));
            });
            Throws<ArgumentException>(delegate
            {
                ScsiFraming.BuildCommandWrapper(1, 36, DataDirection.None,
                    0, 0, ScsiFraming.BuildInquiryCdb(36));
            });
        }

        private static void TestCommandTargetRange()
        {
            Throws<ArgumentOutOfRangeException>(delegate
            {
                ScsiFraming.BuildCommandWrapper(1, 36, DataDirection.In,
                    7, 0, ScsiFraming.BuildInquiryCdb(36));
            });
        }

        private static void TestPrecisionTwoLoaderReadBufferD8()
        {
            byte[] cdb = ScsiFraming.BuildPrecisionTwoLoaderReadBufferD8Cdb();
            Equal(10, cdb.Length);
            Equal("3C 00 D8 00 00 00 00 00 42 00",
                ScsiFraming.ToHex(cdb));
            Equal((uint)66, ScsiFraming.PrecisionTwoLoaderBufferD8Length);

            byte[] cbw = ScsiFraming.BuildCommandWrapper(0x12345678,
                ScsiFraming.PrecisionTwoLoaderBufferD8Length,
                DataDirection.In, 5, 0, cdb);
            Equal((byte)66, cbw[8]);
            Equal((byte)0, cbw[9]);
            Equal((byte)0, cbw[10]);
            Equal((byte)0, cbw[11]);
            Equal((byte)0x80, cbw[12]);
            Equal((byte)5, cbw[13]);
            Equal((byte)10, cbw[14]);
            Equal("3C 00 D8 00 00 00 00 00 42 00",
                ScsiFraming.ToHex(Subarray(cbw, 15, 10)));
        }

        private static void TestPrecisionTwoScannerReady()
        {
            byte[] cdb = ScsiFraming.BuildPrecisionTwoScannerReadyCdb();
            Equal(6, cdb.Length);
            Equal("DF 00 00 00 02 00", ScsiFraming.ToHex(cdb));
            Equal((uint)2, ScsiFraming.PrecisionTwoScannerReadyLength);

            byte[] cbw = ScsiFraming.BuildCommandWrapper(0x12345678,
                ScsiFraming.PrecisionTwoScannerReadyLength,
                DataDirection.In, 5, 0, cdb);
            Equal((byte)2, cbw[8]);
            Equal((byte)0x80, cbw[12]);
            Equal((byte)5, cbw[13]);
            Equal((byte)6, cbw[14]);
            Equal("DF 00 00 00 02 00",
                ScsiFraming.ToHex(Subarray(cbw, 15, 6)));
        }

        private static void TestPrecisionTwoFaultPixelReadBuffer()
        {
            byte[] cdb =
                ScsiFraming.BuildPrecisionTwoFaultPixelReadBufferCdb();
            Equal(10, cdb.Length);
            Equal("3C 00 E0 00 04 00 00 00 16 00",
                ScsiFraming.ToHex(cdb));
            Equal((uint)22,
                ScsiFraming.PrecisionTwoFaultPixelBufferLength);

            byte[] cbw = ScsiFraming.BuildCommandWrapper(0x12345678,
                ScsiFraming.PrecisionTwoFaultPixelBufferLength,
                DataDirection.In, 5, 0, cdb);
            Equal((byte)22, cbw[8]);
            Equal((byte)0, cbw[9]);
            Equal((byte)0x80, cbw[12]);
            Equal((byte)5, cbw[13]);
            Equal((byte)10, cbw[14]);
            Equal("3C 00 E0 00 04 00 00 00 16 00",
                ScsiFraming.ToHex(Subarray(cbw, 15, 10)));

            byte[] dataCdb =
                ScsiFraming.BuildPrecisionTwoFaultPixelDataReadBufferCdb();
            Equal(10, dataCdb.Length);
            Equal("3C 00 E0 00 04 00 00 00 3A 00",
                ScsiFraming.ToHex(dataCdb));
            Equal((uint)58, ScsiFraming.PrecisionTwoFaultPixelDataLength);
            byte[] dataCbw = ScsiFraming.BuildCommandWrapper(0x12345678,
                ScsiFraming.PrecisionTwoFaultPixelDataLength,
                DataDirection.In, 5, 0, dataCdb);
            Equal((byte)58, dataCbw[8]);
            Equal((byte)0x80, dataCbw[12]);
            Equal("3C 00 E0 00 04 00 00 00 3A 00",
                ScsiFraming.ToHex(Subarray(dataCbw, 15, 10)));

            byte[] calibrationCdb =
                ScsiFraming.BuildPrecisionTwoCalibrationReadBufferCdb();
            Equal("3C 00 E0 00 00 00 00 04 00 00",
                ScsiFraming.ToHex(calibrationCdb));
            Equal((uint)1024,
                ScsiFraming.PrecisionTwoCalibrationBufferLength);
            byte[] calibrationCbw = ScsiFraming.BuildCommandWrapper(
                0x12345678,
                ScsiFraming.PrecisionTwoCalibrationBufferLength,
                DataDirection.In, 5, 0, calibrationCdb);
            Equal((byte)0, calibrationCbw[8]);
            Equal((byte)4, calibrationCbw[9]);
            Equal((byte)0x80, calibrationCbw[12]);
            Equal("3C 00 E0 00 00 00 00 04 00 00",
                ScsiFraming.ToHex(Subarray(calibrationCbw, 15, 10)));

            byte[] d8Offset55Cdb =
                ScsiFraming.BuildPrecisionTwoD8Offset55ReadBufferCdb();
            Equal("3C 00 D8 00 00 37 00 00 0A 00",
                ScsiFraming.ToHex(d8Offset55Cdb));
            Equal((uint)10, ScsiFraming.PrecisionTwoD8Offset55Length);
            byte[] d8Offset55Cbw = ScsiFraming.BuildCommandWrapper(
                0x12345678, ScsiFraming.PrecisionTwoD8Offset55Length,
                DataDirection.In, 5, 0, d8Offset55Cdb);
            Equal((byte)10, d8Offset55Cbw[8]);
            Equal((byte)0x80, d8Offset55Cbw[12]);
            Equal("3C 00 D8 00 00 37 00 00 0A 00",
                ScsiFraming.ToHex(Subarray(d8Offset55Cbw, 15, 10)));
        }

        private static void TestPrecisionTwoPreviewSetWindow()
        {
            byte[] cdb =
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb();
            Equal(10, cdb.Length);
            Equal("24 00 00 00 00 00 00 00 54 00",
                ScsiFraming.ToHex(cdb));
            Equal((uint)84,
                ScsiFraming.PrecisionTwoPreviewSetWindowLength);

            byte[] cbw = ScsiFraming.BuildCommandWrapper(0x12345678,
                ScsiFraming.PrecisionTwoPreviewSetWindowLength,
                DataDirection.Out, 5, 0, cdb);
            Equal((byte)84, cbw[8]);
            Equal((byte)0, cbw[9]);
            Equal((byte)0, cbw[12]);
            Equal((byte)5, cbw[13]);
            Equal((byte)10, cbw[14]);
            Equal("24 00 00 00 00 00 00 00 54 00",
                ScsiFraming.ToHex(Subarray(cbw, 15, 10)));
        }

        private static void TestPrecisionTwoStartupConfigurationCdbs()
        {
            Equal("3C 00 D8 00 00 00 00 04 00 00",
                ScsiFraming.ToHex(ScsiFraming.
                    BuildPrecisionTwoDynamicConfigurationD8ReadBufferCdb()));
            Equal((uint)1024,
                ScsiFraming.PrecisionTwoDynamicConfigurationLength);
            Equal("3B 01 00 00 00 00 00 04 3A 00",
                ScsiFraming.ToHex(ScsiFraming.
                    BuildPrecisionTwoStartupWriteBuffer1082Cdb()));
            Equal((uint)1082,
                ScsiFraming.PrecisionTwoStartupWriteBuffer1082Length);
            Equal("3C 00 E0 00 00 00 00 04 30 00",
                ScsiFraming.ToHex(ScsiFraming.
                    BuildPrecisionTwoStartupReadBufferE01072Cdb()));
            Equal((uint)1072,
                ScsiFraming.PrecisionTwoStartupReadBufferE01072Length);
        }

        private static void TestPrecisionTwoPreviewImageRead()
        {
            Equal((uint)3840,
                ScsiFraming.GetPrecisionTwoPreviewImageReadLength(640));
            byte[] cdb =
                ScsiFraming.BuildPrecisionTwoPreviewImageReadCdb(640);
            Equal(10, cdb.Length);
            Equal("28 00 00 00 28 00 00 0F 00 00",
                ScsiFraming.ToHex(cdb));

            byte[] cbw = ScsiFraming.BuildCommandWrapper(0x12345678,
                ScsiFraming.GetPrecisionTwoPreviewImageReadLength(640),
                DataDirection.In, 5, 0, cdb);
            Equal((byte)0, cbw[8]);
            Equal((byte)15, cbw[9]);
            Equal((byte)0x80, cbw[12]);
            Equal("28 00 00 00 28 00 00 0F 00 00",
                ScsiFraming.ToHex(Subarray(cbw, 15, 10)));

            Throws<ArgumentOutOfRangeException>(delegate
            {
                ScsiFraming.BuildPrecisionTwoPreviewImageReadCdb(0);
            });
            Throws<ArgumentOutOfRangeException>(delegate
            {
                ScsiFraming.BuildPrecisionTwoPreviewImageReadCdb(0x2AAAAB);
            });
        }

        private static void TestPrecisionTwoPreviewCommandManifest()
        {
            byte[] data = new byte[
                ScsiFraming.PrecisionTwoPreviewSetWindowLength];
            data[7] = 0x4C;
            string syntheticHash = Sha256Hex(data);
            Equal(
                "F45F2A91958286CF750AB56588E2C20A83E95E9B5973C046B6068C6C2FEC0EF6",
                PrecisionTwoPreviewCommandManifest.SetWindowSha256);
            Equal(
                "96EB9049828909D06A5E8AB32861124D1FE59B67005D3BF0EBDA235584D0C6FB",
                PrecisionTwoPreviewCommandManifest.AspiLiveSetWindowSha256);
            Equal(
                "78D93BC3FBA82976A49AA369301DFB065DD392CA381EB56CC504E79A643DAC63",
                PrecisionTwoPreviewCommandManifest.
                    AspiLive24x36SetWindowSha256);
            Equal(
                "A44969F96CACE039220A55E5FCC7F8EFB4EE054526039D806B9391C4D9097DAD",
                PrecisionTwoPreviewCommandManifest.
                    AspiLive4x5SetWindowSha256);
            Equal(
                "3092FA15361A422D558EF968164EE6CB00B854413630CC0C07A4F62877515A9D",
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveCleanupSetWindowSha256);
            Equal(
                "210F3499FB1BE7F689F44A26C68D8D079E6FB01E17AE19B320B5011E6F8A9EDA",
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanSetWindowSha256);
            Equal(
                "23B3C62B62096A50A58E6BC035D20C6C04E38F5D10A30BF27C6348D0C37EFB15",
                PrecisionTwoPreviewCommandManifest.
                    AspiLiveFullScanCleanupSetWindowSha256);
            Equal((uint)749, PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanWidth);
            Equal((uint)4494, PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanLength);
            Equal(762, PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanRows);
            Equal(32, PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanMaximumConsecutiveShortRetries);
            Equal(6096, PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanMaximumShortRetries);
            Equal(6858, PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanMaximumReadSubmissions);
            Equal(996, PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanNaturalRows);
            Equal(997, PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanRow997CompletionRows);
            Equal(7976, PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanRow997CompletionMaximumShortRetries);
            Equal(8973, PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanRow997CompletionMaximumReadSubmissions);
            Equal(998, PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanProgressMinimumCleanupRows);
            Equal(998, PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanProgressMaximumRows);
            Equal(7984, PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanProgressMaximumShortRetries);
            Equal(8982, PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanProgressMaximumReadSubmissions);
            Equal(2, PrecisionTwoPreviewCommandManifest.
                AspiLivePreviewCleanupSetWindowCount);
            Equal((uint)666, PrecisionTwoPreviewCommandManifest.
                AspiLiveFirstImageWidth);
            Equal((uint)3996, PrecisionTwoPreviewCommandManifest.
                AspiLiveFirstImageLength);
            Equal(7968, PrecisionTwoPreviewCommandManifest.
                AspiLivePreviewNaturalPerRowMaximumShortRetries);
            Equal(8964, PrecisionTwoPreviewCommandManifest.
                AspiLivePreviewNaturalPerRowMaximumReadSubmissions);
            Equal(911, PrecisionTwoPreviewCommandManifest.
                AspiLivePreviewPoweredNaturalRows);
            Equal(32, PrecisionTwoPreviewCommandManifest.
                AspiLivePreviewPoweredMaximumConsecutiveShortRetries);
            Equal(50, PrecisionTwoPreviewCommandManifest.
                AspiLiveImageShortRetryBackoffMilliseconds);
            Equal(7288, PrecisionTwoPreviewCommandManifest.
                AspiLivePreviewPoweredNaturalMaximumShortRetries);
            Equal(8199, PrecisionTwoPreviewCommandManifest.
                AspiLivePreviewPoweredNaturalMaximumReadSubmissions);
            Equal(PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalRows *
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewNaturalMaximumConsecutiveShortRetries,
                PrecisionTwoPreviewCommandManifest.
                    AspiLivePreviewNaturalPerRowMaximumShortRetries);
            True(PrecisionTwoPreviewCommandManifest.SetWindowSha256 !=
                PrecisionTwoPreviewCommandManifest.AspiLiveSetWindowSha256);
            PrecisionTwoPreviewCommandManifest.ValidateSetWindow(
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data,
                syntheticHash);
            PrecisionTwoPreviewCommandManifest.ValidateSetWindowEnvelope(
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data);
            Equal(syntheticHash, PrecisionTwoPreviewCommandManifest.
                FingerprintSetWindow(
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data));
            PrecisionTwoPreviewCommandManifest.ValidateCleanupSetWindow(
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data,
                syntheticHash);
            byte[] invalidCleanup = (byte[])data.Clone();
            invalidCleanup[10] = 1;
            Throws<ProtocolException>(delegate
            {
                PrecisionTwoPreviewCommandManifest.ValidateCleanupSetWindow(
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                    invalidCleanup, Sha256Hex(invalidCleanup));
            });
            Throws<ProtocolException>(delegate
            {
                PrecisionTwoPreviewCommandManifest.ValidateCleanupSetWindow(
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveCleanupSetWindowSha256);
            });
            byte[] described = (byte[])data.Clone();
            described[10] = 0x12;
            described[11] = 0x34;
            described[12] = 0x56;
            described[13] = 0x78;
            described[56] = 0x89;
            described[57] = 0xAB;
            described[58] = 0xCD;
            described[59] = 0xEF;
            described[70] = 0xAB;
            described[71] = 0xCD;
            described[82] = 0x12;
            described[83] = 0x34;
            string description = PrecisionTwoPreviewCommandManifest.
                DescribeSetWindow(
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                    described);
            True(description.IndexOf("regions-sha256=00-09:",
                StringComparison.Ordinal) == 0);
            True(description.IndexOf(",10-29:",
                StringComparison.Ordinal) > 0);
            True(description.IndexOf(",30-47:",
                StringComparison.Ordinal) > 0);
            True(description.IndexOf(",48-55:",
                StringComparison.Ordinal) > 0);
            True(description.IndexOf(",56-71:",
                StringComparison.Ordinal) > 0);
            True(description.IndexOf(",72-83:",
                StringComparison.Ordinal) > 0);
            True(description.IndexOf("; u32be=10:12345678,",
                StringComparison.Ordinal) > 0);
            True(description.IndexOf(",56:89ABCDEF,",
                StringComparison.Ordinal) > 0);
            True(description.IndexOf("; u16be=70:ABCD,82:1234",
                StringComparison.Ordinal) > 0);
            Throws<ProtocolException>(delegate
            {
                PrecisionTwoPreviewCommandManifest.ValidateSetWindow(
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data);
            });
            Throws<ProtocolException>(delegate
            {
                PrecisionTwoPreviewCommandManifest.ValidateSetWindow(
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), data,
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveSetWindowSha256);
            });

            byte[] changed = (byte[])data.Clone();
            changed[83] = 1;
            PrecisionTwoPreviewCommandManifest.ValidateSetWindowEnvelope(
                ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(), changed);
            Throws<ProtocolException>(delegate
            {
                PrecisionTwoPreviewCommandManifest.ValidateSetWindow(
                    ScsiFraming.BuildPrecisionTwoPreviewSetWindowCdb(),
                    changed, syntheticHash);
            });

            byte[] read = ScsiFraming.
                BuildPrecisionTwoPreviewImageReadCdb(640);
            Equal((uint)640, PrecisionTwoPreviewCommandManifest.
                ValidatePredictedImageRead(read, 3840));
            Equal((uint)640, PrecisionTwoPreviewCommandManifest.
                ValidateOfflineImageRead(read, 3840));
            byte[] liveRead = ScsiFraming.
                BuildPrecisionTwoPreviewImageReadCdb(666);
            Equal((uint)666, PrecisionTwoPreviewCommandManifest.
                ValidateAspiLiveFirstImageRead(liveRead, 3996));
            Throws<ProtocolException>(delegate
            {
                PrecisionTwoPreviewCommandManifest.
                    ValidateAspiLiveFirstImageRead(
                        ScsiFraming.BuildPrecisionTwoPreviewImageReadCdb(640),
                        3840);
            });
            read[4] = 0x51;
            Equal((uint)640, PrecisionTwoPreviewCommandManifest.
                ValidateOfflineImageRead(read, 3840));
            Throws<ProtocolException>(delegate
            {
                PrecisionTwoPreviewCommandManifest.
                    ValidatePredictedImageRead(read, 3840);
            });
            read[4] = 0x50;
            Throws<ProtocolException>(delegate
            {
                PrecisionTwoPreviewCommandManifest.
                    ValidatePredictedImageRead(read, 3840);
            });
        }

        private static void TestPrecisionTwoLoaderWriteBufferModeOneZero()
        {
            byte[] cdb = ScsiFraming.
                BuildPrecisionTwoLoaderWriteBufferModeOneZeroCdb();
            Equal(10, cdb.Length);
            Equal("3B 01 00 00 00 00 00 00 00 00",
                ScsiFraming.ToHex(cdb));

            byte[] cbw = ScsiFraming.BuildCommandWrapper(0x12345678, 0,
                DataDirection.None, 5, 0, cdb);
            Equal((byte)0, cbw[8]);
            Equal((byte)0, cbw[9]);
            Equal((byte)0, cbw[10]);
            Equal((byte)0, cbw[11]);
            Equal((byte)0, cbw[12]);
            Equal((byte)5, cbw[13]);
            Equal((byte)10, cbw[14]);
            Equal("3B 01 00 00 00 00 00 00 00 00",
                ScsiFraming.ToHex(Subarray(cbw, 15, 10)));
        }

        private static void TestPrecisionTwoLoaderFirstRecord()
        {
            byte[] cdb = ScsiFraming.
                BuildPrecisionTwoLoaderFirstRecordCdb();
            Equal(10, cdb.Length);
            Equal("3B 01 00 00 00 00 00 10 0A 00",
                ScsiFraming.ToHex(cdb));
            Equal((uint)4106,
                ScsiFraming.PrecisionTwoLoaderFirstRecordLength);

            byte[] cbw = ScsiFraming.BuildCommandWrapper(0x12345678,
                ScsiFraming.PrecisionTwoLoaderFirstRecordLength,
                DataDirection.Out, 5, 0, cdb);
            Equal((byte)0x0A, cbw[8]);
            Equal((byte)0x10, cbw[9]);
            Equal((byte)0, cbw[10]);
            Equal((byte)0, cbw[11]);
            Equal((byte)0, cbw[12]);
            Equal((byte)5, cbw[13]);
            Equal((byte)10, cbw[14]);
            Equal("3B 01 00 00 00 00 00 10 0A 00",
                ScsiFraming.ToHex(Subarray(cbw, 15, 10)));
        }

        private static void TestPrecisionTwoLoaderTerminalRecord()
        {
            byte[] cdb = ScsiFraming.
                BuildPrecisionTwoLoaderTerminalRecordCdb();
            Equal(10, cdb.Length);
            Equal("3B 01 00 00 00 00 00 00 0A 00",
                ScsiFraming.ToHex(cdb));

            byte[] cbw = ScsiFraming.BuildCommandWrapper(0x12345678, 10,
                DataDirection.Out, 5, 0, cdb);
            Equal((byte)10, cbw[8]);
            Equal((byte)0, cbw[9]);
            Equal((byte)0, cbw[10]);
            Equal((byte)0, cbw[11]);
            Equal((byte)0, cbw[12]);
            Equal((byte)5, cbw[13]);
            Equal((byte)10, cbw[14]);
            Equal("3B 01 00 00 00 00 00 00 0A 00",
                ScsiFraming.ToHex(Subarray(cbw, 15, 10)));
        }

        private static void TestStatusWrapper()
        {
            byte[] csw = BuildStatus(0x12345678, 4, 0x02);
            CommandStatusWrapper parsed = ScsiFraming.ParseStatusWrapper(
                csw, 0x12345678, 36);
            Equal((uint)4, parsed.Residue);
            Equal((uint)32, parsed.ActualLength);
            Equal((byte)0x02, parsed.RawStatus);
            Equal(AdapterStatus.CheckCondition, parsed.Status);
            True(parsed.IsKnownStatus);
        }

        private static void TestStatusSignature()
        {
            byte[] csw = BuildStatus(1, 0, 0);
            csw[0] = 0;
            Throws<ProtocolException>(delegate
            {
                ScsiFraming.ParseStatusWrapper(csw, 1, 0);
            });
        }

        private static void TestStatusTag()
        {
            Throws<ProtocolException>(delegate
            {
                ScsiFraming.ParseStatusWrapper(BuildStatus(2, 0, 0), 1, 0);
            });
        }

        private static void TestStatusResidue()
        {
            Throws<ProtocolException>(delegate
            {
                ScsiFraming.ParseStatusWrapper(BuildStatus(1, 37, 0), 1, 36);
            });
        }

        private static void TestInquiry()
        {
            byte[] bytes = new byte[36];
            bytes[0] = 0x05;
            bytes[1] = 0x80;
            WriteAscii(bytes, 8, 8, "ADAPTEC");
            WriteAscii(bytes, 16, 16, "TEST DEVICE");
            WriteAscii(bytes, 32, 4, "1.0");
            InquiryData inquiry = InquiryData.Parse(bytes);
            Equal((byte)5, inquiry.PeripheralDeviceType);
            True(inquiry.Removable);
            Equal("ADAPTEC", inquiry.Vendor);
            Equal("TEST DEVICE", inquiry.Product);
            Equal("1.0", inquiry.Revision);
        }

        private static byte[] BuildFirmware(params SyntheticRecord[] records)
        {
            byte[] bytes = new byte[(records.Length + 1) * 28];
            for (int i = 0; i < records.Length; i++)
            {
                int offset = i * 28;
                WriteUInt32(bytes, offset, (uint)records[i].Data.Length);
                WriteUInt32(bytes, offset + 4, records[i].Address);
                Buffer.BlockCopy(records[i].Data, 0, bytes, offset + 12,
                    records[i].Data.Length);
            }
            int terminator = records.Length * 28;
            WriteUInt32(bytes, terminator, 16);
            WriteUInt32(bytes, terminator + 8, 1);
            return bytes;
        }

        private static byte[] BuildStatus(uint tag, uint residue, byte status)
        {
            byte[] bytes = new byte[13];
            WriteUInt32(bytes, 0, ScsiFraming.StatusSignature);
            WriteUInt32(bytes, 4, tag);
            WriteUInt32(bytes, 8, residue);
            bytes[12] = status;
            return bytes;
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)(value & 0xFF);
            bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
            bytes[offset + 2] = (byte)((value >> 16) & 0xFF);
            bytes[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteAscii(byte[] bytes, int offset, int length,
            string value)
        {
            for (int i = 0; i < length; i++)
            {
                bytes[offset + i] = (byte)' ';
            }
            byte[] text = Encoding.ASCII.GetBytes(value);
            Buffer.BlockCopy(text, 0, bytes, offset, Math.Min(length, text.Length));
        }

        private static byte[] Subarray(byte[] bytes, int offset, int length)
        {
            byte[] result = new byte[length];
            Buffer.BlockCopy(bytes, offset, result, 0, length);
            return result;
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).
                    Replace("-", string.Empty);
            }
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

        private static void True(bool value)
        {
            if (!value)
            {
                throw new Exception("Expected true.");
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

        private sealed class SyntheticRecord
        {
            public SyntheticRecord(uint address, byte[] data)
            {
                Address = address;
                Data = data;
            }

            public uint Address;
            public byte[] Data;
        }
    }
}
