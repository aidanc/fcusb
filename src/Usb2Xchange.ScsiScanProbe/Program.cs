// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Usb2Xchange.Protocol;

namespace Usb2Xchange.ScsiScanProbe
{
    internal static class Program
    {
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint ShareRead = 0x00000001;
        private const uint ShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint FileFlagOverlapped = 0x40000000;
        private const uint IoctlScsiScanCommand = 0x00190012;
        private const uint IoctlScsiScanGetInfo = 0x00190022;
        private const uint SrbFlagsDataIn = 0x00000040;
        private const byte SrbStatusSuccess = 0x01;
        private const byte SrbStatusMask = 0x3F;
        private const int ScannerInfoSize = 144;
        private const int CommandSize32 = 44;
        private const byte InquiryLength = 96;
        private const byte SenseLength = 32;
        private const int ErrorIoPending = 997;
        private const uint WaitObject0 = 0;
        private const uint WaitTimeout = 258;
        private const int IoctlTimeoutMilliseconds = 30000;

        private static int Main(string[] args)
        {
            try
            {
                RequireNativeLayout();
                if (args.Length == 1 && args[0] == "dry-run")
                {
                    Console.WriteLine(
                        "SCSISCAN x86 layout and read-only INQUIRY command are valid.");
                    Console.WriteLine(
                        "No scanner path was opened and no IOCTL was sent.");
                    return 0;
                }
                if (args.Length > 1)
                {
                    PrintUsage();
                    return 2;
                }

                string path = args.Length == 1 ? args[0] : FindScannerPath();
                if (path == null)
                {
                    throw new InvalidOperationException(
                        "No \\\\.\\Scanner0 through Scanner15 path could be opened.");
                }
                return Probe(path);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: {0}", ex.Message);
                return 1;
            }
        }

        private static int Probe(string path)
        {
            using (SafeFileHandle scanner = OpenScanner(path))
            {
                byte[] infoBytes = new byte[ScannerInfoSize];
                uint infoReturned = ExecuteIoControl(scanner,
                    IoctlScsiScanGetInfo, null, infoBytes,
                    IoctlTimeoutMilliseconds);
                uint reportedInfoSize = ReadUInt32(infoBytes, 0);
                if ((infoReturned != 0 && infoReturned != ScannerInfoSize) ||
                    (reportedInfoSize != 0 &&
                        reportedInfoSize != ScannerInfoSize))
                {
                    throw new InvalidOperationException(string.Format(
                        "SCSISCAN_INFO was truncated or invalid " +
                        "(IOCTL bytes={0}, structure size={1}).",
                        infoReturned, reportedInfoSize));
                }

                string adapter = Encoding.ASCII.GetString(infoBytes, 12, 128).
                    TrimEnd('\0', ' ');
                Console.WriteLine("Opened {0}", path);
                Console.WriteLine(
                    "SCSISCAN_INFO: port={0}, path={1}, target={2}, LUN={3}, " +
                    "adapter='{4}', IOCTL bytes={5}, structure size={6}",
                    infoBytes[8], infoBytes[9], infoBytes[10], infoBytes[11],
                    adapter, infoReturned, reportedInfoSize);

                byte[] status = new byte[1];
                byte[] sense = new byte[SenseLength];
                byte[] data = new byte[InquiryLength];
                GCHandle statusPin = default(GCHandle);
                GCHandle sensePin = default(GCHandle);
                try
                {
                    statusPin = GCHandle.Alloc(status, GCHandleType.Pinned);
                    sensePin = GCHandle.Alloc(sense, GCHandleType.Pinned);
                    byte[] command = BuildInquiryCommand(
                        statusPin.AddrOfPinnedObject(),
                        sensePin.AddrOfPinnedObject());
                    uint dataReturned = ExecuteIoControl(scanner,
                        IoctlScsiScanCommand, command, data,
                        IoctlTimeoutMilliseconds);
                    if ((status[0] & SrbStatusMask) != SrbStatusSuccess)
                    {
                        throw new InvalidOperationException(string.Format(
                            "SCSISCAN INQUIRY returned SRB status 0x{0:X2}; sense={1}.",
                            status[0], ToHex(sense)));
                    }
                    if (dataReturned < 36 || dataReturned > data.Length)
                    {
                        throw new InvalidOperationException(string.Format(
                            "SCSISCAN INQUIRY returned invalid length {0}.",
                            dataReturned));
                    }
                    byte[] inquiryBytes = new byte[dataReturned];
                    Buffer.BlockCopy(data, 0, inquiryBytes, 0,
                        checked((int)dataReturned));
                    InquiryData inquiry = InquiryData.Parse(inquiryBytes);
                    if (inquiry.PeripheralDeviceType != 0x06 ||
                        !string.Equals(inquiry.Vendor, "Imacon",
                            StringComparison.Ordinal) ||
                        !string.Equals(inquiry.Product, "SCSI Loader",
                            StringComparison.Ordinal) ||
                        !string.Equals(inquiry.Revision, "L302",
                            StringComparison.Ordinal))
                    {
                        throw new ProtocolException(string.Format(
                            "Unexpected SCSISCAN identity: {0} / {1} / {2}, type {3:X2}.",
                            inquiry.Vendor, inquiry.Product, inquiry.Revision,
                            inquiry.PeripheralDeviceType));
                    }
                    Console.WriteLine(
                        "SCSISCAN INQUIRY: {0} / {1} / {2}, type {3:X2}, " +
                        "bytes={4}, SRB=0x{5:X2}", inquiry.Vendor,
                        inquiry.Product, inquiry.Revision,
                        inquiry.PeripheralDeviceType, dataReturned, status[0]);
                    return 0;
                }
                finally
                {
                    if (sensePin.IsAllocated)
                    {
                        sensePin.Free();
                    }
                    if (statusPin.IsAllocated)
                    {
                        statusPin.Free();
                    }
                }
            }
        }

