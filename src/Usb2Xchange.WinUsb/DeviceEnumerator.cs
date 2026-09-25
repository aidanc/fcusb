// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Usb2Xchange.WinUsb
{
    public static class DeviceEnumerator
    {
        public static IList<string> FindDevicePaths()
        {
            Guid guid = UsbConstants.DeviceInterfaceGuid;
            IntPtr deviceInfoSet = NativeMethods.SetupDiGetClassDevs(
                ref guid, IntPtr.Zero, IntPtr.Zero,
                NativeMethods.DigcfPresent | NativeMethods.DigcfDeviceInterface);

            if (deviceInfoSet == new IntPtr(-1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "SetupDiGetClassDevs failed.");
            }

            List<string> paths = new List<string>();
            try
            {
                for (uint index = 0; ; index++)
                {
                    SpDeviceInterfaceData data = new SpDeviceInterfaceData();
                    data.Size = Marshal.SizeOf(typeof(SpDeviceInterfaceData));
                    if (!NativeMethods.SetupDiEnumDeviceInterfaces(deviceInfoSet,
                        IntPtr.Zero, ref guid, index, ref data))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == NativeMethods.ErrorNoMoreItems)
                        {
                            break;
                        }
                        throw new Win32Exception(error,
                            "SetupDiEnumDeviceInterfaces failed.");
                    }

                    uint requiredSize;
                    NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet,
                        ref data, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);
                    int sizeError = Marshal.GetLastWin32Error();
                    if (requiredSize == 0 ||
                        sizeError != NativeMethods.ErrorInsufficientBuffer)
                    {
                        throw new Win32Exception(sizeError,
                            "Could not determine device-interface path size.");
                    }

                    IntPtr detail = Marshal.AllocHGlobal((int)requiredSize);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        uint ignored;
                        if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(
                            deviceInfoSet, ref data, detail, requiredSize,
                            out ignored, IntPtr.Zero))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error(),
                                "SetupDiGetDeviceInterfaceDetail failed.");
                        }

                        string path = Marshal.PtrToStringUni(
                            IntPtr.Add(detail, 4));
                        if (!string.IsNullOrEmpty(path))
                        {
                            paths.Add(path);
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
                NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return paths.AsReadOnly();
        }
    }
}
