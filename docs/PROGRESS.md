<!-- Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only -->
# Public progress

2026-09-25: Public source published; preparing the approved v0.1.0-poc.1 download.
The transport is a working proof of concept with prior Windows 10/11 portable
runtime acceptance, not a fully qualified consumer product. See
[KNOWN_LIMITATIONS](KNOWN_LIMITATIONS.md) for evidence and
[PUBLICATION_AUDIT](PUBLICATION_AUDIT.md) for public validation.

The first-use manager warning is versioned and stored locally. Setup and ZIP
packaging include license/disclaimer/notices and operator guides. The firmware
extractor operates only on an exact user-supplied legacy file, without executing
it. Proprietary software/firmware is excluded from all publication assets.

Remaining work: fresh-VM GUI acceptance, separately authorized failure/holder
tests. The owner has approved the prerelease version/assets. No hardware operation
is part of public source preparation.

## Downloadable prerelease and illustrated demo (2026-09-25)

Owner approved v0.1.0-poc.1 with source/runtime ZIPs, unsigned setup EXE and
SHA-256 manifests. Added the isolated manager demo, offline illustrated guide,
five application-rendered demo screens and one-click demo/help entry points.
Demo dispatch returns before any PowerShell, elevation, installation or USB
backend; the lifecycle self-test covers both PIDs and premature-start refusal.
The explicit documentation export writes only PNGs to a chosen empty folder.
No new hardware acceptance is claimed. Clean-VM GUI setup/elevation/shortcuts,
repair and uninstall qualification remains open; see FRESH_VM_ACCEPTANCE.
