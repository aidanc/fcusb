<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Architecture

```text
Per-user setup / manager / owned lifecycle
                  |
Verified private FlexColor English 4.0.3
                  | existing in-process ASPI ABI
Project x86 wnaspi32.dll facade + managed ASPI provider
                  | serialized target 5 / LUN 0 SRBs
Project WinUSB transport -> Microsoft inbox WinUSB
                  | USB2Xchange CBW / data / CSW
USB2Xchange -> SCSI -> FlexTight Precision II
```

The source-only patcher changes four backend-selection sites (five bytes) in an
exact-hash private FlexColor DLL and retains an exact original backup. It does
not patch scanner firmware or image processing. The GPL/proprietary in-process
loading boundary and distribution uncertainty are documented in [NOTICE](../NOTICE.md).

Normal `trusted-flexcolor-pass-through` trusts only the exact executable,
active patch, original backup and private-tree location. It lets FlexColor own
scanner command order. The provider validates ASPI flags, 6/10/12/16-byte CDB
shape, direction/buffer length and target/LUN; caps transfers at 16 MiB; serializes
each request with automatic sense; bounds each underlying USB operation at
120 seconds; and validates signatures, tags, residue and status. The retained
handle is discarded after transport exceptions. It does not promise physical
recovery after disconnects.

Short successful rows that FlexColor cannot represent as residual count become
BUSY with no partial copy. This hardware-derived compatibility behavior lets
FlexColor retry the logical row. Logs retain command and completion metadata,
not image/data-out payloads. Paths and device identifiers still require redaction.

The manager coordinates per-user installation, exact private preparation,
dual-PID WinUSB registration, firmware initialization and owned Start/Stop.
Before Start/Initialize/Register it requires the warning-version marker through
a No-default UI and a backend check. Status uses PnP metadata without SCSI I/O;
the lower-level lifecycle Status is entirely hardware-unchecked. The marker is
communication, not a security boundary against the local user or lower-level
developer commands. Technical transport/process guards remain independent.

`src/Usb2Xchange.Protocol` and `WinUsb` implement transport; `AspiShim` bridges
the application; `FlexColorPatch` and `FlexColorAspiLauncher` enforce private
identity; `Manager` and `SetupBootstrap` provide UI/packaging. Historical broker,
miniport and command-specific research fixtures remain source/test components.
No research kernel binary or installation utility is packaged for end users.
