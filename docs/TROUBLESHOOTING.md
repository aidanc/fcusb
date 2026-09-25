<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Troubleshooting

> **EXPERIMENTAL SOFTWARE — USE AT YOUR OWN RISK.** Recovery is not guaranteed.
> Failed scans, data loss, malfunction or hardware damage are possible. No
> warranty or individual support is promised. Read [DISCLAIMER](../DISCLAIMER.md).

Stop on identity or hash failures. Do not install an XP driver, weaken Windows
security, change safety constants, or download DLLs from a DLL-download site.

| Symptom/status | Corrective action |
|---|---|
| Setup not available as a public release | Build source; no binary download is implied by source publication. |
| Unknown publisher/SmartScreen | Verify source commit and SHA-256. Current artifacts are unsigned. Do not disable protection globally. |
| Legacy setup cannot run on this PC | Its launcher is 16-bit. Supply a complete licensed 4.0.3 installed tree. |
| MFC71.dll/MSVCR71.dll/MSVCP71.dll missing or loader error | Supply all three exact x86 hashes together and Repair. New VC++ packages are not replacements. |
| Source not exact supported FlexColor 4.0.3 | Verify English executable, original DLL and scanner firmware; restore licensed original. Never bypass checks. |
| NotInstalled/NeedsPreparation | Run Set up with complete prerequisites; Repair requires an existing installation. |
| Risk acknowledgement required | Open the manager, read and explicitly accept the warning. Refusal cancels operation. |
| Adapter absent | Verify cable, VM USB ownership and both PID passthrough rules; inspect present PnP Hardware Ids. |
| NeedsWinUsb | Select Microsoft inbox WinUsb Device for the currently present exact PID. |
| NeedsInterfaceRegistration | Register interface for that PID; accept UAC, reconnect if requested. |
| Firmware loaded but timed out / PID 2003 appeared | Provision the second PID binding/GUID, then cold reconnect with FlexColor stopped. |
| Multiple/ambiguous adapter | Disconnect extras; do not guess which device commands will reach. |
| Scanner not found / unexpected identity | Verify target 5, power, termination and supported model using its manual. Close other FlexColor instances. Do not run arbitrary CDBs. |
| Untrusted/stale session | Use Status and normal owned Stop; do not kill unrelated FlexColor or delete ownership records blindly. |
| Provider staging or backup mismatch | Close the managed process, verify sources and Repair. Preserve the original backup. |
| Transfer timeout/unplug/stall | Close owned application if possible, retain redacted error and use rollback/reconnect plan. In-flight recovery is unqualified. |
| Poor-looking saved-3F preview | Distinguish processed preview, IFD 0 source and export settings; consult 3F/sharpening reports. |

Before reporting, read [CONTRIBUTING](../CONTRIBUTING.md). Record `winver`, x64,
VM/physical status, USB controller mode, exact VID/PID/identity, file SHA-256,
public commit, exact command/output, scanner power state and snapshot/rollback
status. Mark unsupported combinations clearly.

Use Manager → Open logs and copy only relevant metadata lines into a separate
text file for review. Replace usernames, full private paths, serial/device
instance suffixes, personal filenames and unrelated details with placeholders.
Retain command ordering, CDB, lengths, status and error text needed to reproduce.
Review manually before pasting. Never attach proprietary files, firmware,
scans, 3F/FFF, manuals, raw buffers, USB traces or crash dumps.

Individual installation support or recovery assistance is not promised.
Documentation-answered questions may be closed with a relevant link.
