// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Globalization;
using System.IO;
using Usb2Xchange.WinUsb;

namespace Usb2Xchange.AspiShim
{
    internal sealed class AspiFileLog : IUsbLog
    {
        private static readonly object Sync = new object();
        private readonly string path;
        private readonly string fallbackPath;

        public AspiFileLog()
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Usb2Xchange");
            string configured = Environment.GetEnvironmentVariable(
                "USB2XCHANGE_ASPI_LOG_PATH");
            fallbackPath = Path.Combine(Path.GetTempPath(),
                "usb2xchange-aspi-shim.log");
            try
            {
                path = string.IsNullOrWhiteSpace(configured)
                    ? Path.Combine(directory, "aspi-shim.log")
                    : Path.GetFullPath(configured);
            }
            catch
            {
                path = fallbackPath;
            }
        }

        public void Info(string message)
        {
            Write("INFO", message);
        }

        public void Trace(string message)
        {
            // WinUSB trace messages contain raw transfer payloads. The ASPI
            // compatibility log intentionally records metadata only.
        }

        public void Warning(string message)
        {
            Write("WARN", message);
        }

        private void Write(string level, string message)
        {
            try
            {
                lock (Sync)
                {
                    string directory = Path.GetDirectoryName(path);
                    Directory.CreateDirectory(directory);
                    File.AppendAllText(path, string.Format(CultureInfo.InvariantCulture,
                        "{0:u} {1} {2}{3}", DateTime.UtcNow, level, message,
                        Environment.NewLine));
                }
            }
            catch
            {
                try
                {
                    lock (Sync)
                    {
                        File.AppendAllText(fallbackPath,
                            DateTime.UtcNow.ToString("u",
                                CultureInfo.InvariantCulture) + " " + level +
                            " " + message + Environment.NewLine);
                    }
                }
                catch
                {
                    // Logging must never break the host application.
                }
            }
        }
    }
}
