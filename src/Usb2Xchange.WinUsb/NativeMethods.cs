// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Usb2Xchange.WinUsb
{
    internal enum UsbPipeType
    {
        Control = 0,
        Isochronous = 1,
        Bulk = 2,
        Interrupt = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SpDeviceInterfaceData
    {
        public int Size;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct WinUsbSetupPacket
    {
        public byte RequestType;
        public byte Request;
        public ushort Value;
        public ushort Index;
        public ushort Length;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct UsbInterfaceDescriptor
    {
        public byte Length;
        public byte DescriptorType;
        public byte InterfaceNumber;
        public byte AlternateSetting;
        public byte NumberOfEndpoints;
        public byte InterfaceClass;
        public byte InterfaceSubClass;
        public byte InterfaceProtocol;
        public byte Interface;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WinUsbPipeInformation
    {
        public UsbPipeType PipeType;
        public byte PipeId;
        public ushort MaximumPacketSize;
        public byte Interval;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OverlappedData
    {
        public IntPtr Internal;
        public IntPtr InternalHigh;
        public uint Offset;
        public uint OffsetHigh;
        public IntPtr EventHandle;
    }

    internal sealed class SafeWinUsbHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeWinUsbHandle() : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.WinUsbFree(handle);
        }
    }

    internal static class NativeMethods
    {
        static NativeMethods()
        {
            if (IntPtr.Size != 4 && IntPtr.Size != 8)
            {
                throw new PlatformNotSupportedException(
                    "WinUSB transport requires a 32-bit or 64-bit Windows process.");
            }
            RequireSize(typeof(WinUsbSetupPacket), 8);
            RequireSize(typeof(UsbInterfaceDescriptor), 9);
            RequireSize(typeof(WinUsbPipeInformation), 12);
            RequireSize(typeof(SpDeviceInterfaceData), IntPtr.Size == 8 ? 32 : 28);
            RequireSize(typeof(OverlappedData), IntPtr.Size == 8 ? 32 : 20);
        }

        internal const uint DigcfPresent = 0x00000002;
        internal const uint DigcfDeviceInterface = 0x00000010;
        internal const int ErrorNoMoreItems = 259;
        internal const int ErrorInsufficientBuffer = 122;
        internal const int ErrorIoPending = 997;
        internal const uint WaitObject0 = 0;
        internal const uint WaitTimeout = 258;
        internal const uint WaitFailed = 0xFFFFFFFF;
        internal const uint GenericRead = 0x80000000;
        internal const uint GenericWrite = 0x40000000;
        internal const uint FileShareRead = 0x00000001;
        internal const uint FileShareWrite = 0x00000002;
        internal const uint OpenExisting = 3;
        internal const uint FileAttributeNormal = 0x00000080;
        internal const uint FileFlagOverlapped = 0x40000000;
        internal const uint PipeTransferTimeout = 3;

        private static void RequireSize(Type type, int expected)
        {
            int actual = Marshal.SizeOf(type);
            if (actual != expected)
            {
                throw new TypeLoadException(string.Format(
                    "Native layout {0} is {1} bytes; expected {2}.",
                    type.Name, actual, expected));
            }
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid, IntPtr enumerator, IntPtr parentWindow, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid,
            uint memberIndex, ref SpDeviceInterfaceData interfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet, ref SpDeviceInterfaceData interfaceData,
            IntPtr detailData, uint detailDataSize, out uint requiredSize,
            IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(
            string fileName, uint desiredAccess, uint shareMode,
            IntPtr securityAttributes, uint creationDisposition,
            uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeWaitHandle CreateEvent(
            IntPtr eventAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool manualReset,
            [MarshalAs(UnmanagedType.Bool)] bool initialState,
            string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint WaitForSingleObject(SafeWaitHandle handle,
            uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetOverlappedResult(SafeFileHandle fileHandle,
            ref OverlappedData overlapped, out uint transferred,
            [MarshalAs(UnmanagedType.Bool)] bool wait);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CancelIoEx(SafeFileHandle fileHandle,
            ref OverlappedData overlapped);

        [DllImport("winusb.dll", EntryPoint = "WinUsb_Initialize", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WinUsbInitialize(SafeFileHandle deviceHandle,
            out SafeWinUsbHandle interfaceHandle);

        [DllImport("winusb.dll", EntryPoint = "WinUsb_Free", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WinUsbFree(IntPtr interfaceHandle);

        [DllImport("winusb.dll", EntryPoint = "WinUsb_GetDescriptor", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WinUsbGetDescriptor(
            SafeWinUsbHandle interfaceHandle, byte descriptorType, byte index,
            ushort languageId, [Out] byte[] buffer, uint bufferLength,
            out uint lengthTransferred);

        [DllImport("winusb.dll", EntryPoint = "WinUsb_QueryInterfaceSettings", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WinUsbQueryInterfaceSettings(
            SafeWinUsbHandle interfaceHandle, byte alternateSetting,
            ref UsbInterfaceDescriptor descriptor);

        [DllImport("winusb.dll", EntryPoint = "WinUsb_QueryPipe", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WinUsbQueryPipe(SafeWinUsbHandle interfaceHandle,
            byte alternateSetting, byte pipeIndex, ref WinUsbPipeInformation pipe);

        [DllImport("winusb.dll", EntryPoint = "WinUsb_SetPipePolicy", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WinUsbSetPipePolicy(SafeWinUsbHandle interfaceHandle,
            byte pipeId, uint policyType, uint valueLength, ref uint value);

        [DllImport("winusb.dll", EntryPoint = "WinUsb_ResetPipe", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WinUsbResetPipe(SafeWinUsbHandle interfaceHandle,
            byte pipeId);

        [DllImport("winusb.dll", EntryPoint = "WinUsb_ControlTransfer", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WinUsbControlTransfer(
            SafeWinUsbHandle interfaceHandle, WinUsbSetupPacket setupPacket,
            IntPtr buffer, uint bufferLength, out uint lengthTransferred,
            ref OverlappedData overlapped);

        [DllImport("winusb.dll", EntryPoint = "WinUsb_ReadPipe", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WinUsbReadPipe(SafeWinUsbHandle interfaceHandle,
            byte pipeId, IntPtr buffer, uint bufferLength,
            out uint lengthTransferred, ref OverlappedData overlapped);

        [DllImport("winusb.dll", EntryPoint = "WinUsb_WritePipe", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WinUsbWritePipe(SafeWinUsbHandle interfaceHandle,
            byte pipeId, IntPtr buffer, uint bufferLength,
            out uint lengthTransferred, ref OverlappedData overlapped);
    }
}
