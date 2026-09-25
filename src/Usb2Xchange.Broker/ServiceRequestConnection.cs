// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Usb2Xchange.BrokerProtocol;

namespace Usb2Xchange.Broker
{
    internal sealed class ServiceRequestConnection : IDisposable
    {
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint ShareRead = 0x00000001;
        private const uint ShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint FileFlagOverlapped = 0x40000000;
        private const uint IoctlMiniportProcessServiceIrp = 0x0004D038;
        private const int ErrorIoPending = 997;
        private const int ErrorNotFound = 1168;
        private const int OfflineCompletionWaitMilliseconds = 5000;

        private readonly SafeFileHandle handle;

        static ServiceRequestConnection()
        {
            int expected = IntPtr.Size == 8 ? 32 : 20;
            int actual = Marshal.SizeOf(typeof(OverlappedData));
            if (actual != expected)
            {
                throw new TypeLoadException(string.Format(
                    "OVERLAPPED layout is {0} bytes; expected {1}.",
                    actual, expected));
            }
        }

        internal static void ValidateNativeLayout()
        {
            // Invoking this method runs the static layout assertion without
            // opening a device or changing miniport state.
        }

        internal ServiceRequestConnection(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
            {
                throw new ArgumentNullException("devicePath");
            }
            handle = CreateFile(devicePath, GenericRead | GenericWrite,
                ShareRead | ShareWrite, IntPtr.Zero, OpenExisting,
                FileFlagOverlapped, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Could not open the USB2Xchange service-request interface.");
            }
        }

        internal byte[] Receive(ulong generation, int timeoutMilliseconds,
            Action beforeTimeoutCancellation)
        {
            if (timeoutMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            }
            if (beforeTimeoutCancellation == null)
            {
                throw new ArgumentNullException("beforeTimeoutCancellation");
            }

            byte[] input = BrokerWireProtocol.SerializeServiceWait(generation);
            byte[] output = new byte[BrokerWireProtocol.RequestMaximumSize];
            GCHandle inputPin = default(GCHandle);
            GCHandle outputPin = default(GCHandle);
            IntPtr overlappedPointer = IntPtr.Zero;

            using (ManualResetEvent completionEvent = new ManualResetEvent(false))
            {
                try
                {
                    inputPin = GCHandle.Alloc(input, GCHandleType.Pinned);
                    outputPin = GCHandle.Alloc(output, GCHandleType.Pinned);
                    OverlappedData overlapped = new OverlappedData();
                    overlapped.EventHandle = completionEvent.SafeWaitHandle.
                        DangerousGetHandle();
                    overlappedPointer = Marshal.AllocHGlobal(
                        Marshal.SizeOf(typeof(OverlappedData)));
                    Marshal.StructureToPtr(overlapped, overlappedPointer, false);

                    uint immediateBytes;
                    bool completed = DeviceIoControl(handle,
                        IoctlMiniportProcessServiceIrp,
                        inputPin.AddrOfPinnedObject(),
                        checked((uint)input.Length),
                        outputPin.AddrOfPinnedObject(),
                        checked((uint)output.Length), out immediateBytes,
                        overlappedPointer);
                    if (!completed)
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != ErrorIoPending)
                        {
                            throw new Win32Exception(error,
                                "The miniport rejected the service wait.");
                        }
                    }

                    if (!completed &&
                        !completionEvent.WaitOne(timeoutMilliseconds))
                    {
                        Exception offlineError = null;
                        try
                        {
                            // Advancing the miniport generation completes the
                            // retained service IRP before cancellation is tried.
                            beforeTimeoutCancellation();
                        }
                        catch (Exception ex)
                        {
                            offlineError = ex;
                        }

                        if (!completionEvent.WaitOne(
                                OfflineCompletionWaitMilliseconds))
                        {
                            CancelPending(overlappedPointer);
                            uint cancelledBytes;
                            // The OVERLAPPED structure and pinned buffers must
                            // remain alive until cancellation has completed.
                            GetOverlappedResult(handle, overlappedPointer,
                                out cancelledBytes, true);
                        }
                        if (offlineError != null)
                        {
                            throw new InvalidOperationException(
                                "The broker timed out and could not take the miniport offline.",
                                offlineError);
                        }
                    }

                    uint transferred;
                    if (!GetOverlappedResult(handle, overlappedPointer,
                            out transferred, false))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(),
                            "The USB2Xchange service wait did not complete successfully.");
                    }
                    if (transferred < BrokerWireProtocol.RequestPrefixSize ||
                        transferred > BrokerWireProtocol.RequestMaximumSize)
                    {
                        throw new BrokerProtocolException(string.Format(
                            "The miniport service wait returned an invalid {0}-byte request.",
                            transferred));
                    }
                    byte[] request = new byte[checked((int)transferred)];
                    Buffer.BlockCopy(output, 0, request, 0, request.Length);
                    return request;
                }
                finally
                {
                    if (overlappedPointer != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(overlappedPointer);
                    }
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
        }

        private void CancelPending(IntPtr overlappedPointer)
        {
            if (!CancelIoEx(handle, overlappedPointer))
            {
                int error = Marshal.GetLastWin32Error();
                if (error != ErrorNotFound)
                {
                    throw new Win32Exception(error,
                        "Could not cancel the service wait after offline failed.");
                }
            }
        }

        public void Dispose()
        {
            handle.Dispose();
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
            out uint bytesReturned, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOverlappedResult(
            SafeFileHandle fileHandle, IntPtr overlapped,
            out uint transferred,
            [MarshalAs(UnmanagedType.Bool)] bool wait);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CancelIoEx(SafeFileHandle fileHandle,
            IntPtr overlapped);
    }
}
