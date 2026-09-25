<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Fresh Windows 10/11 GUI acceptance checklist

> Experimental hardware operation requires a specific operator-approved test
> and rollback point. Read [DISCLAIMER](../DISCLAIMER.md). No such test is implied
> by publishing source or running the offline suite.

Previous portable script-driven acceptance is summarized with dates/results in
[KNOWN_LIMITATIONS](KNOWN_LIMITATIONS.md). The checklist below remains open for
the current public manager/setup revision. Qualify both operating systems
independently and record edition, build, VM software and USB-controller mode.

| Test | Required resources | Completion evidence |
|---|---|---|
| Verify setup/ZIP and manifest against exact source commit | Clean Windows 10 and 11 VMs; no hardware | SHA-256, unsigned-publisher status, inspected payload list |
| Set up complete licensed tree and runtime files | Clean VM and local proprietary prerequisites; scanner off | Per-user private app, shortcuts, uninstall entry; original tree unchanged |
| Risk refusal | Clean VM; preferably adapter disconnected | No Start/Initialize/Register operation or hardware process dispatched; no acknowledgement marker |
| Risk acceptance and persistence | Clean VM | Full warning visible and readable; No default; only version marker saved; restart reuse and deletion re-prompt |
| PID 2002 inbox binding/GUID | Adapter, clean VM snapshot | Exact Hardware Ids, WinUSB service, project GUID, Ready state |
| Initialize and PID 2003 binding/GUID | Adapter firmware transition, same VM | Verified input; PID change; expected first-time second-binding prompt; Ready state |
| Normal project shortcut from cold PID 2002 | Adapter plus powered Precision II | Firmware initialization, target-5 L302/M333, owned FlexColor process |
| Preview, full 60x60 Scan/save | Powered scanner, expendable original | Normal cleanup and independently validated TIFF geometry/depth/output |
| Stop and same-process repeat/cancellation | Powered scanner | Normal in-app Stop cleanup, recovery, and manager closing only its owned process |
| Physical reconnect while idle | Adapter, powered scanner | PID 2002→2003, M333 rediscovery, second owned Start/Status/Stop |
| Repair | Local inputs, clean VM; stop application first | Exact hashes/provider state, original source unchanged; deliberate later Start still works |
| Uninstall | Both clean VMs, app stopped | Private tree/firmware/config/acknowledgement/shortcuts removed; original tree intact; binding remains documented |
| Rollback | Snapshot or tested alternate rollback | Restore pre-binding state; no project kernel driver/certificate/boot-policy changes |

Keep screenshots/results locally, redacting private details before reporting.
Adapter-only tests cannot qualify scanner operation. Synthetic tests cannot
qualify USB transport or physical output. Do not force unplug during a command
as part of normal acceptance: that needs a separate failure-test plan. Powered
60x70, useful CHECK CONDITION/sense and failure recovery are separate future work.
