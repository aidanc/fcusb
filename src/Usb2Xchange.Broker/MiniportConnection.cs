// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Usb2Xchange.BrokerProtocol;

namespace Usb2Xchange.Broker
{
    internal sealed class MiniportConnection : IDisposable
    {
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint ShareRead = 0x00000001;
        private const uint ShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint IoctlScsiMiniport = 0x0004D008;

        private readonly SafeFileHandle handle;

        internal MiniportConnection(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
            {
                throw new ArgumentNullException("devicePath");
            }
            handle = CreateFile(devicePath, GenericRead | GenericWrite,
                ShareRead | ShareWrite, IntPtr.Zero, OpenExisting, 0,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Could not open the USB2Xchange StoragePort interface.");
            }
        }

        internal MiniportResponse Send(byte[] payload)
        {
            byte[] input = MiniportWireProtocol.WrapMessage(payload);
            byte[] output = new byte[input.Length];
            uint bytesReturned;
            if (!DeviceIoControl(handle, IoctlScsiMiniport, input,
                    checked((uint)input.Length), output,
                    checked((uint)output.Length), out bytesReturned,
                    IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "The USB2Xchange miniport rejected the broker IOCTL.");
            }
            if (bytesReturned != output.Length)
            {
                throw new BrokerProtocolException(
                    "The miniport returned a truncated SRB_IO_CONTROL buffer.");
            }
            return MiniportWireProtocol.ParseResponse(output);
        }

        public void Dispose()
        {
            handle.Dispose();
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
            uint controlCode, byte[] inputBuffer, uint inputBufferSize,
            byte[] outputBuffer, uint outputBufferSize,
            out uint bytesReturned, IntPtr overlapped);
    }
}
