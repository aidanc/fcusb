// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
namespace Usb2Xchange.AspiShim
{
    public static class AspiConstants
    {
        public const byte HostAdapterInquiry = 0x00;
        public const byte GetDeviceType = 0x01;
        public const byte ExecuteScsiCommand = 0x02;

        public const byte StatusPending = 0x00;
        public const byte StatusComplete = 0x01;
        public const byte StatusError = 0x04;
        public const byte StatusInvalidCommand = 0x80;
        public const byte StatusInvalidHostAdapter = 0x81;
        public const byte StatusNoDevice = 0x82;
        public const byte StatusInvalidSrb = 0xE0;

        public const byte FlagDirectionIn = 0x08;
        public const byte FlagDirectionOut = 0x10;
        public const byte FlagEventNotify = 0x40;
        public const byte FlagResidualCount = 0x04;

        public const byte HostStatusOk = 0x00;
        public const byte HostStatusSelectionTimeout = 0x11;
        public const byte TargetStatusGood = 0x00;
        public const byte TargetStatusCheckCondition = 0x02;
        public const byte TargetStatusBusy = 0x08;

        public const int CommandOffset = 0x00;
        public const int StatusOffset = 0x01;
        public const int HostAdapterOffset = 0x02;
        public const int FlagsOffset = 0x03;
        public const int TargetOffset = 0x08;
        public const int LunOffset = 0x09;
        public const int BufferLengthOffset = 0x0C;
        public const int BufferPointerOffset = 0x10;
        public const int SenseLengthOffset = 0x14;
        public const int CdbLengthOffset = 0x15;
        public const int HostStatusOffset = 0x16;
        public const int TargetStatusOffset = 0x17;
        public const int PostProcedureOffset = 0x18;
        public const int CdbOffset = 0x30;
        public const int SenseAreaOffset = 0x40;

        public const int MaximumTransferLength = 4096;
        public const int MaximumOfflineReplayTransferLength = 65536;
        public const int MaximumTrustedTransferLength = 16 * 1024 * 1024;
        public const int MaximumSenseLength = 18;
    }
}
