// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;

namespace Usb2Xchange.AspiShim
{
    internal interface IAspiOperatorCycleTransport : IAspiDataOutTransport,
        IAspiPreviewSetWindowValidationPolicy, IDisposable
    {
        bool CycleCompleted { get; }
    }

    internal sealed class AspiOperatorSessionTransport :
        IAspiDataOutTransport, IAspiPreviewSetWindowValidationPolicy,
        IDisposable
    {
        private readonly object sync = new object();
        private readonly Func<IAspiOperatorCycleTransport> cycleFactory;
        private IAspiOperatorCycleTransport cycle;
        private bool failed;
        private bool disposed;
        private int completedCycles;

        internal AspiOperatorSessionTransport(
            Func<IAspiOperatorCycleTransport> cycleFactory)
        {
            if (cycleFactory == null)
            {
                throw new ArgumentNullException("cycleFactory");
            }
            this.cycleFactory = cycleFactory;
        }

        internal int CompletedCycles
        {
            get
            {
                lock (sync)
                {
                    return completedCycles;
                }
            }
        }

        internal bool Failed
        {
            get
            {
                lock (sync)
                {
                    return failed;
                }
            }
        }

        bool IAspiPreviewSetWindowValidationPolicy.
            StatefulPreviewSetWindowValidationEnabled
        {
            get { return true; }
        }

        public AspiTransportResult Execute(byte target, byte lun, byte[] cdb,
            uint requestedLength)
        {
            lock (sync)
            {
                IAspiOperatorCycleTransport current = RequireCycle();
                try
                {
                    AspiTransportResult result = current.Execute(target, lun,
                        cdb, requestedLength);
                    RetireCompletedCycle(current);
                    return result;
                }
                catch
                {
                    FailClosed(current);
                    throw;
                }
            }
        }

        public AspiTransportResult ExecuteDataOut(byte target, byte lun,
            byte[] cdb, byte[] data)
        {
            lock (sync)
            {
                IAspiOperatorCycleTransport current = RequireCycle();
                try
                {
                    AspiTransportResult result = current.ExecuteDataOut(target,
                        lun, cdb, data);
                    RetireCompletedCycle(current);
                    return result;
                }
                catch
                {
                    FailClosed(current);
                    throw;
                }
            }
        }

        private IAspiOperatorCycleTransport RequireCycle()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    "AspiOperatorSessionTransport");
            }
            if (failed)
            {
                throw new InvalidOperationException(
                    "The operator session failed closed and cannot create " +
                    "another transport cycle.");
            }
            if (cycle == null)
            {
                try
                {
                    cycle = cycleFactory();
                }
                catch
                {
                    failed = true;
                    throw;
                }
                if (cycle == null)
                {
                    failed = true;
                    throw new InvalidOperationException(
                        "The operator transport cycle factory returned null.");
                }
            }
            return cycle;
        }

        private void RetireCompletedCycle(
            IAspiOperatorCycleTransport current)
        {
            if (!current.CycleCompleted)
            {
                return;
            }
            if (!Object.ReferenceEquals(cycle, current))
            {
                throw new InvalidOperationException(
                    "The completed operator transport was not the active " +
                    "cycle.");
            }
            cycle = null;
            ++completedCycles;
            current.Dispose();
        }

        private void FailClosed(IAspiOperatorCycleTransport current)
        {
            failed = true;
            if (Object.ReferenceEquals(cycle, current))
            {
                cycle = null;
            }
            try
            {
                current.Dispose();
            }
            catch
            {
                // Preserve the command failure that caused terminalization.
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }
                disposed = true;
                if (cycle != null)
                {
                    IAspiOperatorCycleTransport current = cycle;
                    cycle = null;
                    current.Dispose();
                }
            }
        }
    }
}
