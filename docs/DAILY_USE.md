<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Daily operation

> **EXPERIMENTAL SOFTWARE — USE AT YOUR OWN RISK.** Failed scans, data loss,
> malfunction and hardware damage are possible. No warranty, guaranteed recovery
> or individual support is offered. Read [DISCLAIMER](../DISCLAIMER.md).

Read the scanner manual for cabling, termination, safe holder loading and power
sequence. Use the supported scanner at target 5/LUN 0 and protect valuable
originals. Do not unplug USB or change SCSI connections during operation.

1. Turn on the scanner normally and connect the adapter. Use **FlexColor with
   USB2Xchange** or **Start FlexColor** in the manager.
2. On first hardware use, read the warning and explicitly acknowledge it.
   Later starts reuse only its local warning-version marker.
3. Start diagnoses PID 2002/2003, uploads verified adapter firmware if needed,
   waits for re-enumeration, and verifies target 5 as L302 or M333. FlexColor
   performs normal scanner microcode initialization. No separate daily loader
   command is needed after both PIDs have been provisioned.
4. In FlexColor, choose the holder/settings, run **Preview**, then **Scan** and
   save to your own backed-up directory outside the installation. Check the
   saved dimensions, depth and appearance. A completed dialog alone does not
   establish output integrity. `60x60` has full-scan evidence; other coverage
   is listed in [KNOWN_LIMITATIONS](KNOWN_LIMITATIONS.md).
5. To cancel a scan, use FlexColor's **Stop** and allow cleanup to finish. The
   manager's **Stop FlexColor** closes the exact owned application session;
   it is not the same as the in-application scan cancellation control. Prefer
   normal completion/Stop before closing the process.
6. After FlexColor has stopped, USB reconnect is handled by the next normal
   Start. If Start fails, preserve a redacted error excerpt and follow
   [TROUBLESHOOTING](TROUBLESHOOTING.md), rather than repeatedly restarting it.

**Open logs** opens the private `Usb2XchangeLogs` directory. The normal provider
records command/transfer/status metadata without image or data-out payloads.
Paths and device identifiers can still be sensitive. Never post whole logs or
scans automatically. Repair and uninstall are documented separately.

For direct scanner-to-TIFF rendering, the recovered host USM path makes Amount
`-120` neutral; for saved-3F exports, zero/off is neutral. This distinction is
host processing, not a scanner firmware adjustment or proof about all physical
optical/analog processing. Read [the sharpening evidence](PRECISION_II_SHARPENING_ANALYSIS.md).
