// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using Usb2Xchange.Protocol;
using Usb2Xchange.WinUsb;

namespace Usb2Xchange.Broker
{
    internal sealed class WinUsbScsiTransport : IReadOnlyScsiTransport
    {
        private readonly Usb2XchangeDevice device;

        internal WinUsbScsiTransport(Usb2XchangeDevice device)
        {
            if (device == null)
            {
                throw new ArgumentNullException("device");
            }
            this.device = device;
        }

        public RawScsiResult Execute(byte target, byte lun, byte[] cdb,
            uint requestedLength, int timeoutMilliseconds)
        {
            ScsiCommandResult result = device.ExecuteReadOnly(target, lun,
                cdb, requestedLength, timeoutMilliseconds);
            return new RawScsiResult(result.Status.RawStatus,
                result.Status.RequestedLength, result.Status.Residue,
                result.Data);
        }

        public RawScsiResult ExecutePrecisionTwoLoaderReadBufferD8Once()
        {
            ScsiCommandResult result =
                device.ReadPrecisionTwoLoaderBufferD8Once();
            return new RawScsiResult(result.Status.RawStatus,
                result.Status.RequestedLength, result.Status.Residue,
                result.Data);
        }

        public RawScsiResult ExecutePrecisionTwoOperationalReadBufferD8Once()
        {
            ScsiCommandResult result =
                device.ReadPrecisionTwoOperationalBufferD8Once();
            return new RawScsiResult(result.Status.RawStatus,
                result.Status.RequestedLength, result.Status.Residue,
                result.Data);
        }

        public RawScsiResult ExecutePrecisionTwoOperationalScannerReadyOnce()
        {
            ScsiCommandResult result =
                device.ReadPrecisionTwoOperationalScannerReadyOnce();
            return new RawScsiResult(result.Status.RawStatus,
                result.Status.RequestedLength, result.Status.Residue,
                result.Data);
        }

        public RawScsiResult
            ExecutePrecisionTwoOperationalFaultPixelReadBufferOnce()
        {
            ScsiCommandResult result =
                device.ReadPrecisionTwoOperationalFaultPixelBufferOnce();
            return new RawScsiResult(result.Status.RawStatus,
                result.Status.RequestedLength, result.Status.Residue,
                result.Data);
        }

        public RawScsiResult
            ExecutePrecisionTwoOperationalFaultPixelDataReadBufferOnce()
        {
            ScsiCommandResult result =
                device.ReadPrecisionTwoOperationalFaultPixelDataBufferOnce();
            return new RawScsiResult(result.Status.RawStatus,
                result.Status.RequestedLength, result.Status.Residue,
                result.Data);
        }

        public RawScsiResult
            ExecutePrecisionTwoOperationalCalibrationReadBufferOnce()
        {
            ScsiCommandResult result =
                device.ReadPrecisionTwoOperationalCalibrationBufferOnce();
            return new RawScsiResult(result.Status.RawStatus,
                result.Status.RequestedLength, result.Status.Residue,
                result.Data);
        }

        public RawScsiResult
            ExecutePrecisionTwoOperationalD8Offset55ReadBufferOnce()
        {
            ScsiCommandResult result =
                device.ReadPrecisionTwoOperationalD8Offset55BufferOnce();
            return new RawScsiResult(result.Status.RawStatus,
                result.Status.RequestedLength, result.Status.Residue,
                result.Data);
        }

        public RawScsiResult
            ExecutePrecisionTwoOperationalPreviewSetWindowOnce(byte[] data)
        {
            ScsiCommandResult result = device.
                ExecutePrecisionTwoOperationalPreviewSetWindowOnce(data);
            return RawScsiResult.ForDataOut(result.Status.RawStatus,
                result.Status.RequestedLength, result.Status.Residue);
        }

        public RawScsiResult
            ExecutePrecisionTwoLoaderWriteBufferModeOneZeroOnce()
        {
            ScsiCommandResult result = device.
                ExecutePrecisionTwoLoaderWriteBufferModeOneZeroOnce();
            return new RawScsiResult(result.Status.RawStatus,
                result.Status.RequestedLength, result.Status.Residue,
                result.Data);
        }

        public RawScsiResult
            ExecutePrecisionTwoLoaderWriteBufferRecordOnce(byte[] record,
                int recordIndex)
        {
            ScsiCommandResult result = device.
                ExecutePrecisionTwoLoaderWriteBufferRecordOnce(record,
                    recordIndex);
            return RawScsiResult.ForDataOut(result.Status.RawStatus,
                result.Status.RequestedLength, result.Status.Residue);
        }

        public RawScsiResult
            ExecutePrecisionTwoLoaderWriteBufferTerminalOnce(byte[] record)
        {
            ScsiCommandResult result = device.
                ExecutePrecisionTwoLoaderWriteBufferTerminalOnce(record);
            return RawScsiResult.ForDataOut(result.Status.RawStatus,
                result.Status.RequestedLength, result.Status.Residue);
        }
    }

    internal sealed class BrokerUsbLog : IUsbLog
    {
        public void Info(string message)
        {
            Write("INFO", message);
        }

        public void Trace(string message)
        {
            Write("USB", message);
        }

        public void Warning(string message)
        {
            Write("WARN", message);
        }

        private static void Write(string level, string message)
        {
            Console.WriteLine("{0:O} [{1}] {2}", DateTime.UtcNow, level,
                message);
        }
    }
}
