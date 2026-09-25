// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Usb2Xchange.Protocol;
using Usb2Xchange.WinUsb;

namespace Usb2Xchange.Cli
{
    internal sealed class ConsoleUsbLog : IUsbLog
    {
        private readonly bool verbose;

        public ConsoleUsbLog(bool verbose)
        {
            this.verbose = verbose;
        }

        public void Info(string message)
        {
            Write("INFO", message);
        }

        public void Trace(string message)
        {
            if (verbose)
            {
                Write("TRACE", message);
            }
        }

        public void Warning(string message)
        {
            Write("WARN", message);
        }

        private static void Write(string level, string message)
        {
            Console.Error.WriteLine("{0:O} [{1}] {2}",
                DateTimeOffset.Now, level, message);
        }
    }

    internal static class Program
    {
        private const int DefaultTransferTimeoutMilliseconds = 20000;
        private const int DefaultReenumerationSeconds = 20;

        private static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: {0}", ex.Message);
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine("CAUSE: {0}", ex.InnerException.Message);
                }
                Win32Exception win32 = ex as Win32Exception;
                if (win32 != null)
                {
                    Console.Error.WriteLine("WIN32_ERROR: {0}", win32.NativeErrorCode);
                }
                return 1;
            }
        }

        private static int Run(string[] originalArgs)
        {
            List<string> args = new List<string>(originalArgs);
            bool verbose = RemoveFlag(args, "--verbose");
            int timeoutMilliseconds = RemoveIntOption(args, "--timeout-ms",
                DefaultTransferTimeoutMilliseconds, 100, 120000);
            int reenumerationSeconds = RemoveIntOption(args,
                "--reenumeration-seconds", DefaultReenumerationSeconds, 1, 120);
            bool approveLoaderReadBufferD8 = RemoveFlag(args,
                "--approve-loader-read-buffer-d8");
            ConsoleUsbLog log = new ConsoleUsbLog(verbose);

            if (args.Count == 0 || args[0] == "help" || args[0] == "--help" ||
                args[0] == "-h")
            {
                PrintUsage();
                return 0;
            }

            string command = args[0].ToLowerInvariant();
            if (command == "firmware-info")
            {
                RequireArgumentCount(args, 2);
                PrintFirmwareInfo(FirmwareImage.Load(args[1]));
                return 0;
            }
            if (command == "list")
            {
                RequireArgumentCount(args, 1);
                return ListDevices(log);
            }
            if (command == "load")
            {
                RequireArgumentCount(args, 2);
                FirmwareImage firmware = FirmwareImage.Load(args[1]);
                firmware.RequireKnownUsb2XchangeImage();
                LoadFirmwareAndWait(firmware, log, timeoutMilliseconds,
                    reenumerationSeconds);
                return 0;
            }
            if (command == "inquiry")
            {
                RequireArgumentCount(args, 1);
                using (Usb2XchangeDevice device = RequireDevice(
                    UsbConstants.OperationalProductId, log, timeoutMilliseconds))
                {
                    RunInquiry(device);
                }
                return 0;
            }
            if (command == "scan")
            {
                RequireArgumentCount(args, 1);
                using (Usb2XchangeDevice device = RequireDevice(
                    UsbConstants.OperationalProductId, log, timeoutMilliseconds))
                {
                    return ScanTargets(device);
                }
            }
            if (command == "loader-read-buffer-d8")
            {
                RequireArgumentCount(args, 1);
                if (!approveLoaderReadBufferD8)
                {
                    throw new ArgumentException(
                        "loader-read-buffer-d8 requires the explicit " +
                        "--approve-loader-read-buffer-d8 flag.");
                }
                using (Usb2XchangeDevice device = RequireDevice(
                    UsbConstants.OperationalProductId, log,
                    DefaultTransferTimeoutMilliseconds))
                {
                    return RunLoaderReadBufferD8(device);
                }
            }
            if (command == "probe")
            {
                RequireArgumentCount(args, 2);
                FirmwareImage firmware = FirmwareImage.Load(args[1]);
                firmware.RequireKnownUsb2XchangeImage();
                Probe(firmware, log, timeoutMilliseconds, reenumerationSeconds);
                return 0;
            }

            throw new ArgumentException("Unknown command: " + args[0]);
        }

        private static int ListDevices(IUsbLog log)
        {
            IList<string> paths = DeviceEnumerator.FindDevicePaths();
            if (paths.Count == 0)
            {
                Console.WriteLine("No USB2Xchange WinUSB interfaces were found.");
                Console.WriteLine(
                    "If Device Manager shows PID 2002 as Unknown, WinUSB is not bound yet.");
                return 2;
            }

            int count = 0;
            foreach (string path in paths)
            {
                using (WinUsbDevice device = WinUsbDevice.Open(path, log))
                {
                    UsbDeviceDescriptorInfo descriptor = device.GetDeviceDescriptor();
                    if (descriptor.VendorId != UsbConstants.VendorId)
                    {
                        continue;
                    }
                    count++;
                    Console.WriteLine(
                        "VID={0:X4} PID={1:X4} REV={2:X4} USB={3:X4} EP0={4} PATH={5}",
                        descriptor.VendorId, descriptor.ProductId,
                        descriptor.DeviceVersion, descriptor.UsbVersion,
                        descriptor.MaxPacketSize0, path);
                }
            }
            if (count == 0)
            {
                Console.WriteLine("The interface GUID exists, but no VID 03F3 device matched.");
                return 2;
            }
            return 0;
        }

        private static void PrintFirmwareInfo(FirmwareImage firmware)
        {
            Console.WriteLine("Length: {0}", firmware.FileLength);
            Console.WriteLine("SHA-256: {0}", firmware.Sha256);
            Console.WriteLine("Data records: {0}", firmware.Records.Count);
            Console.WriteLine("Payload bytes: {0}", firmware.PayloadLength);
            Console.WriteLine("Terminator type: 0x{0:X8}", firmware.TerminatorType);
            Console.WriteLine("Known USB2Xchange image: {0}",
                firmware.IsKnownUsb2XchangeImage ? "yes" : "no");
        }

        private static Usb2XchangeDevice LoadFirmwareAndWait(
            FirmwareImage firmware, IUsbLog log, int timeoutMilliseconds,
            int reenumerationSeconds)
        {
            Usb2XchangeDevice loader = RequireDevice(
                UsbConstants.LoaderProductId, log, timeoutMilliseconds);
            try
            {
                loader.UploadKnownUsb2Firmware(firmware);
            }
            finally
            {
                loader.Dispose();
            }

            log.Info(string.Format("Waiting up to {0} seconds for PID 2003.",
                reenumerationSeconds));
            Usb2XchangeDevice operational = Usb2XchangeDevice.WaitFor(
                UsbConstants.OperationalProductId,
                TimeSpan.FromSeconds(reenumerationSeconds), log,
                timeoutMilliseconds);
            Console.WriteLine("USB2Xchange re-enumerated as PID 2003.");
            return operational;
        }

        private static void Probe(FirmwareImage firmware, IUsbLog log,
            int timeoutMilliseconds, int reenumerationSeconds)
        {
            Usb2XchangeDevice operational = Usb2XchangeDevice.Find(
                UsbConstants.OperationalProductId, log, timeoutMilliseconds);
            if (operational == null)
            {
                operational = LoadFirmwareAndWait(firmware, log,
                    timeoutMilliseconds, reenumerationSeconds);
            }
            else
            {
                log.Info("PID 2003 is already present; skipping firmware upload.");
            }

            using (operational)
            {
                RunInquiry(operational);
            }
        }

        private static void RunInquiry(Usb2XchangeDevice device)
        {
            device.InitializeOperational();
            InquiryData inquiry = device.Inquiry(0, 0);
            Console.WriteLine("INQUIRY succeeded for target 0, LUN 0.");
            PrintInquiry(inquiry);
        }

        private static int ScanTargets(Usb2XchangeDevice device)
        {
            device.InitializeOperational();
            Console.WriteLine(
                "Scanning SCSI target IDs 0 through 6 with read-only INQUIRY.");
            int respondingTargets = 0;
            for (byte target = 0; target <= 6; target++)
            {
                try
                {
                    InquiryData inquiry = device.Inquiry(target, 0);
                    respondingTargets++;
                    Console.WriteLine("Target {0}, LUN 0 responded:", target);
                    PrintInquiry(inquiry);
                }
                catch (ScsiSelectionTimeoutException)
                {
                    Console.WriteLine(
                        "Target {0}, LUN 0: no response (adapter status 8A).",
                        target);
                }
            }
            Console.WriteLine("Responding targets: {0}", respondingTargets);
            return respondingTargets == 0 ? 2 : 0;
        }

        private static int RunLoaderReadBufferD8(Usb2XchangeDevice device)
        {
            device.InitializeOperational();
            Console.WriteLine(
                "Requiring target 5, LUN 0 to identify exactly as " +
                "Imacon / SCSI Loader / L302.");
            Console.WriteLine(
                "Then issuing one approved READ BUFFER D8 request: " +
                "66 bytes, 10000 ms, no retry or reset.");
            ScsiCommandResult result =
                device.ReadPrecisionTwoLoaderBufferD8Once();
            Console.WriteLine(
                "Verified target 5, LUN 0: Imacon / SCSI Loader / L302.");
            Console.WriteLine(
                "READ BUFFER D8 completed: adapter status=0x{0:X2}, " +
                "actual={1}, residue={2}.", result.Status.RawStatus,
                result.Status.ActualLength, result.Status.Residue);
            Console.WriteLine("Response: {0}", ScsiFraming.ToHex(result.Data));
            Console.WriteLine(
                "The probe is stopping now; no retry or follow-up command was sent.");
            return result.Status.RawStatus == (byte)AdapterStatus.Success ? 0 : 3;
        }

        private static void PrintInquiry(InquiryData inquiry)
        {
            Console.WriteLine("Peripheral qualifier: {0}",
                inquiry.PeripheralQualifier);
            Console.WriteLine("Peripheral device type: 0x{0:X2}",
                inquiry.PeripheralDeviceType);
            Console.WriteLine("Removable: {0}", inquiry.Removable);
            Console.WriteLine("Vendor: {0}", inquiry.Vendor);
            Console.WriteLine("Product: {0}", inquiry.Product);
            Console.WriteLine("Revision: {0}", inquiry.Revision);
        }

        private static Usb2XchangeDevice RequireDevice(ushort productId,
            IUsbLog log, int timeoutMilliseconds)
        {
            Usb2XchangeDevice device = Usb2XchangeDevice.Find(productId,
                log, timeoutMilliseconds);
            if (device == null)
            {
                throw new InvalidOperationException(string.Format(
                    "VID 03F3, PID {0:X4} was not exposed through the project's " +
                    "WinUSB interface GUID. Check the WinUSB binding first.",
                    productId));
            }
            return device;
        }

        private static bool RemoveFlag(List<string> args, string name)
        {
            int index = args.IndexOf(name);
            if (index < 0)
            {
                return false;
            }
            args.RemoveAt(index);
            return true;
        }

        private static int RemoveIntOption(List<string> args, string name,
            int defaultValue, int minimum, int maximum)
        {
            int index = args.IndexOf(name);
            if (index < 0)
            {
                return defaultValue;
            }
            if (index + 1 >= args.Count)
            {
                throw new ArgumentException(name + " requires an integer value.");
            }
            int value;
            if (!Int32.TryParse(args[index + 1], out value) ||
                value < minimum || value > maximum)
            {
                throw new ArgumentException(string.Format(
                    "{0} must be an integer from {1} through {2}.",
                    name, minimum, maximum));
            }
            args.RemoveAt(index + 1);
            args.RemoveAt(index);
            return value;
        }

        private static void RequireArgumentCount(List<string> args, int expected)
        {
            if (args.Count != expected)
            {
                throw new ArgumentException(
                    "Incorrect arguments. Run with --help for usage.");
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("USB2Xchange read-only diagnostic");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  usb2xchange firmware-info <usb2xchange.fw>");
            Console.WriteLine("  usb2xchange list");
            Console.WriteLine("  usb2xchange load <usb2xchange.fw>");
            Console.WriteLine("  usb2xchange inquiry");
            Console.WriteLine("  usb2xchange scan");
            Console.WriteLine(
                "  usb2xchange loader-read-buffer-d8 --approve-loader-read-buffer-d8");
            Console.WriteLine("  usb2xchange probe <usb2xchange.fw>");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --verbose                 Log every USB transfer and payload.");
            Console.WriteLine("  --timeout-ms N            Per-transfer timeout (default 20000).");
            Console.WriteLine("  --reenumeration-seconds N PID transition timeout (default 20).");
            Console.WriteLine(
                "  --approve-loader-read-buffer-d8  Approve the exact target-5 " +
                "66-byte READ BUFFER D8 experiment.");
            Console.WriteLine();
            Console.WriteLine(
                "Only known firmware, read-only INQUIRY, and the explicitly " +
                "approved exact loader READ BUFFER D8 experiment are supported.");
        }
    }
}
