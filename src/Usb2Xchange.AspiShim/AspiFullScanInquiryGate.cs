// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using Usb2Xchange.Protocol;
using Usb2Xchange.WinUsb;

namespace Usb2Xchange.AspiShim
{
    internal enum AspiFullScanInquiryKind
    {
        Prefix,
        InStreamRevalidation
    }

    internal sealed class AspiFullScanInquiryGate
    {
        // The powered Preview re-enumerated exact M333 every 30 logical rows.
        // This ceiling covers the shorter 762-row full scan even if its first
        // revalidation occurs at row one.
        internal const int MaximumInStreamRevalidations =
            (PrecisionTwoPreviewCommandManifest.AspiLiveFullScanRows + 29) /
            30;
        internal const int NaturalMaximumInStreamRevalidations =
            (PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanNaturalRows + 29) / 30;
        internal const int Row997CompletionMaximumInStreamRevalidations =
            (PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanRow997CompletionRows + 29) / 30;
        internal const int ProgressMaximumInStreamRevalidations =
            (PrecisionTwoPreviewCommandManifest.
                AspiLiveFullScanProgressMaximumRows + 29) / 30;

        private bool armed;
        private bool prefixAttempted;
        private bool prefixCompleted;
        private bool completionPending;
        private bool failed;
        private AspiFullScanInquiryKind pendingKind;
        private int inStreamRevalidations;
        private int lastInStreamRevalidationRow;
        private int fullScanRowLimit;
        private int maximumInStreamRevalidations;

        internal bool PrefixCompleted
        {
            get { return prefixCompleted && !failed; }
        }

        internal int InStreamRevalidations
        {
            get { return inStreamRevalidations; }
        }

        internal int LastInStreamRevalidationRow
        {
            get { return lastInStreamRevalidationRow; }
        }

        internal int FullScanRowLimit
        {
            get { return fullScanRowLimit; }
        }

        internal int MaximumAllowedInStreamRevalidations
        {
            get { return maximumInStreamRevalidations; }
        }

        internal void Arm(int completedPreviewRows)
        {
            Arm(completedPreviewRows,
                PrecisionTwoPreviewCommandManifest.AspiLiveFullScanRows);
        }

        internal void Arm(int completedPreviewRows, int rowLimit)
        {
            if (armed || failed || completedPreviewRows !=
                    PrecisionTwoPreviewCommandManifest.
                        AspiLivePreviewPoweredNaturalRows ||
                rowLimit != PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanRows &&
                    rowLimit != PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanNaturalRows &&
                    rowLimit != PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanRow997CompletionRows &&
                    rowLimit != PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanProgressMaximumRows)
            {
                FailClosed();
                throw new InvalidOperationException(
                    "Full-scan INQUIRY authority requires exactly one " +
                    "completed 911-row powered Preview.");
            }
            fullScanRowLimit = rowLimit;
            maximumInStreamRevalidations = rowLimit ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanProgressMaximumRows
                ? ProgressMaximumInStreamRevalidations
                : rowLimit ==
                    PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanRow997CompletionRows
                ? Row997CompletionMaximumInStreamRevalidations
                : rowLimit == PrecisionTwoPreviewCommandManifest.
                        AspiLiveFullScanNaturalRows
                    ? NaturalMaximumInStreamRevalidations
                    : MaximumInStreamRevalidations;
            armed = true;
        }

        internal AspiFullScanInquiryKind Begin(byte target, byte lun,
            byte[] cdb, uint requestedLength, bool fullScanStreamActive,
            int completedFullScanRows)
        {
            if (!armed || failed || completionPending || target != 5 ||
                lun != 0 || !IsExactInquiry(cdb, requestedLength))
            {
                FailClosed();
                throw new InvalidOperationException(
                    "Full-scan INQUIRY requires its exact target-5/LUN-0 " +
                    "96-byte envelope and one completion at a time.");
            }

            if (!prefixCompleted)
            {
                if (prefixAttempted || fullScanStreamActive ||
                    completedFullScanRows !=
                        PrecisionTwoPreviewCommandManifest.
                            AspiLivePreviewPoweredNaturalRows)
                {
                    FailClosed();
                    throw new InvalidOperationException(
                        "The full-scan operational INQUIRY prefix is limited " +
                        "to one attempt immediately after powered Preview.");
                }
                prefixAttempted = true;
                pendingKind = AspiFullScanInquiryKind.Prefix;
            }
            else
            {
                if (!fullScanStreamActive || completedFullScanRows <= 0 ||
                    completedFullScanRows >= fullScanRowLimit ||
                    completedFullScanRows <= lastInStreamRevalidationRow ||
                    inStreamRevalidations >= maximumInStreamRevalidations)
                {
                    FailClosed();
                    throw new InvalidOperationException(
                        "An in-stream operational INQUIRY revalidation is " +
                        "permitted only after new full rows and within its " +
                        "exact bounded count.");
                }
                ++inStreamRevalidations;
                lastInStreamRevalidationRow = completedFullScanRows;
                pendingKind = AspiFullScanInquiryKind.InStreamRevalidation;
            }
            completionPending = true;
            return pendingKind;
        }

        internal void Complete(AspiFullScanInquiryKind kind,
            byte adapterStatus, uint actualLength, uint residue,
            bool exactOperationalIdentity)
        {
            if (failed || !completionPending || kind != pendingKind)
            {
                FailClosed();
                throw new InvalidOperationException(
                    "Full-scan INQUIRY completion did not match its pending " +
                    "attempt.");
            }
            completionPending = false;
            if (adapterStatus != (byte)AdapterStatus.Success ||
                actualLength != 96 || residue != 0 ||
                !exactOperationalIdentity)
            {
                FailClosed();
                throw new ProtocolException(
                    "Full-scan INQUIRY did not return an exact successful " +
                    "96-byte Imacon/FlexTight II/M333 completion.");
            }
            if (kind == AspiFullScanInquiryKind.Prefix)
            {
                prefixCompleted = true;
            }
        }

        internal void FailClosed()
        {
            failed = true;
            completionPending = false;
        }

        private static bool IsExactInquiry(byte[] cdb,
            uint requestedLength)
        {
            return requestedLength == 96 && cdb != null &&
                cdb.Length == 6 && cdb[0] == 0x12 && cdb[1] == 0 &&
                cdb[2] == 0 && cdb[3] == 0 && cdb[4] == 0x60 &&
                cdb[5] == 0;
        }
    }
}
