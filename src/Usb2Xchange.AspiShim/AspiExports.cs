// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;

namespace Usb2Xchange.AspiShim
{
    public static class AspiExports
    {
        private static readonly AspiFileLog Log = new AspiFileLog();
        private static readonly AspiRuntimeContext Runtime =
            AspiRuntime.Create(Log);
        private static readonly AspiSrbProcessor Processor =
            new AspiSrbProcessor(Runtime.Transport, Log,
                Runtime.LoaderDataOutEnabled,
                Runtime.OperationalReplayEnabled,
                Runtime.PredictedPreviewObservationEnabled,
                Runtime.PreviewSetWindowEnabled,
                Runtime.PreviewSetWindowSha256,
                Runtime.StartupWriteFingerprintEnabled);

        public static uint GetASPI32SupportInfo()
        {
            // ASPI places SS_COMP in the high byte and adapter count in the low byte.
            Log.Info(string.Format(
                "GetASPI32SupportInfo: status=01, adapters={0}, mode={1}.",
                Runtime.AdapterCount, Runtime.Mode));
            return 0x00000100U | Runtime.AdapterCount;
        }

        public static uint SendASPI32Command(IntPtr srb)
        {
            try
            {
                return Processor.Process(srb);
            }
            catch (Exception ex)
            {
                Log.Warning("Unhandled ASPI command failure: " +
                    ex.GetType().FullName + ": " + ex.Message);
                return AspiConstants.StatusError;
            }
        }
    }
}
