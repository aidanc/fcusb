// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Runtime.InteropServices;
using Usb2Xchange.AspiShim;
using Usb2Xchange.Protocol;

namespace Usb2Xchange.AspiProbe
{
    internal static class Program
    {
        private static int Main(string[] arguments)
        {
            try
            {
                if (arguments.Length != 1 ||
                    (arguments[0] != "disabled" &&
                     arguments[0] != "offline-replay" &&
                     arguments[0] != "--approve-live-read-only"))
                {
                    Console.Error.WriteLine(
                        "Usage: Usb2Xchange.AspiProbe.exe " +
                        "<disabled|offline-replay|--approve-live-read-only>");
                    return 2;
                }
                Environment.SetEnvironmentVariable(
                    "USB2XCHANGE_ASPI_TRANSPORT",
                    arguments[0] == "disabled" ? null :
                    arguments[0] == "offline-replay" ? "offline-replay" :
                        "live-read-only", EnvironmentVariableTarget.Process);
                uint support = GetASPI32SupportInfo();
                Console.WriteLine("ASPI support: status={0:X2}, adapters={1}",
                    (support >> 8) & 0xFF, support & 0xFF);
                if (arguments[0] == "disabled")
                {
                    return support == 0x0100 ? 0 : 2;
                }
                if (support != 0x0101)
                {
                    return 2;
                }

                bool found = false;
                for (byte target = 0; target <= 6; target++)
                {
                    byte deviceType;
                    byte status = GetDeviceTypeForTarget(target, out deviceType);
                    if (status == AspiConstants.StatusComplete)
                    {
                        found = true;
                        InquiryData inquiry = InquiryTarget(target);
                        Console.WriteLine(
                            "Target {0}: type=0x{1:X2}, vendor='{2}', product='{3}', revision='{4}'",
                            target, inquiry.PeripheralDeviceType, inquiry.Vendor,
                            inquiry.Product, inquiry.Revision);
                    }
                    else
                    {
                        Console.WriteLine("Target {0}: ASPI status 0x{1:X2}",
                            target, status);
                    }
                }

                if (!found)
                {
                    Console.Error.WriteLine("No SCSI targets responded to read-only INQUIRY.");
                    return 3;
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static byte GetDeviceTypeForTarget(byte target,
            out byte deviceType)
        {
            IntPtr srb = AllocateZeroed(16);
            try
            {
                Marshal.WriteByte(srb, AspiConstants.CommandOffset,
                    AspiConstants.GetDeviceType);
                Marshal.WriteByte(srb, AspiConstants.TargetOffset, target);
                uint result = SendASPI32Command(srb);
                deviceType = Marshal.ReadByte(srb, 0x0A);
                return checked((byte)result);
            }
            finally
            {
                Marshal.FreeHGlobal(srb);
            }
        }

        private static InquiryData InquiryTarget(byte target)
        {
            const int length = 36;
            IntPtr buffer = AllocateZeroed(length);
            IntPtr srb = AllocateZeroed(96);
            try
            {
                Marshal.WriteByte(srb, AspiConstants.CommandOffset,
                    AspiConstants.ExecuteScsiCommand);
                Marshal.WriteByte(srb, AspiConstants.FlagsOffset,
                    AspiConstants.FlagDirectionIn);
                Marshal.WriteByte(srb, AspiConstants.TargetOffset, target);
                Marshal.WriteInt32(srb, AspiConstants.BufferLengthOffset,
                    length);
                Marshal.WriteInt32(srb, AspiConstants.BufferPointerOffset,
                    buffer.ToInt32());
                Marshal.WriteByte(srb, AspiConstants.SenseLengthOffset, 18);
                Marshal.WriteByte(srb, AspiConstants.CdbLengthOffset, 6);
                byte[] cdb = ScsiFraming.BuildInquiryCdb(length);
                Marshal.Copy(cdb, 0,
                    IntPtr.Add(srb, AspiConstants.CdbOffset), cdb.Length);

                uint status = SendASPI32Command(srb);
                if (status != AspiConstants.StatusComplete)
                {
                    throw new InvalidOperationException(string.Format(
                        "Target {0} INQUIRY failed: SRB=0x{1:X2}, HA=0x{2:X2}, target=0x{3:X2}.",
                        target, status,
                        Marshal.ReadByte(srb, AspiConstants.HostStatusOffset),
                        Marshal.ReadByte(srb, AspiConstants.TargetStatusOffset)));
                }

                byte[] data = new byte[length];
                Marshal.Copy(buffer, data, 0, data.Length);
                return InquiryData.Parse(data);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                Marshal.FreeHGlobal(srb);
            }
        }

        private static IntPtr AllocateZeroed(int length)
        {
            IntPtr pointer = Marshal.AllocHGlobal(length);
            for (int i = 0; i < length; i++)
            {
                Marshal.WriteByte(pointer, i, 0);
            }
            return pointer;
        }

        [DllImport("wnaspi32.dll", EntryPoint = "GetASPI32SupportInfo",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint GetASPI32SupportInfo();

        [DllImport("wnaspi32.dll", EntryPoint = "SendASPI32Command",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SendASPI32Command(IntPtr srb);
    }
}
