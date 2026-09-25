// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Usb2Xchange.WinUsb
{
    public sealed class UsbDeviceDescriptorInfo
    {
        internal UsbDeviceDescriptorInfo(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 18)
            {
                throw new ArgumentException("A complete USB device descriptor is required.");
            }
            UsbVersion = ReadUInt16(bytes, 2);
            DeviceClass = bytes[4];
            DeviceSubClass = bytes[5];
            DeviceProtocol = bytes[6];
            MaxPacketSize0 = bytes[7];
            VendorId = ReadUInt16(bytes, 8);
            ProductId = ReadUInt16(bytes, 10);
            DeviceVersion = ReadUInt16(bytes, 12);
            NumberOfConfigurations = bytes[17];
        }

        public ushort UsbVersion { get; private set; }
        public byte DeviceClass { get; private set; }
        public byte DeviceSubClass { get; private set; }
        public byte DeviceProtocol { get; private set; }
        public byte MaxPacketSize0 { get; private set; }
        public ushort VendorId { get; private set; }
        public ushort ProductId { get; private set; }
        public ushort DeviceVersion { get; private set; }
        public byte NumberOfConfigurations { get; private set; }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }
    }

    public sealed class UsbPipeInfo
    {
        internal UsbPipeInfo(WinUsbPipeInformation native)
        {
            TypeName = native.PipeType.ToString();
            PipeId = native.PipeId;
            MaximumPacketSize = native.MaximumPacketSize;
            Interval = native.Interval;
            IsBulk = native.PipeType == UsbPipeType.Bulk;
            IsIn = (native.PipeId & 0x80) != 0;
        }

        public string TypeName { get; private set; }
        public byte PipeId { get; private set; }
        public ushort MaximumPacketSize { get; private set; }
        public byte Interval { get; private set; }
        public bool IsBulk { get; private set; }
        public bool IsIn { get; private set; }
    }

    public sealed class WinUsbDevice : IDisposable
    {
        private readonly SafeFileHandle fileHandle;
        private readonly SafeWinUsbHandle interfaceHandle;
        private readonly IUsbLog log;
        private bool disposed;

        private WinUsbDevice(string path, SafeFileHandle fileHandle,
            SafeWinUsbHandle interfaceHandle, IUsbLog log)
        {
            Path = path;
            this.fileHandle = fileHandle;
            this.interfaceHandle = interfaceHandle;
            this.log = log ?? new NullUsbLog();
        }

        public string Path { get; private set; }

        public static WinUsbDevice Open(string path, IUsbLog log)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("A device-interface path is required.", "path");
            }

            SafeFileHandle file = NativeMethods.CreateFile(path,
                NativeMethods.GenericRead | NativeMethods.GenericWrite,
                NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
                IntPtr.Zero, NativeMethods.OpenExisting,
                NativeMethods.FileAttributeNormal | NativeMethods.FileFlagOverlapped,
                IntPtr.Zero);

            if (file.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                file.Dispose();
                throw new Win32Exception(error, "Could not open the WinUSB device.");
            }

            SafeWinUsbHandle winUsb;
            if (!NativeMethods.WinUsbInitialize(file, out winUsb))
            {
                int error = Marshal.GetLastWin32Error();
                file.Dispose();
                throw new Win32Exception(error, "WinUsb_Initialize failed.");
            }

            return new WinUsbDevice(path, file, winUsb, log);
        }

        public UsbDeviceDescriptorInfo GetDeviceDescriptor()
        {
            ThrowIfDisposed();
            byte[] bytes = new byte[18];
            uint transferred;
            if (!NativeMethods.WinUsbGetDescriptor(interfaceHandle, 0x01, 0, 0,
                bytes, (uint)bytes.Length, out transferred))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "WinUsb_GetDescriptor(device) failed.");
            }
            if (transferred != bytes.Length)
            {
                throw new InvalidOperationException(string.Format(
                    "USB device descriptor was short: {0}/18 bytes.", transferred));
            }
            return new UsbDeviceDescriptorInfo(bytes);
        }

        public IList<UsbPipeInfo> GetPipes()
        {
            ThrowIfDisposed();
            UsbInterfaceDescriptor descriptor = new UsbInterfaceDescriptor();
            if (!NativeMethods.WinUsbQueryInterfaceSettings(interfaceHandle, 0,
                ref descriptor))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "WinUsb_QueryInterfaceSettings failed.");
            }

            List<UsbPipeInfo> pipes = new List<UsbPipeInfo>();
            for (byte index = 0; index < descriptor.NumberOfEndpoints; index++)
            {
                WinUsbPipeInformation pipe = new WinUsbPipeInformation();
                if (!NativeMethods.WinUsbQueryPipe(interfaceHandle, 0, index,
                    ref pipe))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(),
                        "WinUsb_QueryPipe failed.");
                }
                pipes.Add(new UsbPipeInfo(pipe));
            }
            return pipes.AsReadOnly();
        }

        public void SetPipeTimeout(byte pipeId, int timeoutMilliseconds)
        {
            ThrowIfDisposed();
            if (timeoutMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            }
            uint value = (uint)timeoutMilliseconds;
            if (!NativeMethods.WinUsbSetPipePolicy(interfaceHandle, pipeId,
                NativeMethods.PipeTransferTimeout, 4, ref value))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "WinUsb_SetPipePolicy(PIPE_TRANSFER_TIMEOUT) failed.");
            }
        }

        public void ResetPipe(byte pipeId)
        {
            ThrowIfDisposed();
            if (!NativeMethods.WinUsbResetPipe(interfaceHandle, pipeId))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "WinUsb_ResetPipe failed.");
            }
        }

        public void ControlOut(byte request, ushort value, ushort index,
            byte[] data, int timeoutMilliseconds)
        {
            ThrowIfDisposed();
            byte[] transferData = data ?? new byte[0];
            if (transferData.Length > UInt16.MaxValue)
            {
                throw new ArgumentOutOfRangeException("data");
            }

            WinUsbSetupPacket setup = new WinUsbSetupPacket();
            setup.RequestType = 0x40;
            setup.Request = request;
            setup.Value = value;
            setup.Index = index;
            setup.Length = (ushort)transferData.Length;

            log.Trace(string.Format(
                "CTRL OUT bm=40 req={0:X2} value={1:X4} index={2:X4} len={3} data={4}",
                request, value, index, transferData.Length, ToHex(transferData)));

            uint transferred = ExecuteControl(setup, transferData,
                timeoutMilliseconds);
            if (transferred != transferData.Length)
            {
                throw new InvalidOperationException(string.Format(
                    "Control transfer was short: {0}/{1} bytes.",
                    transferred, transferData.Length));
            }
        }

        public void WritePipe(byte pipeId, byte[] data, int timeoutMilliseconds)
        {
            ThrowIfDisposed();
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            log.Trace(string.Format("BULK OUT ep={0:X2} len={1} data={2}",
                pipeId, data.Length, ToHex(data)));

            uint transferred = ExecutePipe(false, pipeId, data,
                timeoutMilliseconds);
            if (transferred != data.Length)
            {
                throw new InvalidOperationException(string.Format(
                    "Bulk write was short: {0}/{1} bytes.",
                    transferred, data.Length));
            }
        }

        public void WritePipeRedacted(byte pipeId, byte[] data,
            int timeoutMilliseconds)
        {
            ThrowIfDisposed();
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            log.Trace(string.Format(
                "BULK OUT ep={0:X2} len={1} data=<redacted>",
                pipeId, data.Length));

            uint transferred = ExecutePipe(false, pipeId, data,
                timeoutMilliseconds);
            if (transferred != data.Length)
            {
                throw new InvalidOperationException(string.Format(
                    "Bulk write was short: {0}/{1} bytes.",
                    transferred, data.Length));
            }
        }

        public byte[] ReadPipe(byte pipeId, int requestedLength,
            int timeoutMilliseconds)
        {
            return ReadPipe(pipeId, requestedLength, timeoutMilliseconds,
                false);
        }

        public byte[] ReadPipeRedacted(byte pipeId, int requestedLength,
            int timeoutMilliseconds)
        {
            return ReadPipe(pipeId, requestedLength, timeoutMilliseconds,
                true);
        }

        private byte[] ReadPipe(byte pipeId, int requestedLength,
            int timeoutMilliseconds, bool redactData)
        {
            ThrowIfDisposed();
            if (requestedLength <= 0)
            {
                throw new ArgumentOutOfRangeException("requestedLength");
            }

            byte[] buffer = new byte[requestedLength];
            uint transferred = ExecutePipe(true, pipeId, buffer,
                timeoutMilliseconds);
            byte[] result = new byte[transferred];
            Buffer.BlockCopy(buffer, 0, result, 0, (int)transferred);
            log.Trace(redactData
                ? string.Format(
                    "BULK IN ep={0:X2} requested={1} actual={2} " +
                    "data=<redacted>", pipeId, requestedLength, transferred)
                : string.Format(
                    "BULK IN ep={0:X2} requested={1} actual={2} data={3}",
                    pipeId, requestedLength, transferred, ToHex(result)));
            return result;
        }

        private uint ExecuteControl(WinUsbSetupPacket setup, byte[] data,
            int timeoutMilliseconds)
        {
            GCHandle pin = default(GCHandle);
            try
            {
                IntPtr buffer = IntPtr.Zero;
                if (data.Length != 0)
                {
                    pin = GCHandle.Alloc(data, GCHandleType.Pinned);
                    buffer = pin.AddrOfPinnedObject();
                }

                using (SafeWaitHandle waitHandle = NativeMethods.CreateEvent(
                    IntPtr.Zero, true, false, null))
                {
                    if (waitHandle.IsInvalid)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(),
                            "CreateEvent failed.");
                    }
                    OverlappedData overlapped = CreateOverlapped(waitHandle);
                    uint immediate;
                    bool completed = NativeMethods.WinUsbControlTransfer(
                        interfaceHandle, setup, buffer, (uint)data.Length,
                        out immediate, ref overlapped);
                    return CompleteOverlapped(completed, immediate,
                        ref overlapped, waitHandle, timeoutMilliseconds,
                        "WinUsb_ControlTransfer");
                }
            }
            finally
            {
                if (pin.IsAllocated)
                {
                    pin.Free();
                }
            }
        }

        private uint ExecutePipe(bool read, byte pipeId, byte[] data,
            int timeoutMilliseconds)
        {
            GCHandle pin = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                using (SafeWaitHandle waitHandle = NativeMethods.CreateEvent(
                    IntPtr.Zero, true, false, null))
                {
                    if (waitHandle.IsInvalid)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(),
                            "CreateEvent failed.");
                    }
                    OverlappedData overlapped = CreateOverlapped(waitHandle);
                    uint immediate;
                    bool completed;
                    if (read)
                    {
                        completed = NativeMethods.WinUsbReadPipe(interfaceHandle,
                            pipeId, pin.AddrOfPinnedObject(), (uint)data.Length,
                            out immediate, ref overlapped);
                    }
                    else
                    {
                        completed = NativeMethods.WinUsbWritePipe(interfaceHandle,
                            pipeId, pin.AddrOfPinnedObject(), (uint)data.Length,
                            out immediate, ref overlapped);
                    }

                    return CompleteOverlapped(completed, immediate,
                        ref overlapped, waitHandle, timeoutMilliseconds,
                        read ? "WinUsb_ReadPipe" : "WinUsb_WritePipe");
                }
            }
            finally
            {
                pin.Free();
            }
        }

        private uint CompleteOverlapped(bool completed, uint immediate,
            ref OverlappedData overlapped, SafeWaitHandle waitHandle,
            int timeoutMilliseconds, string operation)
        {
            if (timeoutMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            }
            if (completed)
            {
                return immediate;
            }

            int error = Marshal.GetLastWin32Error();
            if (error != NativeMethods.ErrorIoPending)
            {
                throw new Win32Exception(error, operation + " failed.");
            }

            uint wait = NativeMethods.WaitForSingleObject(waitHandle,
                (uint)timeoutMilliseconds);
            if (wait == NativeMethods.WaitTimeout)
            {
                NativeMethods.CancelIoEx(fileHandle, ref overlapped);
                uint ignored;
                NativeMethods.GetOverlappedResult(fileHandle, ref overlapped,
                    out ignored, true);
                throw new TimeoutException(string.Format(
                    "{0} timed out after {1} ms.", operation, timeoutMilliseconds));
            }
            if (wait == NativeMethods.WaitFailed)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "WaitForSingleObject failed for " + operation + ".");
            }
            if (wait != NativeMethods.WaitObject0)
            {
                throw new InvalidOperationException(string.Format(
                    "Unexpected wait result 0x{0:X8} for {1}.", wait, operation));
            }

            uint transferred;
            if (!NativeMethods.GetOverlappedResult(fileHandle, ref overlapped,
                out transferred, false))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    operation + " completion failed.");
            }
            return transferred;
        }

        private static OverlappedData CreateOverlapped(SafeWaitHandle waitHandle)
        {
            OverlappedData value = new OverlappedData();
            value.EventHandle = waitHandle.DangerousGetHandle();
            return value;
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return "<empty>";
            }
            return BitConverter.ToString(bytes).Replace('-', ' ');
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("WinUsbDevice");
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                interfaceHandle.Dispose();
                fileHandle.Dispose();
                disposed = true;
            }
        }
    }
}
