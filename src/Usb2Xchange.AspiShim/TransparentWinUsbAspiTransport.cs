// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using Usb2Xchange.Protocol;
using Usb2Xchange.WinUsb;

namespace Usb2Xchange.AspiShim
{
    internal sealed class TransparentWinUsbAspiTransport :
        IAspiTransparentPassThroughTransport, IDisposable
    {
        internal const int TransferTimeoutMilliseconds = 120000;

        private readonly object sync = new object();
        private readonly IUsbLog log;
        private Usb2XchangeDevice device;
        private bool disposed;

        internal TransparentWinUsbAspiTransport(IUsbLog log)
        {
            this.log = log ?? new NullUsbLog();
        }

        public AspiTransportResult Execute(byte target, byte lun, byte[] cdb,
            uint requestedLength)
        {
            lock (sync)
            {
                RequireNotDisposed();
                if (cdb == null)
                {
                    throw new ArgumentNullException("cdb");
                }
                if (target != 5 || lun != 0)
                {
                    return SelectionTimeout(requestedLength);
                }

                EnsureConnected();
                try
                {
                    DataDirection direction = requestedLength == 0
                        ? DataDirection.None
                        : DataDirection.In;
                    ScsiCommandResult result = device.ExecutePassThrough(
                        target, lun, cdb, direction, requestedLength, null,
                        TransferTimeoutMilliseconds);
                    return Convert(result);
                }
                catch
                {
                    DisposeDevice();
                    throw;
                }
            }
        }

        public AspiTransportResult ExecuteDataOut(byte target, byte lun,
            byte[] cdb, byte[] data)
        {
            lock (sync)
            {
                RequireNotDisposed();
                if (cdb == null)
                {
                    throw new ArgumentNullException("cdb");
                }
                if (data == null || data.Length == 0)
                {
                    throw new ArgumentException(
                        "A data-out request requires a nonempty payload.",
                        "data");
                }
                if (target != 5 || lun != 0)
                {
                    return SelectionTimeout(checked((uint)data.Length));
                }

                EnsureConnected();
                try
                {
                    ScsiCommandResult result = device.ExecutePassThrough(
                        target, lun, cdb, DataDirection.Out,
                        checked((uint)data.Length), data,
                        TransferTimeoutMilliseconds);
                    return Convert(result);
                }
                catch
                {
                    DisposeDevice();
                    throw;
                }
            }
        }

        private void EnsureConnected()
        {
            if (device != null)
            {
                return;
            }
            device = Usb2XchangeDevice.Find(UsbConstants.OperationalProductId,
                log, TransferTimeoutMilliseconds);
            if (device == null)
            {
                throw new InvalidOperationException(
                    "USB2Xchange operational PID 2003 was not found. " +
                    "Initialize adapter firmware before launching FlexColor.");
            }
            try
            {
                device.InitializeOperational();
            }
            catch
            {
                DisposeDevice();
                throw;
            }
        }

        private static AspiTransportResult Convert(ScsiCommandResult result)
        {
            return new AspiTransportResult(result.Data,
                result.Status.RawStatus, result.Status.RequestedLength,
                result.Status.Residue);
        }

        private static AspiTransportResult SelectionTimeout(
            uint requestedLength)
        {
            return new AspiTransportResult(new byte[0],
                (byte)AdapterStatus.SelectionTimeout, requestedLength,
                requestedLength);
        }

        private void RequireNotDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    "TransparentWinUsbAspiTransport");
            }
        }

        private void DisposeDevice()
        {
            if (device != null)
            {
                device.Dispose();
                device = null;
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (!disposed)
                {
                    DisposeDevice();
                    disposed = true;
                }
            }
        }
    }
}
