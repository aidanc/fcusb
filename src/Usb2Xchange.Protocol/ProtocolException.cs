// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;

namespace Usb2Xchange.Protocol
{
    public class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message)
        {
        }

        public ProtocolException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class ScsiSelectionTimeoutException : ProtocolException
    {
        public ScsiSelectionTimeoutException(string operation, byte target, byte lun)
            : base(string.Format(
                "{0} could not select target {1}, LUN {2} (adapter status 8A).",
                operation, target, lun))
        {
            Target = target;
            Lun = lun;
        }

        public byte Target { get; private set; }
        public byte Lun { get; private set; }
    }
}
