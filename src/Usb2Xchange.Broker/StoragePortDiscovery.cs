// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Usb2Xchange.Broker
{
    internal sealed class StoragePortDevice
    {
        internal StoragePortDevice(string devicePath, string instanceId)
        {
            DevicePath = devicePath;
            InstanceId = instanceId;
        }

        internal string DevicePath { get; private set; }
        internal string InstanceId { get; private set; }
    }

    internal static class StoragePortDiscovery
    {
        private const uint DigcfPresent = 0x00000002;
        private const uint DigcfDeviceInterface = 0x00000010;
        private const int ErrorNoMoreItems = 259;
        private const int ErrorInsufficientBuffer = 122;
        private const uint SpdrpHardwareId = 0x00000001;
        private const uint SpdrpService = 0x00000004;
        private const string InstancePrefix =
            "ROOT\\SCSIADAPTER\\";
        private const string ExpectedHardwareId =
            "ROOT\\USB2XCHANGEVMINIPORT";
        private const string ExpectedService = "usb2xchange-vminiport";

        // GUID_DEVINTERFACE_STORAGEPORT from ntddstor.h.
        private static readonly Guid StoragePortInterface = new Guid(
            "2accfe60-c130-11d2-b082-00a0c91efb8b");

        internal static bool IsUsb2XchangeDevice(string instanceId,
            string serviceName, string[] hardwareIds)
        {
            if (instanceId == null || serviceName == null ||
                hardwareIds == null || !instanceId.StartsWith(
                    InstancePrefix, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(serviceName, ExpectedService,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            foreach (string hardwareId in hardwareIds)
            {
                if (string.Equals(hardwareId, ExpectedHardwareId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        internal static IList<StoragePortDevice> FindUsb2XchangeAdapters()
        {
            List<StoragePortDevice> result = new List<StoragePortDevice>();
            Guid storagePortInterface = StoragePortInterface;
            IntPtr deviceInfoSet = SetupDiGetClassDevs(
                ref storagePortInterface, null, IntPtr.Zero,
                DigcfPresent | DigcfDeviceInterface);
            if (deviceInfoSet == new IntPtr(-1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Could not enumerate StoragePort interfaces.");
            }

            try
            {
                uint index = 0;
                while (true)
                {
                    SpDeviceInterfaceData interfaceData =
                        new SpDeviceInterfaceData();
                    interfaceData.Size = Marshal.SizeOf(
                        typeof(SpDeviceInterfaceData));
                    if (!SetupDiEnumDeviceInterfaces(deviceInfoSet,
                            IntPtr.Zero, ref storagePortInterface, index,
                            ref interfaceData))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == ErrorNoMoreItems)
                        {
                            break;
                        }
                        throw new Win32Exception(error,
                            "Could not enumerate a StoragePort interface.");
                    }
                    index++;

                    SpDevinfoData deviceInfo = new SpDevinfoData();
                    deviceInfo.Size = Marshal.SizeOf(typeof(SpDevinfoData));
                    uint requiredSize;
                    SetupDiGetDeviceInterfaceDetail(deviceInfoSet,
                        ref interfaceData, IntPtr.Zero, 0, out requiredSize,
                        ref deviceInfo);
                    int detailError = Marshal.GetLastWin32Error();
                    if (requiredSize == 0 ||
                        detailError != ErrorInsufficientBuffer)
                    {
                        throw new Win32Exception(detailError,
                            "Could not size a StoragePort interface path.");
                    }

                    IntPtr detail = Marshal.AllocHGlobal(
                        checked((int)requiredSize));
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(deviceInfoSet,
                                ref interfaceData, detail, requiredSize,
                                out requiredSize, ref deviceInfo))
                        {
                            throw new Win32Exception(
                                Marshal.GetLastWin32Error(),
                                "Could not read a StoragePort interface path.");
                        }

                        string devicePath = Marshal.PtrToStringUni(
                            IntPtr.Add(detail, 4));
                        StringBuilder instanceId = new StringBuilder(512);
                        uint instanceRequired;
                        if (!SetupDiGetDeviceInstanceId(deviceInfoSet,
                                ref deviceInfo, instanceId,
                                instanceId.Capacity, out instanceRequired))
                        {
                            throw new Win32Exception(
                                Marshal.GetLastWin32Error(),
                                "Could not read a StoragePort instance ID.");
                        }
                        string serviceName;
                        string[] hardwareIds;
                        if (TryReadStringProperty(deviceInfoSet,
                                ref deviceInfo, SpdrpService,
                                out serviceName) &&
                            TryReadMultiStringProperty(deviceInfoSet,
                                ref deviceInfo, SpdrpHardwareId,
                                out hardwareIds) &&
                            IsUsb2XchangeDevice(instanceId.ToString(),
                                serviceName, hardwareIds))
                        {
                            result.Add(new StoragePortDevice(devicePath,
                                instanceId.ToString()));
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detail);
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }
            return result;
        }

        private static bool TryReadProperty(IntPtr deviceInfoSet,
            ref SpDevinfoData deviceInfo, uint property, out byte[] value,
            out uint requiredSize)
        {
            byte[] buffer = new byte[4096];
            uint dataType;
            if (!SetupDiGetDeviceRegistryProperty(deviceInfoSet,
                    ref deviceInfo, property, out dataType, buffer,
                    (uint)buffer.Length, out requiredSize) ||
                requiredSize == 0 || requiredSize > buffer.Length)
            {
                value = null;
                return false;
            }
            value = buffer;
            return true;
        }

        private static bool TryReadStringProperty(IntPtr deviceInfoSet,
            ref SpDevinfoData deviceInfo, uint property, out string value)
        {
            byte[] buffer;
            uint requiredSize;
            if (!TryReadProperty(deviceInfoSet, ref deviceInfo, property,
                    out buffer, out requiredSize))
            {
                value = null;
                return false;
            }
            value = Encoding.Unicode.GetString(buffer, 0,
                checked((int)requiredSize)).TrimEnd('\0');
            return value.Length != 0;
        }

        private static bool TryReadMultiStringProperty(IntPtr deviceInfoSet,
            ref SpDevinfoData deviceInfo, uint property,
            out string[] values)
        {
            string combined;
            if (!TryReadStringProperty(deviceInfoSet, ref deviceInfo,
                    property, out combined))
            {
                values = null;
                return false;
            }
            values = combined.Split(new char[] { '\0' },
                StringSplitOptions.RemoveEmptyEntries);
            return values.Length != 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SpDeviceInterfaceData
        {
            internal int Size;
            internal Guid InterfaceClassGuid;
            internal int Flags;
            internal IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SpDevinfoData
        {
            internal int Size;
            internal Guid ClassGuid;
            internal uint DevInst;
            internal IntPtr Reserved;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid, string enumerator, IntPtr parent,
            uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet, IntPtr deviceInfoData,
            ref Guid interfaceClassGuid, uint memberIndex,
            ref SpDeviceInterfaceData deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet,
            ref SpDeviceInterfaceData deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            uint deviceInterfaceDetailDataSize, out uint requiredSize,
            ref SpDevinfoData deviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiGetDeviceInstanceId(
            IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData,
            StringBuilder deviceInstanceId, int deviceInstanceIdSize,
            out uint requiredSize);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData,
            uint property, out uint propertyRegDataType,
            [Out] byte[] propertyBuffer, uint propertyBufferSize,
            out uint requiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiDestroyDeviceInfoList(
            IntPtr deviceInfoSet);
    }
}
