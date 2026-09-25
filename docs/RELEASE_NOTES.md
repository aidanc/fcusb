<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# v0.1.0-poc.1 — experimental downloadable prerelease

> **EXPERIMENTAL SOFTWARE — USE AT YOUR OWN RISK.** Defects, unsupported hardware
> or interrupted firmware/scanner operation can cause failed scans, data loss,
> loss of access, malfunction or hardware damage. No warranty, recovery or
> individual support is promised. Read [DISCLAIMER](../DISCLAIMER.md).

This initial source publication supplies the working exact-version ASPI/WinUSB
bridge, private-copy patcher, manager/setup source, build/install scripts and
offline tests under GPL-3.0-only. It adds public setup/acquisition/support guides,
first-use acknowledgement, legal notices in packaging and dedicated 3F structure
documentation. Proprietary prerequisites are separately obtained by users.

This owner-approved prerelease adds a ready-to-run unsigned setup EXE and
runtime ZIP, exact source ZIP and SHA-256 manifests. The manifests identify
the full public source commit. No proprietary prerequisite is bundled.

New in this release: an explicitly isolated `--demo` manager mode, **Try demo**
and **User guide** buttons, a double-click ZIP demo launcher, and an illustrated
offline HTML/Markdown guide with five exports of the actual demo application.
Demo install, two-PID configuration, Start/Stop, repair and uninstall use only
memory. Unknown demo actions fail closed. This models manager state, not USB
or scanner behavior, and never launches FlexColor. The developer-only
`--demo-export <empty-folder>` command exports the actual forms to five PNGs.

Prior clean Windows 10/11 portable runtime results are in
[KNOWN_LIMITATIONS](KNOWN_LIMITATIONS.md). The updated GUI workflow still needs
[fresh-VM acceptance](FRESH_VM_ACCEPTANCE.md). Powered 60x70, in-flight failure
recovery and authentic useful sense remain open. Windows edition/build and
hardware combinations beyond recorded evidence are unqualified.

The sharpening and exact-stock-M333 RAM reports are editable Markdown. A
separate 3F/FFF report distinguishes tested 4.8.9/949 files from 4.0.3 static
evidence and unverified Precision II file layout. No generated sharpening PDF
is included: it should only be regenerated from final public text and separately
reviewed if a later release actually needs it.
