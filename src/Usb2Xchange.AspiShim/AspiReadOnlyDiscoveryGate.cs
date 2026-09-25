// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using Usb2Xchange.Protocol;
using Usb2Xchange.WinUsb;

namespace Usb2Xchange.AspiShim
{
    internal enum PrecisionTwoIdentity
    {
        Unknown,
        Loader,
        Operational,
        Other
    }

    internal sealed class AspiReadOnlyDiscoveryGate
    {
        private bool loaderD8Attempted;
        private bool loaderD8Succeeded;
        private bool operationalD8Attempted;
        private bool operationalD8Succeeded;
        private bool d8CompletionPending;
        private PrecisionTwoIdentity pendingD8Identity;
        private bool loaderTransitionCompleted;
        private bool postLoaderScannerReadyAttempted;
        private bool postLoaderScannerReadyCompletionPending;
        private bool postLoaderScannerReadySucceeded;
        private bool postLoaderOperationalInitializationArmed;
        private bool scannerReadyAttempted;

        internal PrecisionTwoIdentity Identity { get; private set; }

        internal bool LoaderSequenceReady
        {
            get
            {
                return Identity == PrecisionTwoIdentity.Loader &&
                    loaderD8Succeeded && !loaderTransitionCompleted;
            }
        }

        internal bool PostLoaderScannerReadyRequired
        {
            get
            {
                return loaderTransitionCompleted &&
                    Identity == PrecisionTwoIdentity.Unknown &&
                    !postLoaderScannerReadyAttempted;
            }
        }

        internal bool PostLoaderScannerReadySucceeded
        {
            get { return postLoaderScannerReadySucceeded; }
        }

        internal bool PostLoaderOperationalInitializationRequired
        {
            get
            {
                return loaderTransitionCompleted &&
                    postLoaderScannerReadySucceeded &&
                    Identity == PrecisionTwoIdentity.Operational &&
                    !postLoaderOperationalInitializationArmed;
            }
        }

        internal AspiReadOnlyDiscoveryGate()
        {
            Identity = PrecisionTwoIdentity.Unknown;
        }

        internal void RecordIdentity(InquiryData identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException("identity");
            }
            Identity = identity.PeripheralDeviceType == 0x06 &&
                string.Equals(identity.Vendor, "Imacon",
                    StringComparison.Ordinal) &&
                string.Equals(identity.Product, "SCSI Loader",
                    StringComparison.Ordinal) &&
                string.Equals(identity.Revision, "L302",
                    StringComparison.Ordinal)
                ? PrecisionTwoIdentity.Loader
                : identity.PeripheralDeviceType == 0x06 &&
                    string.Equals(identity.Vendor, "Imacon",
                        StringComparison.Ordinal) &&
                    string.Equals(identity.Product, "FlexTight II",
                        StringComparison.Ordinal) &&
                    string.Equals(identity.Revision, "M333",
                        StringComparison.Ordinal)
                    ? PrecisionTwoIdentity.Operational
                    : PrecisionTwoIdentity.Other;
        }

        internal void BeginD8(byte target, byte lun, uint requestedLength)
        {
            if (target != 5 || lun != 0 || requestedLength !=
                    ScsiFraming.PrecisionTwoLoaderBufferD8Length ||
                d8CompletionPending ||
                (Identity != PrecisionTwoIdentity.Loader &&
                    Identity != PrecisionTwoIdentity.Operational))
            {
                throw new InvalidOperationException(
                    "READ BUFFER D8 requires the exact Precision II identity " +
                    "and one completion at a time.");
            }
            if (Identity == PrecisionTwoIdentity.Loader)
            {
                if (loaderD8Attempted || loaderTransitionCompleted)
                {
                    throw new InvalidOperationException(
                        "Loader READ BUFFER D8 is limited to one attempt " +
                        "before the loader transition.");
                }
                loaderD8Attempted = true;
            }
            else
            {
                if (operationalD8Attempted)
                {
                    throw new InvalidOperationException(
                        "Operational READ BUFFER D8 is limited to one attempt.");
                }
                if (loaderTransitionCompleted)
                {
                    throw new InvalidOperationException(
                        "Operational READ BUFFER D8 is not the post-loader " +
                        "successor; the exact path continues from the " +
                        "post-terminal ScannerReady through operational " +
                        "identity directly into initialization.");
                }
                operationalD8Attempted = true;
            }
            pendingD8Identity = Identity;
            d8CompletionPending = true;
        }

