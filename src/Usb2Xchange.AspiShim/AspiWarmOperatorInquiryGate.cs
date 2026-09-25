// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using Usb2Xchange.Protocol;

namespace Usb2Xchange.AspiShim
{
    internal sealed class AspiWarmOperatorInquiryGate
    {
        internal const int RequiredStabilizationInquiryCount = 2;

        private bool armed;
        private bool attempted;
        private bool completionPending;
        private bool completed;
        private bool initialScannerReadyAccepted;
        private bool stabilizationInquiryPending;
        private int stabilizationInquiriesCompleted;
        private bool failed;

        internal bool Completed
        {
            get { return completed && !failed; }
        }

        internal bool InitialScannerReadyAccepted
        {
            get { return initialScannerReadyAccepted && !failed; }
        }

        internal int StabilizationInquiriesCompleted
        {
            get { return failed ? 0 : stabilizationInquiriesCompleted; }
        }

        internal bool StabilizationSequenceCompleted
        {
            get
            {
                return !failed && !stabilizationInquiryPending &&
                    stabilizationInquiriesCompleted ==
                        RequiredStabilizationInquiryCount;
            }
        }

        internal void Arm()
        {
            if (armed || attempted || completionPending || completed ||
                stabilizationInquiryPending ||
                stabilizationInquiriesCompleted != 0 || failed)
            {
                FailClosed();
                throw new InvalidOperationException(
                    "Warm operator INQUIRY authority can be armed only once " +
                    "from a new gate.");
            }
            armed = true;
        }

        internal void Begin(byte target, byte lun, byte[] cdb,
            uint requestedLength)
        {
            if (!armed || attempted || completionPending || completed ||
                failed || target != 5 || lun != 0 || requestedLength != 96 ||
                cdb == null || cdb.Length != 6 || cdb[0] != 0x12 ||
                cdb[1] != 0 || cdb[2] != 0 || cdb[3] != 0 ||
                cdb[4] != 0x60 || cdb[5] != 0)
            {
                FailClosed();
                throw new InvalidOperationException(
                    "Warm operator Preview must begin with one exact " +
                    "target-5/LUN-0 96-byte INQUIRY.");
            }
            attempted = true;
            completionPending = true;
        }

        internal void Complete(byte adapterStatus, uint actualLength,
            uint residue, bool exactOperationalIdentity)
        {
            if (!armed || !attempted || !completionPending || completed ||
                failed)
            {
                FailClosed();
                throw new InvalidOperationException(
                    "Warm operator INQUIRY has no matching pending attempt.");
            }
            completionPending = false;
            if (adapterStatus != (byte)AdapterStatus.Success ||
                actualLength != 96 || residue != 0 ||
                !exactOperationalIdentity)
            {
                FailClosed();
                throw new InvalidOperationException(
                    "Warm operator INQUIRY did not return exact operational " +
                    "M333 identity at its full successful length.");
            }
            completed = true;
        }

        internal void BeginInitialScannerReady(byte target, byte lun,
            byte[] cdb, uint requestedLength)
        {
            byte[] expected =
                ScsiFraming.BuildPrecisionTwoScannerReadyCdb();
            if (!Completed || initialScannerReadyAccepted || target != 5 ||
                lun != 0 || requestedLength !=
                    ScsiFraming.PrecisionTwoScannerReadyLength ||
                !Matches(cdb, expected))
            {
                FailClosed();
                throw new InvalidOperationException(
                    "The exact warm operational INQUIRY must be followed " +
                    "immediately by one target-5/LUN-0 DF.");
            }
            initialScannerReadyAccepted = true;
        }

        internal void BeginStabilizationInquiry(byte target, byte lun,
            byte[] cdb, uint requestedLength,
            int completedInitializationCycles)
        {
            int expectedCycles = AspiOperationalSequenceGate.
                MaximumInitializationCycles - 3 +
                stabilizationInquiriesCompleted;
            if (!Completed || !InitialScannerReadyAccepted ||
                stabilizationInquiryPending ||
                stabilizationInquiriesCompleted >=
                    RequiredStabilizationInquiryCount ||
                completedInitializationCycles != expectedCycles ||
                !IsExactInquiry(target, lun, cdb, requestedLength))
            {
                FailClosed();
                throw new InvalidOperationException(
                    "Warm Preview stabilization permits exactly two " +
                    "counter-bound target-5/LUN-0 96-byte INQUIRY " +
                    "revalidations.");
            }
            stabilizationInquiryPending = true;
        }

        internal void CompleteStabilizationInquiry(byte adapterStatus,
            uint actualLength, uint residue, bool exactOperationalIdentity)
        {
            if (!Completed || !stabilizationInquiryPending)
            {
                FailClosed();
                throw new InvalidOperationException(
                    "Warm stabilization INQUIRY has no matching pending " +
                    "attempt.");
            }
            stabilizationInquiryPending = false;
            if (adapterStatus != (byte)AdapterStatus.Success ||
                actualLength != 96 || residue != 0 ||
                !exactOperationalIdentity)
            {
                FailClosed();
                throw new InvalidOperationException(
                    "Warm stabilization INQUIRY did not return exact " +
                    "operational M333 identity at its full successful " +
                    "length.");
            }
            ++stabilizationInquiriesCompleted;
        }

        internal void FailClosed()
        {
            completionPending = false;
            stabilizationInquiryPending = false;
            failed = true;
        }

        private static bool IsExactInquiry(byte target, byte lun, byte[] cdb,
            uint requestedLength)
        {
            return target == 5 && lun == 0 && requestedLength == 96 &&
                cdb != null && cdb.Length == 6 && cdb[0] == 0x12 &&
                cdb[1] == 0 && cdb[2] == 0 && cdb[3] == 0 &&
                cdb[4] == 0x60 && cdb[5] == 0;
        }

        private static bool Matches(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }
            int difference = 0;
            for (int index = 0; index < left.Length; ++index)
            {
                difference |= left[index] ^ right[index];
            }
            return difference == 0;
        }
    }
}