        private static string FindScannerPath()
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            do
            {
                for (int index = 0; index < 16; index++)
                {
                    string path = string.Format("\\\\.\\Scanner{0}", index);
                    using (SafeFileHandle handle = OpenScanner(path, false))
                    {
                        if (handle != null)
                        {
                            return path;
                        }
                    }
                }
                Thread.Sleep(250);
            }
            while (DateTime.UtcNow < deadline);
            return null;
        }

        private static SafeFileHandle OpenScanner(string path)
        {
            SafeFileHandle handle = OpenScanner(path, true);
            if (handle == null)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Could not open " + path + ".");
            }
            return handle;
        }

        private static SafeFileHandle OpenScanner(string path, bool throwOnError)
        {
            SafeFileHandle handle = CreateFile(path,
                GenericRead | GenericWrite, ShareRead | ShareWrite,
                IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
            if (!handle.IsInvalid)
            {
                return handle;
            }
            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (throwOnError)
            {
                throw new Win32Exception(error, "Could not open " + path + ".");
            }
            return null;
        }

        private static void RequireNativeLayout()
        {
            if (IntPtr.Size != 4)
            {
                throw new PlatformNotSupportedException(
                    "The FlexColor-compatible SCSISCAN probe must run as x86.");
            }
            int size = Marshal.SizeOf(typeof(ScsiScanCommand));
            if (size != CommandSize32)
            {
                throw new TypeLoadException(string.Format(
                    "SCSISCAN_CMD layout is {0} bytes; expected 44.", size));
            }
            int overlappedSize = Marshal.SizeOf(typeof(OverlappedData));
            if (overlappedSize != 20)
            {
                throw new TypeLoadException(string.Format(
                    "x86 OVERLAPPED layout is {0} bytes; expected 20.",
                    overlappedSize));
            }

            ScsiScanCommand command = new ScsiScanCommand();
            command.Size = CommandSize32;
            command.SrbFlags = SrbFlagsDataIn;
            command.CdbLength = 6;
            command.SenseLength = SenseLength;
            command.TransferLength = InquiryLength;
            command.Cdb = new byte[16];
            command.Cdb[0] = 0x12;
            command.Cdb[4] = InquiryLength;
            if (IoctlScsiScanCommand != 0x00190012 ||
                IoctlScsiScanGetInfo != 0x00190022 ||
                command.Cdb[0] != 0x12 || command.Cdb[4] != InquiryLength)
            {
                throw new InvalidOperationException(
                    "SCSISCAN IOCTL or read-only CDB constants are invalid.");
            }
        }

        private static byte[] BuildInquiryCommand(IntPtr statusPointer,
            IntPtr sensePointer)
        {
            byte[] bytes = new byte[CommandSize32];
            WriteUInt32(bytes, 4, CommandSize32);
            WriteUInt32(bytes, 8, SrbFlagsDataIn);
            bytes[12] = 6;
            bytes[13] = SenseLength;
            WriteUInt32(bytes, 16, InquiryLength);
            bytes[20] = 0x12;
            bytes[24] = InquiryLength;
            WriteUInt32(bytes, 36,
                unchecked((uint)statusPointer.ToInt32()));
            WriteUInt32(bytes, 40,
                unchecked((uint)sensePointer.ToInt32()));
            return bytes;
        }

        private static uint ExecuteIoControl(SafeFileHandle device,
            uint controlCode, byte[] input, byte[] output,
            int timeoutMilliseconds)
        {
            GCHandle inputPin = default(GCHandle);
            GCHandle outputPin = default(GCHandle);
            try
            {
                IntPtr inputPointer = IntPtr.Zero;
                uint inputLength = 0;
                if (input != null && input.Length != 0)
                {
                    inputPin = GCHandle.Alloc(input, GCHandleType.Pinned);
                    inputPointer = inputPin.AddrOfPinnedObject();
                    inputLength = checked((uint)input.Length);
                }
                IntPtr outputPointer = IntPtr.Zero;
                uint outputLength = 0;
                if (output != null && output.Length != 0)
                {
                    outputPin = GCHandle.Alloc(output, GCHandleType.Pinned);
                    outputPointer = outputPin.AddrOfPinnedObject();
                    outputLength = checked((uint)output.Length);
                }

                using (SafeWaitHandle waitHandle = CreateEvent(IntPtr.Zero,
                    true, false, null))
                {
                    if (waitHandle.IsInvalid)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(),
                            "CreateEvent for SCSISCAN IOCTL failed.");
                    }
                    OverlappedData overlapped = new OverlappedData();
                    overlapped.EventHandle = waitHandle.DangerousGetHandle();
                    uint immediate;
                    bool completed = DeviceIoControl(device, controlCode,
                        inputPointer, inputLength,
                        outputPointer, outputLength, out immediate,
                        ref overlapped);
                    if (completed)
                    {
                        return immediate;
                    }

                    int error = Marshal.GetLastWin32Error();
                    if (error != ErrorIoPending)
                    {
                        throw new Win32Exception(error, string.Format(
                            "SCSISCAN IOCTL 0x{0:X8} failed with Win32 {1}.",
                            controlCode, error));
                    }
                    uint wait = WaitForSingleObject(waitHandle,
                        checked((uint)timeoutMilliseconds));
                    if (wait == WaitTimeout)
                    {
                        CancelIoEx(device, ref overlapped);
                        uint ignored;
                        GetOverlappedResult(device, ref overlapped,
                            out ignored, true);
                        throw new TimeoutException(string.Format(
                            "SCSISCAN IOCTL 0x{0:X8} timed out.", controlCode));
                    }
                    if (wait != WaitObject0)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(),
                            "WaitForSingleObject for SCSISCAN IOCTL failed.");
                    }

                    uint transferred;
                    if (!GetOverlappedResult(device, ref overlapped,
                            out transferred, false))
                    {
                        int completionError = Marshal.GetLastWin32Error();
                        throw new Win32Exception(completionError, string.Format(
                            "SCSISCAN IOCTL 0x{0:X8} completion failed with Win32 {1}.",
                            controlCode, completionError));
                    }
                    return transferred;
                }
            }
            finally
            {
                if (outputPin.IsAllocated)
                {
                    outputPin.Free();
                }
                if (inputPin.IsAllocated)
                {
                    inputPin.Free();
                }
            }
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset] |
                (bytes[offset + 1] << 8) |
                (bytes[offset + 2] << 16) |
                (bytes[offset + 3] << 24));
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = unchecked((byte)value);
            bytes[offset + 1] = unchecked((byte)(value >> 8));
            bytes[offset + 2] = unchecked((byte)(value >> 16));
            bytes[offset + 3] = unchecked((byte)(value >> 24));
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace('-', ' ');
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usb2Xchange.ScsiScanProbe [\\\\.\\ScannerN]");
            Console.WriteLine("Usb2Xchange.ScsiScanProbe dry-run");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ScsiScanCommand
        {
            public uint Reserved1;
            public uint Size;
            public uint SrbFlags;
            public byte CdbLength;
            public byte SenseLength;
            public byte Reserved2;
            public byte Reserved3;
            public uint TransferLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] Cdb;
            public IntPtr SrbStatusPointer;
            public IntPtr SenseBufferPointer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OverlappedData
        {
            public IntPtr Internal;
            public IntPtr InternalHigh;
            public uint Offset;
            public uint OffsetHigh;
            public IntPtr EventHandle;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string fileName,
            uint desiredAccess, uint shareMode, IntPtr securityAttributes,
            uint creationDisposition, uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(SafeFileHandle device,
            uint controlCode, IntPtr inputBuffer, uint inputBufferSize,
            IntPtr outputBuffer, uint outputBufferSize,
            out uint bytesReturned, ref OverlappedData overlapped);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern SafeWaitHandle CreateEvent(
            IntPtr eventAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool manualReset,
            [MarshalAs(UnmanagedType.Bool)] bool initialState,
            string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(SafeWaitHandle handle,
            uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOverlappedResult(
            SafeFileHandle fileHandle, ref OverlappedData overlapped,
            out uint transferred,
            [MarshalAs(UnmanagedType.Bool)] bool wait);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CancelIoEx(SafeFileHandle fileHandle,
            ref OverlappedData overlapped);
    }
}