        internal void RecordD8(byte adapterStatus, uint residue,
            int dataLength)
        {
            if (!d8CompletionPending)
            {
                throw new InvalidOperationException(
                    "A D8 completion cannot be recorded before its attempt.");
            }
            bool succeeded =
                adapterStatus == (byte)AdapterStatus.Success && residue == 0 &&
                dataLength ==
                    ScsiFraming.PrecisionTwoLoaderBufferD8Length;
            if (pendingD8Identity == PrecisionTwoIdentity.Loader)
            {
                loaderD8Succeeded = succeeded;
            }
            else if (pendingD8Identity == PrecisionTwoIdentity.Operational)
            {
                operationalD8Succeeded = succeeded;
            }
            else
            {
                throw new InvalidOperationException(
                    "The pending D8 identity is invalid.");
            }
            pendingD8Identity = PrecisionTwoIdentity.Unknown;
            d8CompletionPending = false;
        }

        internal void RecordLoaderTransitionCompleted()
        {
            if (!LoaderSequenceReady)
            {
                throw new InvalidOperationException(
                    "The loader transition requires exact loader identity and " +
                    "a successful loader D8 prerequisite.");
            }
            loaderTransitionCompleted = true;
            // Discard the pre-terminal identity. Static and live evidence show
            // one ScannerReady while identity is unknown, followed by target-5
            // re-identification and direct operational initialization.
            Identity = PrecisionTwoIdentity.Unknown;
        }

        internal void BeginPostLoaderScannerReady(byte target, byte lun,
            uint requestedLength)
        {
            if (target != 5 || lun != 0 || requestedLength !=
                    ScsiFraming.PrecisionTwoScannerReadyLength ||
                !loaderTransitionCompleted ||
                Identity != PrecisionTwoIdentity.Unknown ||
                postLoaderScannerReadyAttempted ||
                postLoaderScannerReadyCompletionPending)
            {
                throw new InvalidOperationException(
                    "Post-loader ScannerReady requires one exact target-5 " +
                    "attempt immediately after the completed terminal and " +
                    "before operational re-identification.");
            }
            postLoaderScannerReadyAttempted = true;
            postLoaderScannerReadyCompletionPending = true;
        }

        internal void RecordPostLoaderScannerReady(byte adapterStatus,
            uint residue, int actualLength, byte[] data)
        {
            if (!postLoaderScannerReadyCompletionPending)
            {
                throw new InvalidOperationException(
                    "A post-loader ScannerReady completion cannot be " +
                    "recorded before its exact attempt.");
            }
            postLoaderScannerReadySucceeded =
                adapterStatus == (byte)AdapterStatus.Success && residue == 0 &&
                actualLength == ScsiFraming.PrecisionTwoScannerReadyLength &&
                data != null && data.Length ==
                    ScsiFraming.PrecisionTwoScannerReadyLength && data[0] == 0;
            postLoaderScannerReadyCompletionPending = false;
        }

        internal void ArmPostLoaderOperationalInitialization()
        {
            if (!PostLoaderOperationalInitializationRequired)
            {
                throw new InvalidOperationException(
                    "Post-loader operational initialization requires the " +
                    "completed loader transition, successful post-terminal " +
                    "ScannerReady, and exact operational identity.");
            }
            postLoaderOperationalInitializationArmed = true;
        }

        internal void BeginScannerReady(byte target, byte lun,
            uint requestedLength)
        {
            if (target != 5 || lun != 0 || requestedLength !=
                    ScsiFraming.PrecisionTwoScannerReadyLength ||
                Identity != PrecisionTwoIdentity.Operational ||
                !operationalD8Succeeded || scannerReadyAttempted)
            {
                throw new InvalidOperationException(
                    "ScannerReady requires one successful operational D8 and " +
                    "is limited to one exact target-5 attempt.");
            }
            scannerReadyAttempted = true;
        }
    }
}
