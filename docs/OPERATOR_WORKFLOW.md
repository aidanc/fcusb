<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Advanced script workflow

> Experimental hardware operation is at your own risk. No warranty or recovery
> is promised. Read [DISCLAIMER](../DISCLAIMER.md) before operating equipment.

The [manager](INSTALLATION.md) is the normal interface. The lower-level
`scripts/Usb2Xchange-FlexColor.ps1` exposes Status, Prepare, Start, Stop and
Deactivate. These development commands do not display the manager warning;
read it before deliberately invoking hardware. They are not authorization for
unplanned hardware experiments.

From source or the complete extracted package:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Usb2Xchange-FlexColor.ps1 -Action Status
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Usb2Xchange-FlexColor.ps1 -Action Prepare -SourceRoot 'C:\licensed\FlexColor403' -LegacyRuntimeRoot 'C:\licensed\vc71'
```

Status reports Hardware NotChecked. Prepare verifies inputs, copies only under
out, stages the provider and preflights without USB I/O. Expected states:
PrivateState Prepared, PatchState Active, BackupState Supported, LegacyRuntime
Supported, ProviderStaging Current, ManagedProcess None, ReadyForOperatorStart True.

After deliberate hardware preparation and both bindings:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Usb2Xchange-FlexColor.ps1 -Action Start -SourceRoot 'C:\licensed\FlexColor403' -AdapterFirmwarePath 'C:\local-inputs\usb2xchange.fw'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Usb2Xchange-FlexColor.ps1 -Action Status
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Usb2Xchange-FlexColor.ps1 -Action Stop
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Usb2Xchange-FlexColor.ps1 -Action Deactivate
```

Start verifies adapter/scanner identity and records only its launched process
PID/path/start-time. Stop operates on that owned session. Deactivate restores
the exact private original DLL. Historical `live-*` test modes are developer
research fixtures, not the supported operator flow.
