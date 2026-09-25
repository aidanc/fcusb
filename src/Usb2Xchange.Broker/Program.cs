// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using Usb2Xchange.BrokerProtocol;

namespace Usb2Xchange.Broker
{
    internal static class Program
    {
        private const string PrecisionTwoFirmwareSha256 =
            "D8D7188574C52B255CF7940BEF7CC9192F744693AABC1F735758B7FECDEC65F3";
        private const string PrecisionTwoFirstRecordSha256 =
            "851841B23FCA4D22E4C3C2DB966323BC9192AF3CECE430AAB5D7D8BFBF950729";
        private const string PrecisionTwoSecondRecordSha256 =
            "76B6D086666727C0E6DBD9AAE67134FCBBBE24DA0932705E68A2F1D5A4271A46";
        private const string PrecisionTwoThirdRecordSha256 =
            "18CF662935349330B01E73F0B23672C0A969A38EA391F3D7556280734EE781B9";
        private const string PrecisionTwoFourthRecordSha256 =
            "6AD16D088197C7C84C07C7F69DE977794EF35FC936F83350AEA08D17F52B45BD";
        private const string PrecisionTwoFifthRecordSha256 =
            "C191E5F7E02825163C1DF8FD57F20AC76B575B8C272B216C47EE7BD46FC23B28";
        private const string PrecisionTwoSixthRecordSha256 =
            "F071559D4DB49591F8B366A2A308EDE12875EF311E1CB9C2C6C4D5D5E0FDA42C";
        private const string PrecisionTwoSeventhRecordSha256 =
            "3CDCFC4F5A6150E2A4428C0F2EB587AA199896CA144D12B225A8C9A695D3C7FC";
        private const string PrecisionTwoEighthRecordSha256 =
            "06233C39AA16FF112B879AD52FB7D93A0CA8AE97EA870D802952C2E57719355E";
        private const string PrecisionTwoNinthRecordSha256 =
            "52DAA6702DABE141F6F0E81A7F6AC31954148FA026CD69E4BD78FEF259BD5537";
        private const string PrecisionTwoTenthRecordSha256 =
            "2EAD7C79A4B409019BC0371B4AAA0E1638A3EBD40E1BAE45B403EA4359A0E29B";
        private const string PrecisionTwoEleventhRecordSha256 =
            "91A66555F012FD389737061A477F3731579A3B037507F1E402779C6DFA18E23F";
        private const string PrecisionTwoTwelfthRecordSha256 =
            "EA7EB8845C6B86E49DDD445E08FC19CCAF6DE77DFF792BDC3AB7F99B895594DC";
        private const string PrecisionTwoThirteenthRecordSha256 =
            "C29A2A91BD553304F90DCB5559FC158855E853218100882C53D4A1C3AF4232E4";
        private const string PrecisionTwoFourteenthRecordSha256 =
            "DEB4D4A46C9CEA07C45D4B253B8A2F20B465CC1A71903F0721542336F7AC1FE6";
        private const string PrecisionTwoFifteenthRecordSha256 =
            "FEFA54D1C15F7313703ADC1433ABBB5A356794662CAC1209868F9F161408558A";
        private const string PrecisionTwoSixteenthRecordSha256 =
            "5B30D224B9784DD96499A72F277D3954B6A863EDBEB433F103F2758EECC6E2E6";
        private const string PrecisionTwoTerminalRecordSha256 =
            "7C3D21A44BEBF711C21E354134850F317A3CC9F65922176F5E014B97C0E7094E";

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0 || args[0] == "help" ||
                    args[0] == "--help")
                {
                    PrintUsage();
                    return 0;
                }
                if (args[0] == "dry-run")
                {
                    return DryRun();
                }
                if (args[0] == "status")
                {
                    return ShowStatus();
                }
                if (args[0] == "validate-first-loader-record")
                {
                    if (args.Length != 2)
                    {
                        throw new ArgumentException(
                            "validate-first-loader-record requires exactly " +
                            "one MICROCOD.3XX path.");
                    }
                    LoadPrecisionTwoManifest(args[1], 1);
                    Console.WriteLine(
                        "Offline first-record validation passed. No device " +
                        "was opened and no driver or scanner state changed.");
                    return 0;
                }
                if (args[0] == "validate-two-loader-records")
                {
                    if (args.Length != 2)
                    {
                        throw new ArgumentException(
                            "validate-two-loader-records requires exactly " +
                            "one MICROCOD.3XX path.");
                    }
                    LoadPrecisionTwoManifest(args[1], 2);
                    Console.WriteLine(
                        "Offline two-record validation passed. No device was " +
                        "opened and no driver or scanner state changed.");
                    return 0;
                }
                if (args[0] == "validate-three-loader-records")
                {
                    if (args.Length != 2)
                    {
                        throw new ArgumentException(
                            "validate-three-loader-records requires exactly " +
                            "one MICROCOD.3XX path.");
                    }
                    LoadPrecisionTwoManifest(args[1], 3);
                    Console.WriteLine(
                        "Offline three-record validation passed. No device was " +
                        "opened and no driver or scanner state changed.");
                    return 0;
                }
                if (args[0] == "validate-four-loader-records")
                {
                    if (args.Length != 2)
                    {
                        throw new ArgumentException(
                            "validate-four-loader-records requires exactly " +
                            "one MICROCOD.3XX path.");
                    }
                    LoadPrecisionTwoManifest(args[1], 4);
                    Console.WriteLine(
                        "Offline four-record validation passed. No device was " +
                        "opened and no driver or scanner state changed.");
                    return 0;
                }
                if (args[0] == "validate-five-loader-records")
                {
                    if (args.Length != 2)
                    {
                        throw new ArgumentException(
                            "validate-five-loader-records requires exactly " +
                            "one MICROCOD.3XX path.");
                    }
                    LoadPrecisionTwoManifest(args[1], 5);
                    Console.WriteLine(
                        "Offline five-record validation passed. No device was " +
                        "opened and no driver or scanner state changed.");
                    return 0;
                }
                if (args[0] == "validate-six-loader-records")
                {
                    if (args.Length != 2)
                    {
                        throw new ArgumentException(
                            "validate-six-loader-records requires exactly " +
                            "one MICROCOD.3XX path.");
                    }
                    LoadPrecisionTwoManifest(args[1], 6);
                    Console.WriteLine(
                        "Offline six-record validation passed. No device was " +
                        "opened and no driver or scanner state changed.");
                    return 0;
                }
                if (args[0] == "validate-eight-loader-records")
                {
                    if (args.Length != 2)
                    {
                        throw new ArgumentException(
                            "validate-eight-loader-records requires exactly " +
                            "one MICROCOD.3XX path.");
                    }
                    LoadPrecisionTwoManifest(args[1], 8);
                    Console.WriteLine(
                        "Offline eight-record validation passed. No device " +
                        "was opened and no driver or scanner state changed.");
                    return 0;
                }
                if (args[0] == "validate-ten-loader-records")
                {
                    if (args.Length != 2)
                    {
                        throw new ArgumentException(
                            "validate-ten-loader-records requires exactly " +
                            "one MICROCOD.3XX path.");
                    }
                    LoadPrecisionTwoManifest(args[1], 10);
                    Console.WriteLine(
                        "Offline ten-record validation passed. No device " +
                        "was opened and no driver or scanner state changed.");
                    return 0;
                }
                if (args[0] == "validate-twelve-loader-records")
                {
                    if (args.Length != 2)
                    {
                        throw new ArgumentException(
                            "validate-twelve-loader-records requires exactly " +
                            "one MICROCOD.3XX path.");
                    }
                    LoadPrecisionTwoManifest(args[1], 12);
                    Console.WriteLine(
                        "Offline twelve-record validation passed. No device " +
                        "was opened and no driver or scanner state changed.");
                    return 0;
                }
                if (args[0] == "validate-fourteen-loader-records")
                {
                    if (args.Length != 2)
                    {
                        throw new ArgumentException(
                            "validate-fourteen-loader-records requires " +
                            "exactly one MICROCOD.3XX path.");
                    }
                    LoadPrecisionTwoManifest(args[1], 14);
                    Console.WriteLine(
                        "Offline fourteen-record validation passed. No " +
                        "device was opened and no driver or scanner state " +
                        "changed.");
                    return 0;
                }
                if (args[0] == "validate-sixteen-loader-records")
                {
                    if (args.Length != 2)
                    {
                        throw new ArgumentException(
                            "validate-sixteen-loader-records requires " +
                            "exactly one MICROCOD.3XX path.");
                    }
                    LoadPrecisionTwoManifest(args[1], 16);
                    Console.WriteLine(
                        "Offline sixteen-record validation passed. No " +
                        "device was opened and no driver or scanner state " +
                        "changed.");
                    return 0;
                }
                if (args[0] == "validate-complete-loader-sequence")
                {
                    if (args.Length != 2)
                    {
                        throw new ArgumentException(
                            "validate-complete-loader-sequence requires " +
                            "exactly one MICROCOD.3XX path.");
                    }
                    PrecisionTwoLoaderSequenceManifest manifest =
                        LoadPrecisionTwoManifest(args[1], 16);
                    RequirePrecisionTwoTerminalRecord(manifest);
                    Console.WriteLine(
                        "Offline complete-sequence validation passed: sixteen " +
                        "records plus the exact terminal are pinned. No device " +
                        "was opened and no driver or scanner state changed.");
                    return 0;
                }
                if (args[0] == "offline")
                {
                    ulong generation = args.Length == 2 ?
                        ulong.Parse(args[1]) :
                        checked((ulong)DateTime.UtcNow.Ticks);
                    return SendOffline(generation);
                }
                if (args[0] == "serve-readonly")
                {
                    int durationSeconds = args.Length >= 2 ?
                        int.Parse(args[1]) : 90;
                    int maximumRequests = args.Length >= 3 ?
                        int.Parse(args[2]) : 64;
                    if (args.Length > 3)
                    {
                        throw new ArgumentException(
                            "serve-readonly accepts at most two arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, false, ReadBufferD8Experiment.Block,
                        LoaderWriteBufferExperiment.Block);
                }
                if (args[0] == "capture-first-blocked")
                {
                    int durationSeconds = args.Length >= 2 ?
                        int.Parse(args[1]) : 90;
                    int maximumRequests = args.Length >= 3 ?
                        int.Parse(args[2]) : 64;
                    if (args.Length > 3)
                    {
                        throw new ArgumentException(
                            "capture-first-blocked accepts at most two arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true, ReadBufferD8Experiment.Block,
                        LoaderWriteBufferExperiment.Block);
                }
                if (args[0] == "capture-after-read-buffer-d8")
                {
                    int durationSeconds = args.Length >= 2 ?
                        int.Parse(args[1]) : 90;
                    int maximumRequests = args.Length >= 3 ?
                        int.Parse(args[2]) : 64;
                    if (args.Length > 3)
                    {
                        throw new ArgumentException(
                            "capture-after-read-buffer-d8 accepts at most two arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.LoaderOnce,
                        LoaderWriteBufferExperiment.Block);
                }
                if (args[0] ==
                    "capture-after-operational-read-buffer-d8")
                {
                    int durationSeconds = args.Length >= 2 ?
                        int.Parse(args[1]) : 90;
                    int maximumRequests = args.Length >= 3 ?
                        int.Parse(args[2]) : 64;
                    if (args.Length > 3)
                    {
                        throw new ArgumentException(
                            "capture-after-operational-read-buffer-d8 " +
                            "accepts at most two arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.OperationalOnce,
                        LoaderWriteBufferExperiment.Block);
                }
                if (args[0] ==
                    "capture-after-operational-scanner-ready")
                {
                    int durationSeconds = args.Length >= 2 ?
                        int.Parse(args[1]) : 90;
                    int maximumRequests = args.Length >= 3 ?
                        int.Parse(args[2]) : 64;
                    if (args.Length > 3)
                    {
                        throw new ArgumentException(
                            "capture-after-operational-scanner-ready " +
                            "accepts at most two arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.
                            OperationalD8AndScannerReadyOnce,
                        LoaderWriteBufferExperiment.Block);
                }
                if (args[0] ==
                    "capture-after-operational-fault-pixels")
                {
                    int durationSeconds = args.Length >= 2 ?
                        int.Parse(args[1]) : 90;
                    int maximumRequests = args.Length >= 3 ?
                        int.Parse(args[2]) : 64;
                    if (args.Length > 3)
                    {
                        throw new ArgumentException(
                            "capture-after-operational-fault-pixels " +
                            "accepts at most two arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.
                            OperationalD8ScannerReadyAndFaultPixelsOnce,
                        LoaderWriteBufferExperiment.Block);
                }
                if (args[0] ==
                    "capture-operational-initialization-readonly")
                {
                    int durationSeconds = args.Length >= 2 ?
                        int.Parse(args[1]) : 90;
                    int maximumRequests = args.Length >= 3 ?
                        int.Parse(args[2]) : 128;
                    if (args.Length > 3)
                    {
                        throw new ArgumentException(
                            "capture-operational-initialization-readonly " +
                            "accepts at most two arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.
                            OperationalInitializationSixCycles,
                        LoaderWriteBufferExperiment.Block);
                }
                if (args[0] == "capture-after-preview-set-window")
                {
                    const string acknowledgment =
                        "I-APPROVE-EXACT-PREVIEW-SET-WINDOW-24-F45F2A91";
                    if (args.Length < 2 || args[1] != acknowledgment)
                    {
                        throw new ArgumentException(
                            "The Preview SET WINDOW experiment requires the " +
                            "exact state-change acknowledgment token.");
                    }
                    int durationSeconds = args.Length >= 3 ?
                        int.Parse(args[2]) : 120;
                    int maximumRequests = args.Length >= 4 ?
                        int.Parse(args[3]) : 128;
                    if (args.Length > 4)
                    {
                        throw new ArgumentException(
                            "capture-after-preview-set-window accepts the " +
                            "approval token and at most two numeric arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.
                            OperationalInitializationSixCycles,
                        LoaderWriteBufferExperiment.Block, null,
                        OperationalSetWindowExperiment.
                            ExecuteCapturedPreviewOnceAndOffline);
                }
                if (args[0] ==
                    "capture-after-preview-set-window-next-blocked")
                {
                    const string acknowledgment =
                        "I-APPROVE-EXACT-PREVIEW-SET-WINDOW-24-THEN-BLOCK-NEXT";
                    if (args.Length < 2 || args[1] != acknowledgment)
                    {
                        throw new ArgumentException(
                            "The Preview continuation capture requires the " +
                            "exact state-change acknowledgment token.");
                    }
                    int durationSeconds = args.Length >= 3 ?
                        int.Parse(args[2]) : 120;
                    int maximumRequests = args.Length >= 4 ?
                        int.Parse(args[3]) : 128;
                    if (args.Length > 4)
                    {
                        throw new ArgumentException(
                            "capture-after-preview-set-window-next-blocked " +
                            "accepts the approval token and at most two " +
                            "numeric arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.
                            OperationalInitializationSixCycles,
                        LoaderWriteBufferExperiment.Block, null,
                        OperationalSetWindowExperiment.
                            ExecuteCapturedPreviewOnceThenCaptureNextBlocked);
                }
                if (args[0] ==
                    "capture-after-write-buffer-mode1-zero")
                {
                    const string acknowledgment =
                        "I-APPROVE-EXACT-ZERO-LENGTH-WRITE-BUFFER-3B01";
                    if (args.Length < 2 || args[1] != acknowledgment)
                    {
                        throw new ArgumentException(
                            "The WRITE BUFFER experiment requires the exact " +
                            "state-change acknowledgment token.");
                    }
                    int durationSeconds = args.Length >= 3 ?
                        int.Parse(args[2]) : 90;
                    int maximumRequests = args.Length >= 4 ?
                        int.Parse(args[3]) : 64;
                    if (args.Length > 4)
                    {
                        throw new ArgumentException(
                            "capture-after-write-buffer-mode1-zero accepts the " +
                            "acknowledgment and at most two arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.LoaderOnce,
                        LoaderWriteBufferExperiment.ExecuteOnceAndOffline);
                }
                if (args[0] ==
                    "capture-after-simulated-write-buffer-error")
                {
                    int durationSeconds = args.Length >= 2 ?
                        int.Parse(args[1]) : 90;
                    int maximumRequests = args.Length >= 3 ?
                        int.Parse(args[2]) : 64;
                    if (args.Length > 3)
                    {
                        throw new ArgumentException(
                            "capture-after-simulated-write-buffer-error " +
                            "accepts at most two arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.LoaderOnce,
                        LoaderWriteBufferExperiment.
                            SimulateProtocolErrorOnce);
                }
                if (args[0] ==
                    "capture-after-first-loader-record")
                {
                    const string acknowledgment =
                        "I-APPROVE-ONE-PRECISION2-FIRST-LOADER-RECORD-851841B2";
                    if (args.Length < 3 || args[1] != acknowledgment)
                    {
                        throw new ArgumentException(
                            "The first loader-record experiment requires its " +
                            "exact approval token and firmware path.");
                    }
                    PrecisionTwoLoaderSequenceManifest manifest =
                        LoadPrecisionTwoManifest(args[2], 1);
                    int durationSeconds = args.Length >= 4 ?
                        int.Parse(args[3]) : 120;
                    int maximumRequests = args.Length >= 5 ?
                        int.Parse(args[4]) : 64;
                    if (args.Length > 5)
                    {
                        throw new ArgumentException(
                            "capture-after-first-loader-record accepts the " +
                            "approval token, firmware path, and at most two " +
                            "guard arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.LoaderOnce,
                        LoaderWriteBufferExperiment.
                            ExecuteFirstRecordOnceAndOffline, manifest);
                }
                if (args[0] ==
                    "capture-after-two-loader-records")
                {
                    const string acknowledgment =
                        "I-APPROVE-TWO-PRECISION2-LOADER-RECORDS-" +
                        "851841B2-76B6D086";
                    if (args.Length < 3 || args[1] != acknowledgment)
                    {
                        throw new ArgumentException(
                            "The two-loader-record experiment requires its " +
                            "exact approval token and firmware path.");
                    }
                    PrecisionTwoLoaderSequenceManifest manifest =
                        LoadPrecisionTwoManifest(args[2], 2);
                    int durationSeconds = args.Length >= 4 ?
                        int.Parse(args[3]) : 120;
                    int maximumRequests = args.Length >= 5 ?
                        int.Parse(args[4]) : 64;
                    if (args.Length > 5)
                    {
                        throw new ArgumentException(
                            "capture-after-two-loader-records accepts the " +
                            "approval token, firmware path, and at most two " +
                            "guard arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.LoaderOnce,
                        LoaderWriteBufferExperiment.
                            ExecuteFirstTwoRecordsOnceAndOffline, manifest);
                }
                if (args[0] ==
                    "capture-after-three-loader-records")
                {
                    const string acknowledgment =
                        "I-APPROVE-THREE-PRECISION2-LOADER-RECORDS-" +
                        "851841B2-76B6D086-18CF6629";
                    if (args.Length < 3 || args[1] != acknowledgment)
                    {
                        throw new ArgumentException(
                            "The three-loader-record experiment requires its " +
                            "exact approval token and firmware path.");
                    }
                    PrecisionTwoLoaderSequenceManifest manifest =
                        LoadPrecisionTwoManifest(args[2], 3);
                    int durationSeconds = args.Length >= 4 ?
                        int.Parse(args[3]) : 120;
                    int maximumRequests = args.Length >= 5 ?
                        int.Parse(args[4]) : 64;
                    if (args.Length > 5)
                    {
                        throw new ArgumentException(
                            "capture-after-three-loader-records accepts the " +
                            "approval token, firmware path, and at most two " +
                            "guard arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.LoaderOnce,
                        LoaderWriteBufferExperiment.
                            ExecuteFirstThreeRecordsOnceAndOffline, manifest);
                }
                if (args[0] ==
                    "capture-after-four-loader-records")
                {
                    const string acknowledgment =
                        "I-APPROVE-FOUR-PRECISION2-LOADER-RECORDS-" +
                        "851841B2-76B6D086-18CF6629-6AD16D08";
                    if (args.Length < 3 || args[1] != acknowledgment)
                    {
                        throw new ArgumentException(
                            "The four-loader-record experiment requires its " +
                            "exact approval token and firmware path.");
                    }
                    PrecisionTwoLoaderSequenceManifest manifest =
                        LoadPrecisionTwoManifest(args[2], 4);
                    int durationSeconds = args.Length >= 4 ?
                        int.Parse(args[3]) : 120;
                    int maximumRequests = args.Length >= 5 ?
                        int.Parse(args[4]) : 64;
                    if (args.Length > 5)
                    {
                        throw new ArgumentException(
                            "capture-after-four-loader-records accepts the " +
                            "approval token, firmware path, and at most two " +
                            "guard arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.LoaderOnce,
                        LoaderWriteBufferExperiment.
                            ExecuteFirstFourRecordsOnceAndOffline, manifest);
                }
                if (args[0] ==
                    "capture-after-five-loader-records")
                {
                    const string acknowledgment =
                        "I-APPROVE-FIVE-PRECISION2-LOADER-RECORDS-" +
                        "851841B2-76B6D086-18CF6629-6AD16D08-C191E5F7";
                    if (args.Length < 3 || args[1] != acknowledgment)
                    {
                        throw new ArgumentException(
                            "The five-loader-record experiment requires its " +
                            "exact approval token and firmware path.");
                    }
                    PrecisionTwoLoaderSequenceManifest manifest =
                        LoadPrecisionTwoManifest(args[2], 5);
                    int durationSeconds = args.Length >= 4 ?
                        int.Parse(args[3]) : 120;
                    int maximumRequests = args.Length >= 5 ?
                        int.Parse(args[4]) : 64;
                    if (args.Length > 5)
                    {
                        throw new ArgumentException(
                            "capture-after-five-loader-records accepts the " +
                            "approval token, firmware path, and at most two " +
                            "guard arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.LoaderOnce,
                        LoaderWriteBufferExperiment.
                            ExecuteFirstFiveRecordsOnceAndOffline, manifest);
                }
                if (args[0] ==
                    "capture-after-six-loader-records")
                {
                    const string acknowledgment =
                        "I-APPROVE-SIX-PRECISION2-LOADER-RECORDS-" +
                        "851841B2-76B6D086-18CF6629-6AD16D08-C191E5F7-" +
                        "F071559D";
                    if (args.Length < 3 || args[1] != acknowledgment)
                    {
                        throw new ArgumentException(
                            "The six-loader-record experiment requires its " +
                            "exact approval token and firmware path.");
                    }
                    PrecisionTwoLoaderSequenceManifest manifest =
                        LoadPrecisionTwoManifest(args[2], 6);
                    int durationSeconds = args.Length >= 4 ?
                        int.Parse(args[3]) : 120;
                    int maximumRequests = args.Length >= 5 ?
                        int.Parse(args[4]) : 64;
                    if (args.Length > 5)
                    {
                        throw new ArgumentException(
                            "capture-after-six-loader-records accepts the " +
                            "approval token, firmware path, and at most two " +
                            "guard arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.LoaderOnce,
                        LoaderWriteBufferExperiment.
                            ExecuteFirstSixRecordsOnceAndOffline, manifest);
                }
                if (args[0] ==
                    "capture-after-eight-loader-records")
                {
                    const string acknowledgment =
                        "I-APPROVE-EIGHT-PRECISION2-LOADER-RECORDS-" +
                        "851841B2-76B6D086-18CF6629-6AD16D08-C191E5F7-" +
                        "F071559D-3CDCFC4F-06233C39";
                    if (args.Length < 3 || args[1] != acknowledgment)
                    {
                        throw new ArgumentException(
                            "The eight-loader-record experiment requires its " +
                            "exact approval token and firmware path.");
                    }
                    PrecisionTwoLoaderSequenceManifest manifest =
                        LoadPrecisionTwoManifest(args[2], 8);
                    int durationSeconds = args.Length >= 4 ?
                        int.Parse(args[3]) : 120;
                    int maximumRequests = args.Length >= 5 ?
                        int.Parse(args[4]) : 64;
                    if (args.Length > 5)
                    {
                        throw new ArgumentException(
                            "capture-after-eight-loader-records accepts the " +
                            "approval token, firmware path, and at most two " +
                            "guard arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.LoaderOnce,
                        LoaderWriteBufferExperiment.
                            ExecuteFirstEightRecordsOnceAndOffline, manifest);
                }
                if (args[0] ==
                    "capture-after-ten-loader-records")
                {
                    const string acknowledgment =
                        "I-APPROVE-TEN-PRECISION2-LOADER-RECORDS-" +
                        "851841B2-76B6D086-18CF6629-6AD16D08-C191E5F7-" +
                        "F071559D-3CDCFC4F-06233C39-52DAA670-2EAD7C79";
                    if (args.Length < 3 || args[1] != acknowledgment)
                    {
                        throw new ArgumentException(
                            "The ten-loader-record experiment requires its " +
                            "exact approval token and firmware path.");
                    }
                    PrecisionTwoLoaderSequenceManifest manifest =
                        LoadPrecisionTwoManifest(args[2], 10);
                    int durationSeconds = args.Length >= 4 ?
                        int.Parse(args[3]) : 120;
                    int maximumRequests = args.Length >= 5 ?
                        int.Parse(args[4]) : 64;
                    if (args.Length > 5)
                    {
                        throw new ArgumentException(
                            "capture-after-ten-loader-records accepts the " +
                            "approval token, firmware path, and at most two " +
                            "guard arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.LoaderOnce,
                        LoaderWriteBufferExperiment.
                            ExecuteFirstTenRecordsOnceAndOffline, manifest);
                }
                if (args[0] ==
                    "capture-after-twelve-loader-records")
                {
                    const string acknowledgment =
                        "I-APPROVE-TWELVE-PRECISION2-LOADER-RECORDS-" +
                        "851841B2-76B6D086-18CF6629-6AD16D08-C191E5F7-" +
                        "F071559D-3CDCFC4F-06233C39-52DAA670-2EAD7C79-" +
                        "91A66555-EA7EB884";
                    if (args.Length < 3 || args[1] != acknowledgment)
                    {
                        throw new ArgumentException(
                            "The twelve-loader-record experiment requires its " +
                            "exact approval token and firmware path.");
                    }
                    PrecisionTwoLoaderSequenceManifest manifest =
                        LoadPrecisionTwoManifest(args[2], 12);
                    int durationSeconds = args.Length >= 4 ?
                        int.Parse(args[3]) : 120;
                    int maximumRequests = args.Length >= 5 ?
                        int.Parse(args[4]) : 64;
                    if (args.Length > 5)
                    {
                        throw new ArgumentException(
                            "capture-after-twelve-loader-records accepts the " +
                            "approval token, firmware path, and at most two " +
                            "guard arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.LoaderOnce,
                        LoaderWriteBufferExperiment.
                            ExecuteFirstTwelveRecordsOnceAndOffline, manifest);
                }
                if (args[0] ==
                    "capture-after-fourteen-loader-records")
                {
                    const string acknowledgment =
                        "I-APPROVE-FOURTEEN-PRECISION2-LOADER-RECORDS-" +
                        "851841B2-76B6D086-18CF6629-6AD16D08-C191E5F7-" +
                        "F071559D-3CDCFC4F-06233C39-52DAA670-2EAD7C79-" +
                        "91A66555-EA7EB884-C29A2A91-DEB4D4A4";
                    if (args.Length < 3 || args[1] != acknowledgment)
                    {
                        throw new ArgumentException(
                            "The fourteen-loader-record experiment requires " +
                            "its exact approval token and firmware path.");
                    }
                    PrecisionTwoLoaderSequenceManifest manifest =
                        LoadPrecisionTwoManifest(args[2], 14);
                    int durationSeconds = args.Length >= 4 ?
                        int.Parse(args[3]) : 120;
                    int maximumRequests = args.Length >= 5 ?
                        int.Parse(args[4]) : 64;
                    if (args.Length > 5)
                    {
                        throw new ArgumentException(
                            "capture-after-fourteen-loader-records accepts " +
                            "the approval token, firmware path, and at most " +
                            "two guard arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.LoaderOnce,
                        LoaderWriteBufferExperiment.
                            ExecuteFirstFourteenRecordsOnceAndOffline, manifest);
                }
                if (args[0] ==
                    "capture-after-sixteen-loader-records")
                {
                    const string acknowledgment =
                        "I-APPROVE-SIXTEEN-PRECISION2-LOADER-RECORDS-" +
                        "851841B2-76B6D086-18CF6629-6AD16D08-C191E5F7-" +
                        "F071559D-3CDCFC4F-06233C39-52DAA670-2EAD7C79-" +
                        "91A66555-EA7EB884-C29A2A91-DEB4D4A4-FEFA54D1-" +
                        "5B30D224";
                    if (args.Length < 3 || args[1] != acknowledgment)
                    {
                        throw new ArgumentException(
                            "The sixteen-loader-record experiment requires " +
                            "its exact approval token and firmware path.");
                    }
                    PrecisionTwoLoaderSequenceManifest manifest =
                        LoadPrecisionTwoManifest(args[2], 16);
                    int durationSeconds = args.Length >= 4 ?
                        int.Parse(args[3]) : 120;
                    int maximumRequests = args.Length >= 5 ?
                        int.Parse(args[4]) : 64;
                    if (args.Length > 5)
                    {
                        throw new ArgumentException(
                            "capture-after-sixteen-loader-records accepts " +
                            "the approval token, firmware path, and at most " +
                            "two guard arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.LoaderOnce,
                        LoaderWriteBufferExperiment.
                            ExecuteAllSixteenRecordsOnceAndOffline, manifest);
                }
                if (args[0] == "capture-after-loader-terminal")
                {
                    const string acknowledgment =
                        "I-APPROVE-PRECISION2-COMPLETE-LOADER-SEQUENCE-" +
                        "D8D71885-7C3D21A4";
                    if (args.Length < 3 || args[1] != acknowledgment)
                    {
                        throw new ArgumentException(
                            "The complete loader terminal experiment requires " +
                            "its exact approval token and firmware path.");
                    }
                    PrecisionTwoLoaderSequenceManifest manifest =
                        LoadPrecisionTwoManifest(args[2], 16);
                    RequirePrecisionTwoTerminalRecord(manifest);
                    int durationSeconds = args.Length >= 4 ?
                        int.Parse(args[3]) : 120;
                    int maximumRequests = args.Length >= 5 ?
                        int.Parse(args[4]) : 64;
                    if (args.Length > 5)
                    {
                        throw new ArgumentException(
                            "capture-after-loader-terminal accepts the approval " +
                            "token, firmware path, and at most two guard " +
                            "arguments.");
                    }
                    return OnlineBrokerSession.Run(durationSeconds,
                        maximumRequests, true,
                        ReadBufferD8Experiment.LoaderOnce,
                        LoaderWriteBufferExperiment.
                            ExecuteAllSixteenRecordsAndTerminalOnceAndOffline,
                        manifest);
                }

                Console.Error.WriteLine("Unknown command: {0}", args[0]);
                PrintUsage();
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: {0}", ex.Message);
                return 1;
            }
        }

        private static int DryRun()
        {
            ServiceRequestConnection.ValidateNativeLayout();
            const ulong generation = 1;
            byte[] state = BrokerWireProtocol.SerializeAdapterState(
                new BrokerAdapterState(generation,
                    BrokerAdapterStateKind.Offline, 0, 0, 0, new byte[0]));
            MiniportResponse wrapped = MiniportWireProtocol.ParseResponse(
                MiniportWireProtocol.WrapMessage(state));
            BrokerAdapterState parsed = BrokerWireProtocol.ParseAdapterState(
                wrapped.Payload);
            byte[] wait = BrokerWireProtocol.SerializeServiceWait(generation);
            ulong waitGeneration = BrokerWireProtocol.ParseServiceWait(wait);
            Console.WriteLine(
                "Offline protocol dry-run passed (generation {0}, state {1}, wait {2} bytes).",
                parsed.Generation, parsed.State, wait.Length);
            if (waitGeneration != generation)
            {
                throw new BrokerProtocolException(
                    "Service-wait generation changed during serialization.");
            }
            Console.WriteLine(
                "Native service-wait layout is valid. No device was opened and " +
                "no driver or scanner state was changed.");
            return 0;
        }

        private static int ShowStatus()
        {
            IList<StoragePortDevice> devices =
                StoragePortDiscovery.FindUsb2XchangeAdapters();
            if (devices.Count == 0)
            {
                Console.WriteLine(
                    "USB2Xchange virtual miniport is not installed/present.");
                return 3;
            }
            foreach (StoragePortDevice device in devices)
            {
                Console.WriteLine("{0}", device.InstanceId);
                Console.WriteLine("  {0}", device.DevicePath);
            }
            return 0;
        }

        private static int SendOffline(ulong generation)
        {
            if (generation == 0)
            {
                throw new ArgumentOutOfRangeException("generation",
                    "Generation must be nonzero.");
            }
            IList<StoragePortDevice> devices =
                StoragePortDiscovery.FindUsb2XchangeAdapters();
            if (devices.Count != 1)
            {
                throw new InvalidOperationException(string.Format(
                    "Expected exactly one USB2Xchange virtual miniport; found {0}.",
                    devices.Count));
            }

            byte[] state = BrokerWireProtocol.SerializeAdapterState(
                new BrokerAdapterState(generation,
                    BrokerAdapterStateKind.Offline, 0, 0, 0, new byte[0]));
            using (MiniportConnection connection =
                new MiniportConnection(devices[0].DevicePath))
            {
                MiniportResponse response = connection.Send(state);
                if (response.ReturnCode != MiniportReturnCode.Success)
                {
                    throw new InvalidOperationException(string.Format(
                        "Miniport returned {0}.", response.ReturnCode));
                }
            }
            Console.WriteLine(
                "Miniport accepted offline generation {0}; no scanner target is exposed.",
                generation);
            return 0;
        }

        private static PrecisionTwoLoaderSequenceManifest
            LoadPrecisionTwoManifest(string firmwarePath,
                int requiredRecordCount)
        {
            if (string.IsNullOrWhiteSpace(firmwarePath))
            {
                throw new ArgumentException(
                    "The Precision II firmware path is required.",
                    "firmwarePath");
            }
            if (requiredRecordCount < 1 || requiredRecordCount > 16)
            {
                throw new ArgumentOutOfRangeException("requiredRecordCount");
            }
            string fullPath = Path.GetFullPath(firmwarePath);
            if (!string.Equals(Path.GetFileName(fullPath), "MICROCOD.3XX",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Refusing a firmware file not named MICROCOD.3XX.");
            }
            byte[] firmware = File.ReadAllBytes(fullPath);
            try
            {
                PrecisionTwoLoaderSequenceManifest manifest =
                    PrecisionTwoLoaderSequenceManifest.Create(firmware,
                        PrecisionTwoFirmwareSha256);
                if (!string.Equals(manifest.GetRecordSha256(0),
                    PrecisionTwoFirstRecordSha256,
                    StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The first loader record does not match the separately " +
                        "approved SHA-256.");
                }
                if (requiredRecordCount >= 2 &&
                    !string.Equals(manifest.GetRecordSha256(1),
                        PrecisionTwoSecondRecordSha256,
                        StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The second loader record does not match the " +
                        "separately gated SHA-256.");
                }
                if (requiredRecordCount >= 3 &&
                    !string.Equals(manifest.GetRecordSha256(2),
                        PrecisionTwoThirdRecordSha256,
                        StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The third loader record does not match the " +
                        "separately gated SHA-256.");
                }
                if (requiredRecordCount >= 4 &&
                    !string.Equals(manifest.GetRecordSha256(3),
                        PrecisionTwoFourthRecordSha256,
                        StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The fourth loader record does not match the " +
                        "separately gated SHA-256.");
                }
                if (requiredRecordCount >= 5 &&
                    !string.Equals(manifest.GetRecordSha256(4),
                        PrecisionTwoFifthRecordSha256,
                        StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The fifth loader record does not match the " +
                        "separately gated SHA-256.");
                }
                if (requiredRecordCount >= 6 &&
                    !string.Equals(manifest.GetRecordSha256(5),
                        PrecisionTwoSixthRecordSha256,
                        StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The sixth loader record does not match the " +
                        "separately gated SHA-256.");
                }
                if (requiredRecordCount >= 7 &&
                    !string.Equals(manifest.GetRecordSha256(6),
                        PrecisionTwoSeventhRecordSha256,
                        StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The seventh loader record does not match the " +
                        "separately gated SHA-256.");
                }
                if (requiredRecordCount >= 8 &&
                    !string.Equals(manifest.GetRecordSha256(7),
                        PrecisionTwoEighthRecordSha256,
                        StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The eighth loader record does not match the " +
                        "separately gated SHA-256.");
                }
                if (requiredRecordCount >= 9 &&
                    !string.Equals(manifest.GetRecordSha256(8),
                        PrecisionTwoNinthRecordSha256,
                        StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The ninth loader record does not match the " +
                        "separately gated SHA-256.");
                }
                if (requiredRecordCount >= 10 &&
                    !string.Equals(manifest.GetRecordSha256(9),
                        PrecisionTwoTenthRecordSha256,
                        StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The tenth loader record does not match the " +
                        "separately gated SHA-256.");
                }
                if (requiredRecordCount >= 11 &&
                    !string.Equals(manifest.GetRecordSha256(10),
                        PrecisionTwoEleventhRecordSha256,
                        StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The eleventh loader record does not match the " +
                        "separately gated SHA-256.");
                }
                if (requiredRecordCount >= 12 &&
                    !string.Equals(manifest.GetRecordSha256(11),
                        PrecisionTwoTwelfthRecordSha256,
                        StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The twelfth loader record does not match the " +
                        "separately gated SHA-256.");
                }
                if (requiredRecordCount >= 13 &&
                    !string.Equals(manifest.GetRecordSha256(12),
                        PrecisionTwoThirteenthRecordSha256,
                        StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The thirteenth loader record does not match the " +
                        "separately gated SHA-256.");
                }
                if (requiredRecordCount >= 14 &&
                    !string.Equals(manifest.GetRecordSha256(13),
                        PrecisionTwoFourteenthRecordSha256,
                        StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The fourteenth loader record does not match the " +
                        "separately gated SHA-256.");
                }
                if (requiredRecordCount >= 15 &&
                    !string.Equals(manifest.GetRecordSha256(14),
                        PrecisionTwoFifteenthRecordSha256,
                        StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The fifteenth loader record does not match the " +
                        "separately gated SHA-256.");
                }
                if (requiredRecordCount >= 16 &&
                    !string.Equals(manifest.GetRecordSha256(15),
                        PrecisionTwoSixteenthRecordSha256,
                        StringComparison.Ordinal))
                {
                    throw new BrokerProtocolException(
                        "The sixteenth loader record does not match the " +
                        "separately gated SHA-256.");
                }
                Console.WriteLine(
                    "Verified local MICROCOD.3XX manifest: firmware-sha256={0}, " +
                    "first-record-sha256={1}, second-record-sha256={2}, " +
                    "third-record-sha256={3}, fourth-record-sha256={4}, " +
                    "fifth-record-sha256={5}, sixth-record-sha256={6}, " +
                    "seventh-record-sha256={7}, eighth-record-sha256={8}, " +
                    "ninth-record-sha256={9}, tenth-record-sha256={10}, " +
                    "eleventh-record-sha256={11}, twelfth-record-sha256={12}, " +
                    "thirteenth-record-sha256={13}, " +
                    "fourteenth-record-sha256={14}, " +
                    "fifteenth-record-sha256={15}, " +
                    "sixteenth-record-sha256={16}. " +
                    "Required-prefix-records={17}. " +
                    "Payload bytes will not be logged.",
                    manifest.FirmwareSha256,
                    manifest.GetRecordSha256(0),
                    requiredRecordCount >= 2 ? manifest.GetRecordSha256(1) :
                        "not-required",
                    requiredRecordCount >= 3 ? manifest.GetRecordSha256(2) :
                        "not-required",
                    requiredRecordCount >= 4 ? manifest.GetRecordSha256(3) :
                        "not-required",
                    requiredRecordCount >= 5 ? manifest.GetRecordSha256(4) :
                        "not-required",
                    requiredRecordCount >= 6 ? manifest.GetRecordSha256(5) :
                        "not-required",
                    requiredRecordCount >= 7 ? manifest.GetRecordSha256(6) :
                        "not-required",
                    requiredRecordCount >= 8 ? manifest.GetRecordSha256(7) :
                        "not-required",
                    requiredRecordCount >= 9 ? manifest.GetRecordSha256(8) :
                        "not-required",
                    requiredRecordCount >= 10 ? manifest.GetRecordSha256(9) :
                        "not-required",
                    requiredRecordCount >= 11 ? manifest.GetRecordSha256(10) :
                        "not-required",
                    requiredRecordCount >= 12 ? manifest.GetRecordSha256(11) :
                        "not-required",
                    requiredRecordCount >= 13 ? manifest.GetRecordSha256(12) :
                        "not-required",
                    requiredRecordCount >= 14 ? manifest.GetRecordSha256(13) :
                        "not-required",
                    requiredRecordCount >= 15 ? manifest.GetRecordSha256(14) :
                        "not-required",
                    requiredRecordCount >= 16 ? manifest.GetRecordSha256(15) :
                        "not-required",
                    requiredRecordCount);
                return manifest;
            }
            finally
            {
                Array.Clear(firmware, 0, firmware.Length);
            }
        }

        private static void RequirePrecisionTwoTerminalRecord(
            PrecisionTwoLoaderSequenceManifest manifest)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException("manifest");
            }
            if (!string.Equals(manifest.TerminalRecordSha256,
                PrecisionTwoTerminalRecordSha256, StringComparison.Ordinal))
            {
                throw new BrokerProtocolException(
                    "The loader terminal record does not match the separately " +
                    "gated SHA-256.");
            }
            Console.WriteLine(
                "Verified exact terminal-record-sha256={0}; payload logging " +
                "remains redacted.", manifest.TerminalRecordSha256);
        }

        private static void PrintUsage()
        {
            Console.WriteLine("USB2Xchange broker checkpoint console");
            Console.WriteLine("  usb2xchange-broker dry-run");
            Console.WriteLine("  usb2xchange-broker status");
            Console.WriteLine(
                "  usb2xchange-broker validate-first-loader-record " +
                "<MICROCOD.3XX-path>");
            Console.WriteLine(
                "  usb2xchange-broker validate-two-loader-records " +
                "<MICROCOD.3XX-path>");
            Console.WriteLine(
                "  usb2xchange-broker validate-three-loader-records " +
                "<MICROCOD.3XX-path>");
            Console.WriteLine(
                "  usb2xchange-broker validate-four-loader-records " +
                "<MICROCOD.3XX-path>");
            Console.WriteLine(
                "  usb2xchange-broker validate-five-loader-records " +
                "<MICROCOD.3XX-path>");
            Console.WriteLine(
                "  usb2xchange-broker validate-six-loader-records " +
                "<MICROCOD.3XX-path>");
            Console.WriteLine(
                "  usb2xchange-broker validate-eight-loader-records " +
                "<MICROCOD.3XX-path>");
            Console.WriteLine(
                "  usb2xchange-broker validate-ten-loader-records " +
                "<MICROCOD.3XX-path>");
            Console.WriteLine(
                "  usb2xchange-broker validate-twelve-loader-records " +
                "<MICROCOD.3XX-path>");
            Console.WriteLine(
                "  usb2xchange-broker validate-fourteen-loader-records " +
                "<MICROCOD.3XX-path>");
            Console.WriteLine(
                "  usb2xchange-broker validate-sixteen-loader-records " +
                "<MICROCOD.3XX-path>");
            Console.WriteLine(
                "  usb2xchange-broker validate-complete-loader-sequence " +
                "<MICROCOD.3XX-path>");
            Console.WriteLine("  usb2xchange-broker offline [generation]");
            Console.WriteLine(
                "  usb2xchange-broker serve-readonly [seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-first-blocked [seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-after-read-buffer-d8 [seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker " +
                "capture-after-operational-read-buffer-d8 " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker " +
                "capture-after-operational-scanner-ready " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker " +
                "capture-after-operational-fault-pixels " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker " +
                "capture-operational-initialization-readonly " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-after-preview-set-window " +
                "<explicit-approval-token> [seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker " +
                "capture-after-preview-set-window-next-blocked " +
                "<explicit-approval-token> [seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-after-write-buffer-mode1-zero " +
                "<explicit-approval-token> [seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker " +
                "capture-after-simulated-write-buffer-error " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-after-first-loader-record " +
                "<explicit-approval-token> <MICROCOD.3XX-path> " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-after-two-loader-records " +
                "<explicit-approval-token> <MICROCOD.3XX-path> " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-after-three-loader-records " +
                "<explicit-approval-token> <MICROCOD.3XX-path> " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-after-four-loader-records " +
                "<explicit-approval-token> <MICROCOD.3XX-path> " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-after-five-loader-records " +
                "<explicit-approval-token> <MICROCOD.3XX-path> " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-after-six-loader-records " +
                "<explicit-approval-token> <MICROCOD.3XX-path> " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-after-eight-loader-records " +
                "<explicit-approval-token> <MICROCOD.3XX-path> " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-after-ten-loader-records " +
                "<explicit-approval-token> <MICROCOD.3XX-path> " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-after-twelve-loader-records " +
                "<explicit-approval-token> <MICROCOD.3XX-path> " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-after-fourteen-loader-records " +
                "<explicit-approval-token> <MICROCOD.3XX-path> " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-after-sixteen-loader-records " +
                "<explicit-approval-token> <MICROCOD.3XX-path> " +
                "[seconds] [max-requests]");
            Console.WriteLine(
                "  usb2xchange-broker capture-after-loader-terminal " +
                "<explicit-approval-token> <MICROCOD.3XX-path> " +
                "[seconds] [max-requests]");
            Console.WriteLine();
            Console.WriteLine(
                "Normal guarded modes execute only the verified target-5 scanner " +
                "and three read-only six-byte SCSI commands; the observed " +
                "single-LUN REPORT LUNS response is synthetic. Capture mode logs " +
                "and rejects the first other CDB, then returns offline. The D8 " +
                "extensions are identity-specific and permit exactly one " +
                "captured READ BUFFER D8. The operational ScannerReady mode " +
                "also permits the exact two-byte data-in poll once only after " +
                "successful operational D8 completion. The fault-pixel mode " +
                "also permits the exact 22-byte E0 READ BUFFER once after a " +
                "successful ScannerReady completion and one exact following " +
                "ScannerReady poll. If the header is the observed 0,1,0 form, " +
                "it permits the derived 58-byte E0 data read and the following " +
                "1,024-byte E0 calibration read once each, followed by one " +
                "exact ten-byte D8 offset-55 read and one exact ScannerReady " +
                "poll, then stops at the next blocked request.");
            Console.WriteLine(
                "The operational initialization mode repeats only that exact " +
                "ordered read-only calibration cycle, at most six times as " +
                "bounded by the FlexColor loop, and stops on the first new or " +
                "variant request.");
            Console.WriteLine(
                "The Preview SET WINDOW experiment is state-changing and " +
                "requires its separate exact acknowledgment. It permits only " +
                "the captured 24h CDB and 84-byte payload SHA-256 immediately " +
                "after a completed initialization cycle, performs no retry or " +
                "automatic sense, redacts the payload, and forces offline after " +
                "the single attempt.");
            Console.WriteLine(
                "The separate Preview continuation capture uses a different " +
                "exact acknowledgment. After the same one-shot pinned request, " +
                "it stays online only until the normal policy logs and blocks " +
                "the next non-allowlisted command; a second SET WINDOW cannot " +
                "reach transport.");
            Console.WriteLine(
                "The WRITE BUFFER experiment is state-changing, remains " +
                "unreachable without its exact acknowledgment, and must not be " +
                "run without separate explicit approval. It permits only the " +
                "observed zero-parameter 3B/01 form after D8 and forces offline.");
            Console.WriteLine(
                "The simulation mode never submits WRITE BUFFER. It returns the " +
                "observed ProtocolError locally and observes whether another " +
                "request follows under the normal hardware blocklist.");
            Console.WriteLine(
                "The first-record experiment independently verifies the exact " +
                "firmware and record hashes, permits one 4,106-byte record only " +
                "after D8, redacts its payload, disables retry and sense, and " +
                "forces offline after its single completion.");
            Console.WriteLine(
                "The two-record experiment has a different exact token. It " +
                "requires record 1 to complete exactly before record 2, permits " +
                "no third or terminal record, redacts both payloads, and forces " +
                "offline after record 2 or any failure.");
            Console.WriteLine(
                "The three-record experiment has another exact token. It " +
                "requires exact successful completions in order, permits no " +
                "fourth or terminal record, redacts all payloads, and forces " +
                "offline after record 3 or any failure.");
            Console.WriteLine(
                "The four-record experiment has another exact token. It " +
                "requires exact successful completions in order, permits no " +
                "fifth or terminal record, redacts all payloads, and forces " +
                "offline after record 4 or any failure.");
            Console.WriteLine(
                "The five-record experiment has another exact token. It " +
                "requires exact successful completions in order, permits no " +
                "sixth or terminal record, redacts all payloads, and forces " +
                "offline after record 5 or any failure.");
            Console.WriteLine(
                "The six-record experiment has another exact token. It " +
                "requires exact successful completions in order, permits no " +
                "seventh or terminal record, redacts all payloads, and forces " +
                "offline after record 6 or any failure.");
            Console.WriteLine(
                "The eight-record experiment has another exact token. It " +
                "requires exact successful completions in order, permits no " +
                "ninth or terminal record, redacts all payloads, and forces " +
                "offline after record 8 or any failure.");
            Console.WriteLine(
                "The ten-record experiment has another exact token. It " +
                "requires exact successful completions in order, permits no " +
                "eleventh or terminal record, redacts all payloads, and forces " +
                "offline after record 10 or any failure.");
            Console.WriteLine(
                "The twelve-record experiment has another exact token. It " +
                "requires exact successful completions in order, permits no " +
                "thirteenth or terminal record, redacts all payloads, and " +
                "forces offline after record 12 or any failure.");
            Console.WriteLine(
                "The fourteen-record experiment has another exact token. It " +
                "requires exact successful completions in order, permits no " +
                "fifteenth or terminal record, redacts all payloads, and " +
                "forces offline after record 14 or any failure.");
            Console.WriteLine(
                "The sixteen-record experiment has another exact token. It " +
                "requires exact successful completions in order, does not " +
                "permit the terminal record, redacts all payloads, and forces " +
                "offline after record 16 or any failure.");
            Console.WriteLine(
                "The terminal experiment has a distinct exact token. It " +
                "requires all sixteen records to complete exactly before one " +
                "exact ten-byte terminal submission, performs no retry, sense, " +
                "reset, or later write, redacts every payload, and forces " +
                "offline after the terminal attempt or any failure.");
        }
    }
}
