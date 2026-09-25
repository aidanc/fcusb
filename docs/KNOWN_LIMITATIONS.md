<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Known limitations and evidence boundaries

This is an unfinished proof of concept, not production-ready scanner software.
Read [DISCLAIMER](../DISCLAIMER.md). No support or compatibility is guaranteed.

| Scope | Evidence / status |
|---|---|
| Exact English FlexColor 4.0.3, USB2Xchange 03F3:2002/2003, Precision II target 5/LUN 0 | Hardware-observed working configuration. Other models, targets, adapters, languages and versions are unqualified. |
| Portable script-driven Windows 10 x64 runtime | Clean VM, 2026-08-25: dual-PID binding, cold Start, Preview/full Scan/save, owned Stop, reconnect and second lifecycle. Saved TIFF 3600×3621 RGB48 at 1600 dpi. |
| Portable script-driven Windows 11 x64 runtime | Clean VM, 2026-08-26: licensed-tree transfer, exact application-local VC++ 7.1 files, dual-PID setup, cold Start, Preview/full Scan/save, Stop and reconnect. Saved TIFF 749×762 RGB24 at 300 dpi. |
| Transparent runtime on Windows 10 | Real FlexColor/hardware: L302→M333, cancellation/cleanup, complete Preview, two same-process 60x60 scans/saves, 24x36 and 4x5 image flow followed by cancellation. |
| New single-file setup/manager, shortcut, repair, first-risk warning, uninstall | Implemented; device-free checks. Complete GUI acceptance on fresh Windows 10 and 11 VMs is still pending. Prior portable acceptance is not acceptance of these changes. |
| Powered holders | 60x60 has full scan evidence. 24x36 and 4x5 have bounded first-row/image-flow and cleanup evidence, not full natural completion. 60x70 intentionally unverified. |
| Failure/recovery | In-flight physical unplug/stall and authentic useful CHECK CONDITION/sense are not qualified. Synthetic status/sense tests are not hardware evidence. |
| Firmware | Exact user-supplied adapter and scanner images only. No flash upgrades, replacement firmware or RAM modification recommended. |
| Windows integration | User-mode bridge; manual inbox WinUSB binding once per PID. No production signed device INF, Authenticode signature, Windows Update distribution or system-wide ASPI service. |
| Research kernel path | Source/tests retained; not in user packages or normal installation. |
| Image science | Host sharpening and stock M333 RAM conclusions are static/controlled-file evidence. Fixed optical/analog/ASIC/FPGA effects and representative Precision II-created 3F layout remain unverified. |

Transfer caps (16 MiB, 120 seconds per transfer) are implementation limits, not
a claim that every permitted size/CDB is safe for every device. The exact trusted
FlexColor process controls scanner sequencing. The warning does not replace
technical validation, backups or a rollback plan.
