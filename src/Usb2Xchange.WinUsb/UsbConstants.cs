// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;

namespace Usb2Xchange.WinUsb
{
    public static class UsbConstants
    {
        public const ushort VendorId = 0x03F3;
        public const ushort LoaderProductId = 0x2002;
        public const ushort OperationalProductId = 0x2003;
        public const ushort Usb2CpuControlAddress = 0xE600;

        public static readonly Guid DeviceInterfaceGuid =
            new Guid("86A64B6A-BC77-49D2-B378-0F43E5DAA568");
    }

    public interface IUsbLog
    {
        void Info(string message);
        void Trace(string message);
        void Warning(string message);
    }

    internal sealed class NullUsbLog : IUsbLog
    {
        public void Info(string message) { }
        public void Trace(string message) { }
        public void Warning(string message) { }
    }
}
